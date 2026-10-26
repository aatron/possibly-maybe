#!/usr/bin/env python3
"""Build mls/mls_totals_open_close.csv from Kaggle parquet + ASA xG (gitignored)."""
from __future__ import annotations

import json
import re
import urllib.request
from pathlib import Path

import pandas as pd
import pyarrow.parquet as pq

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "data" / "raw"
OUT = RAW / "mls" / "mls_totals_open_close.csv"

ALIAS = {
    "Atlanta United": "Atlanta United FC",
    "Austin FC": "Austin FC",
    "CF Montreal": "CF Montréal",
    "Charlotte FC": "Charlotte FC",
    "Chicago Fire": "Chicago Fire FC",
    "Colorado Rapids": "Colorado Rapids",
    "Columbus Crew": "Columbus Crew",
    "DC United": "D.C. United",
    "FC Cincinnati": "FC Cincinnati",
    "FC Dallas": "FC Dallas",
    "Houston Dynamo": "Houston Dynamo FC",
    "Inter Miami CF": "Inter Miami CF",
    "Los Angeles FC": "Los Angeles FC",
    "Los Angeles Galaxy": "LA Galaxy",
    "Minnesota United FC": "Minnesota United FC",
    "Nashville SC": "Nashville SC",
    "New England Revolution": "New England Revolution",
    "New York City FC": "New York City FC",
    "New York Red Bulls": "New York Red Bulls",
    "Orlando City": "Orlando City SC",
    "Philadelphia Union": "Philadelphia Union",
    "Portland Timbers": "Portland Timbers FC",
    "Real Salt Lake": "Real Salt Lake",
    "San Diego FC": "San Diego FC",
    "San Jose Earthquakes": "San Jose Earthquakes",
    "Seattle Sounders": "Seattle Sounders FC",
    "Sporting Kansas City": "Sporting Kansas City",
    "St. Louis City": "St. Louis City SC",
    "Toronto FC": "Toronto FC",
    "Vancouver Whitecaps": "Vancouver Whitecaps FC",
}


def norm(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", "", str(s).lower())


def load_teams() -> dict[str, str]:
    cache = RAW / "mls" / "asa_teams.json"
    if cache.exists():
        teams = json.loads(cache.read_text())
    else:
        with urllib.request.urlopen(
            "https://app.americansocceranalysis.com/api/v1/mls/teams", timeout=30
        ) as resp:
            teams = json.load(resp)
        cache.write_text(json.dumps(teams))
    return {t["team_id"]: t["team_name"] for t in teams}


def main() -> None:
    mls_path = RAW / "mls" / "mls_matches.parquet"
    if not mls_path.exists():
        full = RAW / "kaggle" / "football_odds_movements_2018_2026" / "matches.parquet"
        df = pq.read_table(full).to_pandas()
        mask = df["league_name"].astype(str).str.contains("MLS", case=False, na=False)
        if "comp_key" in df.columns:
            mask = mask | df["comp_key"].astype(str).str.contains("MLS", case=False, na=False)
        mls = df.loc[mask].copy()
    else:
        mls = pq.read_table(mls_path).to_pandas()

    closing = pq.read_table(
        RAW / "kaggle" / "football_odds_movements_2018_2026" / "match_closing.parquet"
    ).to_pandas()
    c = closing[closing["match_id"].isin(set(mls["match_id"]))].copy()
    # Prefer Pinnacle, else Bet365
    pin = c[c["bookmaker"] == "Pinnacle"][
        ["match_id", "open_ou_line", "close_ou_line"]
    ].dropna(subset=["open_ou_line", "close_ou_line"])
    b365 = c[c["bookmaker"] == "Bet365"][
        ["match_id", "open_ou_line", "close_ou_line"]
    ].dropna(subset=["open_ou_line", "close_ou_line"])
    ou = pin.set_index("match_id")
    for mid, row in b365.set_index("match_id").iterrows():
        if mid not in ou.index:
            ou.loc[mid] = row

    tid = load_teams()
    asa = pd.DataFrame(json.loads((RAW / "mls" / "asa_games.json").read_text()))
    xg = pd.DataFrame(json.loads((RAW / "mls" / "asa_xgoals.json").read_text()))
    asa = asa.merge(
        xg[["game_id", "home_team_xgoals", "away_team_xgoals"]], on="game_id"
    )
    asa["home_name"] = asa["home_team_id"].map(tid)
    asa["away_name"] = asa["away_team_id"].map(tid)
    asa["date"] = pd.to_datetime(
        asa["date_time_utc"].str.replace(" UTC", "+0000"), utc=True, format="mixed"
    ).dt.date.astype(str)
    asa["hn"] = asa["home_name"].map(norm)
    asa["an"] = asa["away_name"].map(norm)

    mls = mls.copy()
    mls["date"] = pd.to_datetime(mls["kickoff_utc"], utc=True).dt.date.astype(str)
    mls["hn"] = mls["home_team"].map(lambda x: norm(ALIAS.get(x, x)))
    mls["an"] = mls["away_team"].map(lambda x: norm(ALIAS.get(x, x)))
    joined = mls.merge(
        asa[
            [
                "date",
                "hn",
                "an",
                "home_team_xgoals",
                "away_team_xgoals",
            ]
        ],
        on=["date", "hn", "an"],
        how="left",
    )

    rows = []
    for _, r in joined.iterrows():
        mid = int(r["match_id"])
        if mid not in ou.index:
            continue
        o = ou.loc[mid]
        rows.append(
            {
                "match_id": mid,
                "kickoff_utc": r["kickoff_utc"],
                "season": r["season"],
                "home_team": r["home_team"],
                "away_team": r["away_team"],
                "home_score": int(r["ft_home"]),
                "away_score": int(r["ft_away"]),
                "open_ou": float(o["open_ou_line"]),
                "close_ou": float(o["close_ou_line"]),
                "home_xg": ""
                if pd.isna(r["home_team_xgoals"])
                else float(r["home_team_xgoals"]),
                "away_xg": ""
                if pd.isna(r["away_team_xgoals"])
                else float(r["away_team_xgoals"]),
            }
        )

    out = pd.DataFrame(rows)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    out.to_csv(OUT, index=False)
    with_xg = (out["home_xg"] != "").sum()
    print(f"wrote {OUT} games={len(out)} with_xg={with_xg}")


if __name__ == "__main__":
    main()
