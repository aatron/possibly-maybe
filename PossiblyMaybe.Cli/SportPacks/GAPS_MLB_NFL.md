# MLB / NFL soft-spot gaps (2026-10-25)

Gate: both **HOLD FAIL** — do not real-bet. NBA champ untouched.

## Already local

### MLB
| Piece | Where | Used in model? |
|---|---|---|
| Open/close totals (~2477) | `data/raw/kaggle/mlb_closing_odds_2025/` | yes (`import-mlb`) |
| Probable SP + **prior-season FIP** (~2590) | `data/raw/mlb/games_sp_fip_2025.csv` + BRef `pitching_bref_202{3,4,5}.csv` | yes (`GamePitching`) |
| Schedule raw | `data/raw/mlb/schedule_raw_2025.csv` | SP join only |
| Sweep | `sweep-mlb` — SP FIP × home PF × market; hold Sep–Nov 2025 **+1.4u / 100**, CLV **−0.03** → **HOLD FAIL** | |
| Park factors | `data/raw/mlb/park_factors.csv` (Savant 2024 3yr `index_runs`; `scripts/fetch_mlb_park_factors.py`) | yes (`MlbParkFactors`) |

### NFL
| Piece | Where | Used in model? |
|---|---|---|
| Close totals + scores (~7.5k) | `data/raw/nfl/nflverse_games.csv` | yes |
| Roof / wind / temp / rest | same → `NflGameContext` | yes (adj on scoring EMA) |
| Spreadspoke close history | `data/raw/kaggle/nfl_scores_and_betting/` | backbone / unused extras |
| Open/close (**~92**, 2026 only) | `data/raw/kaggle/nfl_odds_2026/` | thin CLV overlay |
| Sweep | `sweep-nfl` — scoring + roof/wind; hold 2024 **−0.6u / 14** | |

## Missing vs industry soft-spot models

### MLB (have SP prior FIP; lack the rest of the stack)
| Gap | Why soft spots care |
|---|---|
| ~~**Park factor**~~ | **Done** — home `pf_3yr/100` on modelExp; still HOLD FAIL (CLV) |
| **Weather** (temp, wind out/in) | Open parks; not in M1 CSV |
| **Same-season SP form** | Prior-year FIP misses hot/cold / IL returns mid-season |
| **Lineups / platoon (L/R)** | Posting moves the total after open |

### NFL (have roof/wind + points EMA; lack efficiency + CLV depth)
| Gap | Why soft spots care |
|---|---|
| **EPA/play off + def** (opp-adj) from PBP | Industry pace×efficiency; points EMA is cargo-cult |
| **Neutral-script pace** (plays/game) | Volume of scoring chances |
| **More open→close history** | 92 rows ≠ kill gate; hold CLV almost empty |

---

## Next highest-value free download (each sport)

### MLB → park factors (FanGraphs Guts, free HTML; CSV export is paid)

```bash
mkdir -p data/raw/mlb
# UA matters (Cloudflare). Parse .table-scroll table → CSV with columns season,team,basic_5yr,3yr,1yr,…
curl -A "okhttp/4.12.0" -o data/raw/mlb/fangraphs_pf_2024.html \
  "https://www.fangraphs.com/guts.aspx?type=pf&teamid=0&season=2024"
curl -A "okhttp/4.12.0" -o data/raw/mlb/fangraphs_pf_2025.html \
  "https://www.fangraphs.com/guts.aspx?type=pf&teamid=0&season=2025"
```

Alt (Statcast runs index, page embeds `data = […]`):  
`https://baseballsavant.mlb.com/leaderboard/statcast-park-factors?type=year&year=2024&batSide=&condition=All&rolling=&stat=index_wOBA`  
(repeat `year=2025`). Use **prior-year** 1yr/3yr PF on each game (no same-year leakage).

### NFL → EPA/play from nflverse PBP (free GitHub release)

```bash
mkdir -p data/raw/nfl/pbp
# Seasons covering train + 2024 holdout (+ neighbors for rolling windows)
for y in 2021 2022 2023 2024 2025; do
  curl -L -o "data/raw/nfl/pbp/play_by_play_${y}.parquet" \
    "https://github.com/nflverse/nflverse-data/releases/download/pbp/play_by_play_${y}.parquet"
done
```

Aggregate per team-game: mean `epa` by `posteam` / allowed by `defteam` on non-special plays; rolling prior-week off/def EPA + plays/game. Join on nflverse `game_id`.

*(Open/close denser history is free only as season accrues — re-download minmaxer weekly: https://www.kaggle.com/datasets/minmaxer/nfl-odds-2026 → `data/raw/kaggle/nfl_odds_2026/`. Multi-year open/close is mostly paid.)*

---

## Ordered experiments (flip HOLD FAIL → PASS)

### MLB
1. **Park × SP-FIP** — multiply modelExp by home `3yr` PF/100; same w/edge sweep; freeze hold ≥2025-09-01.
2. **Same-season SP FIP** — rolling FIP from starts *before* tip (leakage-safe); blend with prior; kill if hold still ≤0.
3. **Weather bump** — only if (1) fails narrowly: wind out/temp for open roofs (NOAA or box weather when sourced); else skip to lineups.

### NFL
1. **EPA off/def + neutral pace** replace scoring EMA; keep roof/wind; hold = 2024; pass on hold PnL first (CLV still sparse).
2. **Grow open/close** — weekly minmaxer refresh; re-score CLV on OpenClose rows only; require CLV>0 before “money.”
3. **Wind ≥15** as hard under gate (already have `WindMph`) only after EPA model exists — else optimizing noise.

---

*Packs’ Gate-0 checkboxes are stale; trust this file + `PROGRESS.md` for import/sweep status.*
