#!/usr/bin/env python3
"""Build data/raw/nba_box/box_games.csv from Kaggle B + nba_api LeagueGameFinder."""
from __future__ import annotations

import csv
import time
from pathlib import Path

import pandas as pd
from nba_api.stats.endpoints import leaguegamefinder

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "data" / "raw" / "nba_box"
B_SQLITE = ROOT / "data" / "raw" / "kaggle" / "B_basketball-betting-dataset" / "basketball-final.sqlite"


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    import sqlite3

    con = sqlite3.connect(B_SQLITE)
    rows = con.execute(
        """
        SELECT GAME_DATE_HOME, TEAM_ABBREVIATION_HOME, TEAM_ABBREVIATION_AWAY,
          FGA_HOME, OREB_HOME, TOV_HOME, FTA_HOME, PTS_HOME, FG3A_HOME, FGM_HOME, FG3M_HOME,
          FGA_AWAY, OREB_AWAY, TOV_AWAY, FTA_AWAY, PTS_AWAY, FG3A_AWAY, FGM_AWAY, FG3M_AWAY
        FROM Game
        WHERE FGA_HOME IS NOT NULL AND FGA_AWAY IS NOT NULL
        """
    ).fetchall()
    with (OUT / "box_from_B.csv").open("w", newline="") as f:
        w = csv.writer(f)
        w.writerow(
            [
                "date", "home", "away",
                "fga_h", "oreb_h", "tov_h", "fta_h", "pts_h", "fg3a_h", "fgm_h", "fg3m_h",
                "fga_a", "oreb_a", "tov_a", "fta_a", "pts_a", "fg3a_a", "fgm_a", "fg3m_a", "source",
            ]
        )
        for r in rows:
            w.writerow(list(r) + ["B"])
    print("B", len(rows))

    for season in ["2021-22", "2022-23", "2023-24", "2024-25", "2025-26"]:
        print("fetch", season, flush=True)
        gf = leaguegamefinder.LeagueGameFinder(
            season_nullable=season, season_type_nullable="Regular Season", league_id_nullable="00"
        )
        df = gf.get_data_frames()[0]
        df.to_csv(OUT / f"leaguegamefinder_{season.replace('-', '_')}.csv", index=False)
        time.sleep(1.0)

    # unify (same logic as one-shot)
    games: dict = {}

    def norm(t: str) -> str:
        return t.strip().upper()

    for season in ["2021_22", "2022_23", "2023_24", "2024_25", "2025_26"]:
        p = OUT / f"leaguegamefinder_{season}.csv"
        if not p.exists():
            continue
        df = pd.read_csv(p)
        for _, g in df.groupby("GAME_ID"):
            if len(g) != 2:
                continue
            rows = g.to_dict("records")
            home = away = None
            for r in rows:
                m = str(r["MATCHUP"])
                if " vs." in m or " vs " in m:
                    home = r
                elif " @ " in m:
                    away = r
            if home is None or away is None:
                continue
            date = pd.to_datetime(home["GAME_DATE"]).strftime("%Y-%m-%d")
            key = (date, norm(home["TEAM_ABBREVIATION"]), norm(away["TEAM_ABBREVIATION"]))
            games[key] = {
                "date": date,
                "home": key[1],
                "away": key[2],
                "fga_h": home["FGA"], "oreb_h": home["OREB"], "tov_h": home["TOV"], "fta_h": home["FTA"],
                "pts_h": home["PTS"], "fg3a_h": home["FG3A"], "fgm_h": home["FGM"], "fg3m_h": home["FG3M"],
                "fga_a": away["FGA"], "oreb_a": away["OREB"], "tov_a": away["TOV"], "fta_a": away["FTA"],
                "pts_a": away["PTS"], "fg3a_a": away["FG3A"], "fgm_a": away["FGM"], "fg3m_a": away["FG3M"],
                "source": "nba_api",
            }

    with (OUT / "box_from_B.csv").open() as f:
        for row in csv.DictReader(f):
            date = row["date"][:10]
            key = (date, norm(row["home"]), norm(row["away"]))
            if key in games:
                continue
            games[key] = {**row, "date": date, "home": key[1], "away": key[2], "source": "B"}

    fields = [
        "date", "home", "away",
        "fga_h", "oreb_h", "tov_h", "fta_h", "pts_h", "fg3a_h", "fgm_h", "fg3m_h",
        "fga_a", "oreb_a", "tov_a", "fta_a", "pts_a", "fg3a_a", "fgm_a", "fg3m_a", "source",
    ]
    path = OUT / "box_games.csv"
    with path.open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for k in sorted(games):
            w.writerow({fi: games[k].get(fi, "") for fi in fields})
    print("wrote", path, "n=", len(games))


if __name__ == "__main__":
    main()
