#!/usr/bin/env python3
"""Fetch ESPN NBA summary player lines (MIN/PTS/DNP) for walk-forward absence tests.

Resume-safe CSV: data/raw/nba_box/espn_player_box.csv
DNP for game G ≈ tip-time availability (not the live injury board on old summaries).
"""
from __future__ import annotations

import csv
import json
import sqlite3
import time
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from threading import Lock

UA = {"User-Agent": "PossiblyMaybeAbsence/0.1"}
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "data/raw/nba_box/espn_player_box.csv"
DB = ROOT / "possiblymaybe.db"
# Need history before holdout 2025 so player PPG is warm.
SEASONS = ("2023", "2024", "2025")
WORKERS = 6


def fetch_summary(eid: str) -> dict:
    url = f"https://site.api.espn.com/apis/site/v2/sports/basketball/nba/summary?event={eid}"
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=40) as r:
        return json.load(r)


def parse_min(raw: str) -> float:
    if not raw or raw == "-":
        return 0.0
    if ":" in raw:
        m, s = raw.split(":", 1)
        try:
            return int(m) + int(s) / 60.0
        except ValueError:
            return 0.0
    try:
        return float(raw)
    except ValueError:
        return 0.0


def rows_from_summary(eid: str, data: dict) -> list[dict]:
    out = []
    players = (data.get("boxscore") or {}).get("players") or []
    for side in players:
        team = ((side.get("team") or {}).get("abbreviation") or "").upper()
        stats_blocks = side.get("statistics") or []
        if not stats_blocks:
            continue
        labels = stats_blocks[0].get("labels") or []
        try:
            i_min = labels.index("MIN")
            i_pts = labels.index("PTS")
        except ValueError:
            continue
        for a in stats_blocks[0].get("athletes") or []:
            ath = a.get("athlete") or {}
            st = a.get("stats") or []
            dnp = bool(a.get("didNotPlay"))
            mins = 0.0 if dnp or i_min >= len(st) else parse_min(str(st[i_min]))
            pts = 0.0
            if not dnp and i_pts < len(st):
                try:
                    pts = float(st[i_pts])
                except ValueError:
                    pts = 0.0
            out.append(
                {
                    "espn_game_id": eid,
                    "team": team,
                    "player_id": str(ath.get("id") or ""),
                    "player_name": ath.get("displayName") or "",
                    "minutes": f"{mins:.2f}",
                    "pts": f"{pts:.1f}",
                    "dnp": "1" if dnp else "0",
                    "starter": "1" if a.get("starter") else "0",
                    "reason": (a.get("reason") or "").replace("\n", " ")[:120],
                }
            )
    return out


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    done: set[str] = set()
    if OUT.exists():
        with OUT.open() as f:
            for row in csv.DictReader(f):
                done.add(row["espn_game_id"])
        print("resume skip games", len(done), flush=True)

    con = sqlite3.connect(DB)
    games = [
        eid
        for eid, in con.execute(
            """
            SELECT EspnGameId FROM Games
            WHERE Sport='NBA' AND EspnGameId IS NOT NULL AND HomeScore IS NOT NULL
              AND Season IN ({})
            ORDER BY TipoffUtc
            """.format(",".join("?" * len(SEASONS))),
            SEASONS,
        )
    ]
    todo = [g for g in games if g not in done]
    print("target", len(games), "todo", len(todo), "seasons", SEASONS, "workers", WORKERS, flush=True)

    new_file = not OUT.exists()
    lock = Lock()
    f = OUT.open("a", newline="")
    w = csv.DictWriter(
        f,
        fieldnames=[
            "espn_game_id",
            "team",
            "player_id",
            "player_name",
            "minutes",
            "pts",
            "dnp",
            "starter",
            "reason",
        ],
    )
    if new_file:
        w.writeheader()

    ok = err = 0

    def one(eid: str):
        data = fetch_summary(eid)
        return eid, rows_from_summary(eid, data)

    with ThreadPoolExecutor(max_workers=WORKERS) as pool:
        futs = {pool.submit(one, eid): eid for eid in todo}
        for n, fut in enumerate(as_completed(futs), 1):
            eid = futs[fut]
            try:
                _, rows = fut.result()
                with lock:
                    if rows:
                        w.writerows(rows)
                        ok += 1
                    else:
                        err += 1
                    if n % 50 == 0:
                        f.flush()
                        print(f"{n}/{len(todo)} ok={ok} err={err}", flush=True)
            except Exception as e:
                with lock:
                    err += 1
                    if err <= 15 or err % 40 == 0:
                        print("err", eid, e, flush=True)
                time.sleep(0.2)

    f.close()
    print("DONE ok", ok, "err", err, "file", OUT, flush=True)


if __name__ == "__main__":
    main()
