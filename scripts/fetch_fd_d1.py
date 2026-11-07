#!/usr/bin/env python3
"""Fetch football-data.co.uk Bundesliga (D1) CSVs → soccer/bundesliga/totals_open_close.csv.

FD OU is fixed at 2.5 (Avg>2.5 / AvgC>2.5 are prices, not line moves). Prefer Kaggle
variable-line export for Tier A CLV when available:
  python scripts/export_soccer_league_totals.py --league Bundesliga --sport BUNDESLIGA
Then: dotnet run --project PossiblyMaybe.Cli -- import-soccer BUNDESLIGA bundesliga
"""
from __future__ import annotations

import csv
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "data" / "raw" / "soccer" / "bundesliga"
OUT = OUT_DIR / "totals_open_close.csv"
UA = {"User-Agent": "PossiblyMaybe/fetch-fd-d1"}

# yy/yy+1 season folders on football-data.co.uk (Tier A folds + train + Tier C lock).
SEASONS = [
    ("1819", "2018-2019"),
    ("1920", "2019-2020"),
    ("2021", "2020-2021"),
    ("2122", "2021-2022"),
    ("2223", "2022-2023"),
    ("2324", "2023-2024"),
    ("2425", "2024-2025"),
    ("2526", "2025-2026"),
]


def fetch(url: str) -> str:
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as r:
        return r.read().decode("utf-8", errors="replace")


def parse_kickoff(date_s: str, time_s: str) -> str | None:
    # FD Date is DD/MM/YYYY; Time optional HH:MM (local, treat as UTC stamp for warehouse key).
    date_s = (date_s or "").strip()
    time_s = (time_s or "12:00").strip() or "12:00"
    for fmt in ("%d/%m/%Y %H:%M", "%d/%m/%y %H:%M"):
        try:
            dt = datetime.strptime(f"{date_s} {time_s}", fmt).replace(tzinfo=timezone.utc)
            return dt.strftime("%Y-%m-%dT%H:%M:%SZ")
        except ValueError:
            continue
    return None


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    rows: list[dict] = []
    for folder, season in SEASONS:
        url = f"https://www.football-data.co.uk/mmz4281/{folder}/D1.csv"
        try:
            text = fetch(url)
        except Exception as e:  # noqa: BLE001
            print(f"skip {season}: {e}")
            continue
        lines = text.splitlines()
        if not lines:
            continue
        rdr = csv.DictReader(lines)
        for i, c in enumerate(rdr, start=1):
            home, away = (c.get("HomeTeam") or "").strip(), (c.get("AwayTeam") or "").strip()
            if not home or not away:
                continue
            try:
                hs, as_ = int(c["FTHG"]), int(c["FTAG"])
            except (KeyError, ValueError, TypeError):
                continue
            kick = parse_kickoff(c.get("Date") or "", c.get("Time") or "")
            if kick is None:
                continue
            # Fixed main total 2.5; only keep rows that posted OU prices (open or close).
            has_ou = any((c.get(k) or "").strip() for k in ("Avg>2.5", "B365>2.5", "AvgC>2.5", "B365C>2.5"))
            if not has_ou:
                continue
            rows.append(
                {
                    "match_id": f"fd-d1-{season}-{i}",
                    "kickoff_utc": kick,
                    "season": season,
                    "home_team": home,
                    "away_team": away,
                    "home_score": hs,
                    "away_score": as_,
                    "open_ou": 2.5,
                    "close_ou": 2.5,
                    "home_xg": "",
                    "away_xg": "",
                    "sport": "BUNDESLIGA",
                }
            )
        print(f"{season}: +rows from {url}")

    fields = [
        "match_id", "kickoff_utc", "season", "home_team", "away_team",
        "home_score", "away_score", "open_ou", "close_ou", "home_xg", "away_xg", "sport",
    ]
    with OUT.open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)
    print(f"wrote {len(rows)} → {OUT}")


if __name__ == "__main__":
    main()
