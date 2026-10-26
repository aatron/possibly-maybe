#!/usr/bin/env python3
"""Export open/close OU totals CSV for one league from local football-odds Kaggle dump."""
from __future__ import annotations

import argparse
import re
from pathlib import Path

import pyarrow.parquet as pq

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "data" / "raw"
MATCHES = RAW / "kaggle" / "football_odds_movements_2018_2026" / "matches.parquet"
CLOSING = RAW / "kaggle" / "football_odds_movements_2018_2026" / "match_closing.parquet"


def slug(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", "_", s.lower()).strip("_")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--league", required=True, help="Exact league_name, e.g. Championship")
    ap.add_argument("--sport", default=None, help="Sport code for CSV (default: slug of league)")
    args = ap.parse_args()
    sport = (args.sport or slug(args.league)).upper()
    out_dir = RAW / "soccer" / slug(args.league)
    out_dir.mkdir(parents=True, exist_ok=True)
    out = out_dir / "totals_open_close.csv"

    df = pq.read_table(MATCHES).to_pandas()
    league = df[df["league_name"].astype(str) == args.league].copy()
    if league.empty:
        # fuzzy contains
        league = df[df["league_name"].astype(str).str.contains(args.league, case=False, na=False)].copy()
    if league.empty:
        raise SystemExit(f"no matches for league={args.league!r}")

    closing = pq.read_table(CLOSING).to_pandas()
    c = closing[closing["match_id"].isin(set(league["match_id"]))].copy()
    pin = c[c["bookmaker"] == "Pinnacle"][["match_id", "open_ou_line", "close_ou_line"]].dropna(
        subset=["open_ou_line", "close_ou_line"]
    )
    b365 = c[c["bookmaker"] == "Bet365"][["match_id", "open_ou_line", "close_ou_line"]].dropna(
        subset=["open_ou_line", "close_ou_line"]
    )
    ou = pin.set_index("match_id")
    for mid, row in b365.set_index("match_id").iterrows():
        if mid not in ou.index:
            ou.loc[mid] = row

    rows = []
    for _, m in league.iterrows():
        mid = m["match_id"]
        if mid not in ou.index:
            continue
        o = ou.loc[mid]
        open_t, close_t = float(o["open_ou_line"]), float(o["close_ou_line"])
        if abs(open_t - close_t) < 0.25:
            continue
        season = str(m.get("season") or str(m["kickoff_utc"])[:4])
        rows.append(
            {
                "match_id": mid,
                "kickoff_utc": m["kickoff_utc"],
                "season": season,
                "home_team": m["home_team"],
                "away_team": m["away_team"],
                "home_score": int(m["ft_home"]),
                "away_score": int(m["ft_away"]),
                "open_ou": open_t,
                "close_ou": close_t,
                "home_xg": "",
                "away_xg": "",
                "sport": sport,
            }
        )

    import csv

    with out.open("w", newline="") as f:
        w = csv.DictWriter(
            f,
            fieldnames=[
                "match_id",
                "kickoff_utc",
                "season",
                "home_team",
                "away_team",
                "home_score",
                "away_score",
                "open_ou",
                "close_ou",
                "home_xg",
                "away_xg",
                "sport",
            ],
        )
        w.writeheader()
        w.writerows(rows)
    print(f"wrote {len(rows)} → {out} sport={sport} league={args.league}")


if __name__ == "__main__":
    main()
