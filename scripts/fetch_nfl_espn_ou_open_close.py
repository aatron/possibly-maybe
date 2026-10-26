#!/usr/bin/env python3
"""Pull NFL TOTAL open/close from ESPN BET (core odds API). Writes gitignored CSV."""
from __future__ import annotations

import csv
import json
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

UA = {"User-Agent": "Mozilla/5.0 PossiblyMaybe/1.0"}
RAW = Path(__file__).resolve().parents[1] / "data" / "raw" / "espn_nfl_ou"
OUT = RAW / "espn_nfl_ou_open_close.csv"
# Regular-season types=2; holdout year 2024 needs prior seasons for train CLV too
SEASONS = (2021, 2022, 2023, 2024)


def get(url: str, retries: int = 3):
    last = None
    for i in range(retries):
        try:
            req = urllib.request.Request(url, headers=UA)
            with urllib.request.urlopen(req, timeout=25) as r:
                return json.load(r)
        except Exception as e:  # noqa: BLE001
            last = e
            time.sleep(0.4 * (i + 1))
    raise last  # type: ignore[misc]


def event_ids(season: int) -> list[str]:
    ids: list[str] = []
    page = 1
    while True:
        url = (
            "https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/"
            f"seasons/{season}/types/2/events?limit=200&page={page}"
        )
        d = get(url)
        for it in d.get("items") or []:
            ref = it.get("$ref") or ""
            part = ref.split("/events/")[-1].split("?")[0]
            if part.isdigit():
                ids.append(part)
        if page >= int(d.get("pageCount") or 1):
            break
        page += 1
    return ids


def parse_total(blob: dict | None) -> float | None:
    if not blob:
        return None
    tot = blob.get("total") or {}
    for key in ("alternateDisplayValue", "american", "displayValue"):
        v = tot.get(key)
        if v is None:
            continue
        try:
            return float(str(v).replace("+", ""))
        except ValueError:
            continue
    return None


def one_event(eid: str) -> dict | None:
    try:
        ev = get(
            f"https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/events/{eid}"
        )
    except Exception:  # noqa: BLE001
        return None
    comps = (ev.get("competitions") or [None])[0]
    if not isinstance(comps, dict):
        # sometimes competitions is a $ref
        cref = (ev.get("competitions") or {}).get("$ref") if isinstance(ev.get("competitions"), dict) else None
        if not cref and isinstance(ev.get("competitions"), list) and ev["competitions"]:
            first = ev["competitions"][0]
            if isinstance(first, dict) and "$ref" in first:
                cref = first["$ref"]
            elif isinstance(first, dict):
                comps = first
        if cref:
            try:
                comps = get(cref.replace("http://", "https://"))
            except Exception:  # noqa: BLE001
                return None
    if not isinstance(comps, dict):
        return None

    date = (comps.get("date") or ev.get("date") or "")[:10]
    competitors = comps.get("competitors") or []
    # competitors may be $ref list
    if competitors and isinstance(competitors[0], dict) and "$ref" in competitors[0] and "homeAway" not in competitors[0]:
        resolved = []
        for c in competitors:
            try:
                resolved.append(get(c["$ref"].replace("http://", "https://")))
            except Exception:  # noqa: BLE001
                pass
        competitors = resolved

    home = away = None
    hs = as_ = None
    for c in competitors:
        if not isinstance(c, dict):
            continue
        team = c.get("team") or {}
        if isinstance(team, dict) and "$ref" in team and "displayName" not in team:
            try:
                team = get(team["$ref"].replace("http://", "https://"))
            except Exception:  # noqa: BLE001
                team = {}
        name = team.get("displayName") or team.get("name") or ""
        score = c.get("score")
        if isinstance(score, dict):
            score = score.get("value") or score.get("displayValue")
        if c.get("homeAway") == "home":
            home, hs = name, score
        elif c.get("homeAway") == "away":
            away, as_ = name, score
    if not home or not away or not date:
        return None

    season = None
    seas = ev.get("season") or {}
    if isinstance(seas, dict):
        season = seas.get("year")
        if season is None and "$ref" in seas:
            try:
                season = get(seas["$ref"].replace("http://", "https://")).get("year")
            except Exception:  # noqa: BLE001
                pass

    try:
        odds_list = get(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/"
            f"events/{eid}/competitions/{eid}/odds"
        )
    except urllib.error.HTTPError:
        return None

    open_t = close_t = None
    book = ""
    for it in odds_list.get("items") or []:
        ref = (it.get("$ref") or "").replace("http://", "https://")
        if not ref:
            continue
        try:
            o = get(ref)
        except Exception:  # noqa: BLE001
            continue
        name = (o.get("provider") or {}).get("name") or ""
        if name != "ESPN BET":
            continue
        open_t = parse_total(o.get("open"))
        close_t = parse_total(o.get("close"))
        if close_t is None and o.get("overUnder") is not None:
            try:
                close_t = float(o["overUnder"])
            except (TypeError, ValueError):
                pass
        book = name
        break

    if open_t is None or close_t is None:
        return None
    return {
        "espn_id": eid,
        "date_et": date,
        "season": season or date[:4],
        "home_team": home,
        "away_team": away,
        "home_score": hs,
        "away_score": as_,
        "book": book,
        "open_total": open_t,
        "close_total": close_t,
    }


def main() -> None:
    RAW.mkdir(parents=True, exist_ok=True)
    all_ids: list[tuple[int, str]] = []
    for season in SEASONS:
        ids = event_ids(season)
        print(f"season {season}: {len(ids)} events", flush=True)
        all_ids.extend((season, eid) for eid in ids)

    rows: list[dict] = []
    done = 0
    with ThreadPoolExecutor(max_workers=8) as pool:
        futs = {pool.submit(one_event, eid): (season, eid) for season, eid in all_ids}
        for fut in as_completed(futs):
            done += 1
            row = fut.result()
            if row:
                rows.append(row)
            if done % 50 == 0:
                print(f"  progress {done}/{len(all_ids)} kept={len(rows)}", flush=True)

    rows.sort(key=lambda r: (str(r["season"]), r["date_et"], r["home_team"]))
    with OUT.open("w", newline="") as f:
        w = csv.DictWriter(
            f,
            fieldnames=[
                "espn_id",
                "date_et",
                "season",
                "home_team",
                "away_team",
                "home_score",
                "away_score",
                "book",
                "open_total",
                "close_total",
            ],
        )
        w.writeheader()
        w.writerows(rows)
    print(f"wrote {len(rows)} → {OUT}")


if __name__ == "__main__":
    main()
