#!/usr/bin/env python3
"""Fetch Kalshi NBA/MLS total markets and compare to DraftKings-style American odds.

Public API (no auth): https://external-api.kalshi.com/trade-api/v2
Series: KXNBATOTAL, KXMLSTOTAL — binary YES = over {strike}.
"""
from __future__ import annotations

import argparse
import json
import re
import sqlite3
import sys
import urllib.error
import urllib.request
from collections import defaultdict
from datetime import datetime
from typing import Any

BASE = "https://external-api.kalshi.com/trade-api/v2"
UA = {"User-Agent": "PossiblyMaybe/pm-calculate"}
SERIES = {"NBA": "KXNBATOTAL", "MLS": "KXMLSTOTAL"}


def get_json(path: str) -> dict[str, Any]:
    req = urllib.request.Request(BASE + path, headers=UA)
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.load(r)


def all_open_markets(series_ticker: str) -> list[dict[str, Any]]:
    out: list[dict[str, Any]] = []
    cursor = None
    while True:
        path = f"/markets?series_ticker={series_ticker}&status=open&limit=200"
        if cursor:
            path += f"&cursor={cursor}"
        data = get_json(path)
        out.extend(data.get("markets") or [])
        cursor = data.get("cursor")
        if not cursor:
            break
    return out


def yyyymmdd_to_kalshi(d: str) -> str:
    dt = datetime.strptime(d, "%Y%m%d")
    return dt.strftime("%y%b%d").upper()  # 26OCT28


def parse_game_key(key: str) -> tuple[str, str, str] | None:
    m = re.match(r"^(\d{8})\s+(\w+)@(\w+)$", key.strip())
    if not m:
        return None
    return m.group(1), m.group(2).upper(), m.group(3).upper()


def american_from_prob(p: float) -> int | None:
    """Fair American from win probability (price paid if binary $1 contract)."""
    if p <= 0 or p >= 1:
        return None
    if p >= 0.5:
        return int(round(-100 * p / (1 - p)))
    return int(round(100 * (1 - p) / p))


def implied_from_american(american: int) -> float:
    if american == 0:
        raise ValueError("american odds cannot be 0")
    if american < 0:
        return (-american) / ((-american) + 100)
    return 100 / (american + 100)


def decimal_from_american(american: int) -> float:
    if american < 0:
        return 1 + 100 / (-american)
    return 1 + american / 100


def ffloat(x: Any) -> float | None:
    if x is None or x == "":
        return None
    try:
        return float(x)
    except (TypeError, ValueError):
        return None


def index_events(markets: list[dict[str, Any]]) -> dict[str, list[dict[str, Any]]]:
    by: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for m in markets:
        et = m.get("event_ticker") or ""
        by[et].append(m)
    return by


def find_event(
    by_event: dict[str, list[dict[str, Any]]],
    yyyymmdd: str,
    away: str,
    home: str,
) -> tuple[str, list[dict[str, Any]]] | None:
    stamp = yyyymmdd_to_kalshi(yyyymmdd)
    combos = [f"{away}{home}", f"{home}{away}"]
    for et, ms in by_event.items():
        if stamp not in et:
            continue
        tail = et.split("-")[-1].upper() if "-" in et else et.upper()
        if any(c in tail or tail == c for c in combos):
            return et, ms
        # also allow codes embedded anywhere in tail
        if away in tail and home in tail:
            return et, ms
    return None


def pick_strike(markets: list[dict[str, Any]], line: float) -> dict[str, Any] | None:
    scored: list[tuple[float, dict[str, Any]]] = []
    for m in markets:
        strike = ffloat(m.get("floor_strike"))
        if strike is None:
            continue
        scored.append((abs(strike - line), m))
    if not scored:
        return None
    scored.sort(key=lambda t: (t[0], ffloat(t[1].get("floor_strike")) or 0))
    return scored[0][1]


def compare_row(
    *,
    label: str,
    sport: str,
    yyyymmdd: str,
    away: str,
    home: str,
    side: str,
    line: float,
    dk_american: int,
    model_prob: float | None,
    by_event: dict[str, list[dict[str, Any]]],
) -> str:
    side = side.upper()
    hit = find_event(by_event, yyyymmdd, away, home)
    if not hit:
        return f"  {label}: Kalshi — no open {sport} total event for {away}@{home} ({yyyymmdd_to_kalshi(yyyymmdd)})"

    et, markets = hit
    mkt = pick_strike(markets, line)
    if not mkt:
        return f"  {label}: Kalshi event {et} but no strikes"

    strike = ffloat(mkt.get("floor_strike"))
    yes_ask = ffloat(mkt.get("yes_ask_dollars"))
    yes_bid = ffloat(mkt.get("yes_bid_dollars"))
    no_ask = ffloat(mkt.get("no_ask_dollars"))
    # Buy OVER = buy YES; buy UNDER = buy NO (pay no_ask), or sell YES
    if side == "OVER":
        buy_p = yes_ask
        kal_side = "YES(over)"
    else:
        buy_p = no_ask if no_ask is not None else (1 - yes_bid if yes_bid is not None else None)
        kal_side = "NO(under)"

    dk_imp = implied_from_american(dk_american)
    dk_dec = decimal_from_american(dk_american)
    lines = [
        f"  {label}",
        f"    DK     {side} {line} @{dk_american:+d}  implied={dk_imp:.1%}",
    ]

    if buy_p is None or buy_p <= 0 or buy_p >= 1:
        lines.append(f"    Kalshi  {et} strike={strike} {kal_side} — no tradeable ask")
        return "\n".join(lines)

    kal_amer = american_from_prob(buy_p)
    kal_dec = 1.0 / buy_p
    gap_pp = (buy_p - dk_imp) * 100  # + means Kalshi charges more (worse for buyer)
    better = "DK better to buy" if buy_p > dk_imp + 1e-9 else (
        "Kalshi better to buy" if buy_p < dk_imp - 1e-9 else "≈ same"
    )
    lines.append(
        f"    Kalshi  {side} {strike} via {kal_side} ask=${buy_p:.3f} "
        f"(~{kal_amer:+d})  ticker={mkt.get('ticker')}"
    )
    lines.append(
        f"    Edge    buy-price gap={gap_pp:+.1f}pp vs DK → {better}"
    )

    if model_prob is not None and 0 < model_prob < 1:
        ev_dk = model_prob * dk_dec - 1
        ev_kal = model_prob * kal_dec - 1
        pick = "Kalshi" if ev_kal > ev_dk + 1e-9 else ("DK" if ev_dk > ev_kal + 1e-9 else "tie")
        lines.append(
            f"    Model   p={model_prob:.3f}  EV_DK={ev_dk:+.1%}  EV_Kalshi={ev_kal:+.1%}  → prefer {pick}"
        )
    return "\n".join(lines)


def load_caches(sports: set[str]) -> dict[str, dict[str, list[dict[str, Any]]]]:
    caches: dict[str, dict[str, list[dict[str, Any]]]] = {}
    for sport in sports:
        series = SERIES.get(sport.upper())
        if not series:
            continue
        try:
            markets = all_open_markets(series)
            caches[sport.upper()] = index_events(markets)
            print(f"Kalshi {sport.upper()} open events: {len(caches[sport.upper()])} ({len(markets)} markets)", flush=True)
        except urllib.error.HTTPError as e:
            print(f"Kalshi {sport} fetch FAIL: {e}", flush=True)
        except Exception as e:
            print(f"Kalshi {sport} fetch FAIL: {e}", flush=True)
    return caches


def from_paper(db_path: str, caches: dict[str, dict[str, list[dict[str, Any]]]]) -> None:
    con = sqlite3.connect(db_path)
    con.row_factory = sqlite3.Row
    rows = con.execute(
        """
        SELECT GameKey, Side, Line, AmericanOdds, ModelProb, Sport, Scheme
        FROM LedgerEntries WHERE Result IS NULL
        ORDER BY GameKey, Scheme
        """
    ).fetchall()
    if not rows:
        print("No open paper tickets to compare.")
        return

    # one compare per game key (odds same across lanes)
    seen: set[str] = set()
    print("--- Kalshi vs DK (open paper) ---")
    for r in rows:
        key = r["GameKey"]
        if key in seen:
            continue
        seen.add(key)
        parsed = parse_game_key(key)
        if not parsed:
            print(f"  skip unparseable {key}")
            continue
        day, away, home = parsed
        sport = (r["Sport"] or "MLS").upper()
        by_event = caches.get(sport) or {}
        print(
            compare_row(
                label=key,
                sport=sport,
                yyyymmdd=day,
                away=away,
                home=home,
                side=r["Side"],
                line=float(r["Line"]),
                dk_american=int(r["AmericanOdds"]),
                model_prob=float(r["ModelProb"]) if r["ModelProb"] else None,
                by_event=by_event,
            )
        )


def from_slate_args(args: list[str], caches: dict[str, dict[str, list[dict[str, Any]]]]) -> None:
    """Each arg: SPORT|yyyymmdd|AWAY|HOME|SIDE|LINE|DK_AMERICAN|MODEL_P"""
    print("--- Kalshi vs DK (slate GOs) ---")
    for raw in args:
        parts = raw.split("|")
        if len(parts) < 7:
            print(f"  bad slate arg: {raw}")
            continue
        sport, day, away, home, side, line_s, dk_s = parts[:7]
        model_p = float(parts[7]) if len(parts) > 7 and parts[7] else None
        by_event = caches.get(sport.upper()) or {}
        print(
            compare_row(
                label=f"{day} {away}@{home}",
                sport=sport.upper(),
                yyyymmdd=day,
                away=away.upper(),
                home=home.upper(),
                side=side,
                line=float(line_s),
                dk_american=int(dk_s),
                model_prob=model_p,
                by_event=by_event,
            )
        )


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--paper-db", help="Compare open tickets in paper.db")
    ap.add_argument(
        "--slate",
        action="append",
        default=[],
        help="SPORT|yyyymmdd|AWAY|HOME|SIDE|LINE|DK_AMERICAN|MODEL_P (repeatable)",
    )
    ap.add_argument("--sports", default="NBA,MLS", help="Comma sports to cache")
    args = ap.parse_args()

    sports = {s.strip().upper() for s in args.sports.split(",") if s.strip()}
    if args.paper_db:
        # infer sports from open tickets
        try:
            con = sqlite3.connect(args.paper_db)
            for (sp,) in con.execute(
                "SELECT DISTINCT Sport FROM LedgerEntries WHERE Result IS NULL AND Sport != ''"
            ):
                sports.add(sp.upper())
        except Exception:
            pass

    if not args.paper_db and not args.slate:
        ap.print_help()
        return 2

    print(">>> Kalshi odds (public trade-api/v2)")
    caches = load_caches(sports)
    if args.paper_db:
        from_paper(args.paper_db, caches)
    if args.slate:
        from_slate_args(args.slate, caches)
    return 0


if __name__ == "__main__":
    sys.exit(main())
