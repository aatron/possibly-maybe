# NFL Gate 0 raw data — pending downloads

Kaggle requires login. Fetch these manually into `data/raw/` (gitignored); do not commit the CSVs.

## Required (human download)

| Priority | Source | URL | Why |
|---|---|---|---|
| **1** | Spreadspoke-style scores + betting (long history) | https://www.kaggle.com/datasets/tobycrabtree/nfl-scores-and-betting-data | Games, scores, spreads/totals backbone |
| **2** | Open/close totals + spreads | https://www.kaggle.com/datasets/minmaxer/nfl-odds-2026 | **Critical for CLV** (open→close) |

Suggested local paths after unpack (mirror NBA layout):

- `data/raw/kaggle/nfl_scores_and_betting/` — Toby Crabtree / Spreadspoke
- `data/raw/kaggle/nfl_odds_2026/` — minmaxer open/close

## Free close-only fallback (no auth)

- **URL:** https://raw.githubusercontent.com/nflverse/nfldata/master/data/games.csv
- **Local:** `data/raw/nfl/nflverse_games.csv`
- Close lines / scores only → `ClvQuality=CloseOnly` until Kaggle open/close is present

## Status

| Source | Status |
|---|---|
| tobycrabtree/nfl-scores-and-betting-data | **Downloaded** → `kaggle/nfl_scores_and_betting/` (from `archive(7).zip`) |
| minmaxer/nfl-odds-2026 | **Downloaded** → `kaggle/nfl_odds_2026/` (~92 open/close rows — thin) |
| nflverse games.csv | **Downloaded** → `nfl/nflverse_games.csv` (~2.1 MB, close-only) |

**Metrics:** see `SportPacks/NflPack.md` — EPA/pace/wind not implemented yet. Do not ship a “points EMA” NFL champ and call it done.
