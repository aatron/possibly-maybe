#!/usr/bin/env python3
"""Fetch free NBA team tracking into data/raw/nba_tracking/ (gitignored).

Shot quality: LeagueDashTeamPtShot → team_pt_shot_YYYY_YY.csv (EFG_PCT etc.)
Speed/distance: LeagueDashPtStats(pt_measure_type='SpeedDistance') — not SPEED_DISTANCE.
"""
from __future__ import annotations

import time
from pathlib import Path

from nba_api.stats.endpoints import leaguedashptstats, leaguedashteamptshot

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "data" / "raw" / "nba_tracking"
SEASONS = ["2023-24", "2024-25", "2025-26"]


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for season in SEASONS:
        key = season.replace("-", "_")
        print("ptshot", season, flush=True)
        shot = leaguedashteamptshot.LeagueDashTeamPtShot(
            season=season, season_type_all_star="Regular Season", per_mode_simple="Totals"
        )
        shot.get_data_frames()[0].to_csv(OUT / f"team_pt_shot_{key}.csv", index=False)
        time.sleep(1.0)

        print("SpeedDistance", season, flush=True)
        # nba_api uses PtMeasureType.speed_distance == "SpeedDistance" (not SPEED_DISTANCE)
        spd = leaguedashptstats.LeagueDashPtStats(
            season=season,
            season_type_all_star="Regular Season",
            per_mode_simple="PerGame",
            player_or_team="Team",
            pt_measure_type="SpeedDistance",
        )
        spd.get_data_frames()[0].to_csv(OUT / f"team_speed_distance_{key}.csv", index=False)
        time.sleep(1.0)

    print("wrote", OUT)


if __name__ == "__main__":
    main()
