#!/usr/bin/env python3
"""Parse Savant park-factors HTML (or download) → data/raw/mlb/park_factors.csv."""
from __future__ import annotations

import csv
import json
import re
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "data" / "raw" / "mlb" / "park_factors.csv"
URL = (
    "https://baseballsavant.mlb.com/leaderboard/statcast-park-factors"
    "?type=year&year=2024&batSide=&condition=All&rolling=&stat=index_wOBA"
)
TEAM_ID = {
    "108": "LAA", "109": "ARI", "110": "BAL", "111": "BOS", "112": "CHC", "113": "CIN",
    "114": "CLE", "115": "COL", "116": "DET", "117": "HOU", "118": "KC", "119": "LAD",
    "120": "WSH", "121": "NYM", "133": "OAK", "134": "PIT", "135": "SD", "136": "SEA",
    "137": "SF", "138": "STL", "139": "TB", "140": "TEX", "141": "TOR", "142": "MIN",
    "143": "PHI", "144": "ATL", "145": "CWS", "146": "MIA", "147": "NYY", "158": "MIL",
}


def main() -> int:
    html_path = ROOT / "data" / "raw" / "mlb" / "savant_pf.html"
    if html_path.exists():
        html = html_path.read_text(errors="replace")
    else:
        req = urllib.request.Request(URL, headers={"User-Agent": "okhttp/4.12.0"})
        html = urllib.request.urlopen(req, timeout=60).read().decode("utf-8", "replace")
        html_path.parent.mkdir(parents=True, exist_ok=True)
        html_path.write_text(html)
    m = re.search(r"data = (\[.*?\]);", html, re.S)
    if not m:
        print("no data = […] in Savant HTML", file=sys.stderr)
        return 1
    data = json.loads(m.group(1))
    rows = []
    for r in data:
        if str(r.get("key_num_years_rolling")) != "3":
            continue
        team = TEAM_ID.get(str(r.get("main_team_id")))
        if not team:
            print("unmap", r.get("main_team_id"), r.get("name_display_club"), file=sys.stderr)
            continue
        rows.append(
            {
                "season": r["key_year"],
                "team": team,
                "venue": r["venue_name"],
                "club": r["name_display_club"],
                "pf_3yr": int(r["index_runs"]),
                "year_range": r["year_range"],
                "source": "savant_index_runs_3yr",
            }
        )
    rows.sort(key=lambda x: x["team"])
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with OUT.open("w", newline="") as f:
        w = csv.DictWriter(
            f, fieldnames=["season", "team", "venue", "club", "pf_3yr", "year_range", "source"]
        )
        w.writeheader()
        w.writerows(rows)
    print(f"wrote {OUT} n={len(rows)}")
    assert len(rows) == 30
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
