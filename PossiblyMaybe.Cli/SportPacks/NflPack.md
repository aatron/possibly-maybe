# NFL sport pack — Gate 0 / feature plan

**Do not change NBA champ paths.**

## Are we using football-critical metrics today?

**No.** Data landed locally; no NFL totals model yet.  
NBA “pace × ORtg” is the right *shape*, but NFL needs **plays/EPA**, not basketball possessions.

## What sites say matters for NFL totals (ranked)

| Priority | Metric | Why | Free sources |
|---|---|---|---|
| 1 | **Offensive EPA/play** (opp-adjusted) | Best efficiency signal | nflfastR / nflverse play-by-play |
| 2 | **Defensive EPA/play** allowed | Mirror of offense | same |
| 3 | **Neutral-script pace** (plays/game) | Volume of scoring chances | nflverse |
| 4 | **Weather — wind ≥15 mph** | Biggest under lever outdoors | Forecast pre-kick; roof known from schedule |
| 5 | Red zone TD%, success rate, explosives | Secondary | nflverse |
| 6 | Game script / big spread | Clock-killing unders | market spread |

Dome/closed roof ≈ small over bump. Cold alone is often already priced; **wind** is the weather edge.

## Gate 0 (data)

- [x] Spreadspoke → `data/raw/kaggle/nfl_scores_and_betting/` (~14k games, **close** OU)
- [x] minmaxer → `data/raw/kaggle/nfl_odds_2026/nfl_2026_odds.csv` (**open/close**, only **~92** games in 2026 — too thin for a full Gate 1 holdout alone)
- [x] nflverse close-only → `data/raw/nfl/nflverse_games.csv`
- [ ] Import sport-tagged games + totals
- [ ] Need **more NFL open→close history** before a serious kill gate (92 rows ≠ season holdout)
- [ ] Feature ingest: nflverse EPA/pace (free) before claiming soft spots

## Gate 1 feature order

1. Import open/close where we have it + close history for scores
2. Baseline: recent points / plays (honest, weak)
3. **EPA off/def + neutral pace** (must-have vs industry)
4. Roof + wind when forecastable historically
5. Kill if holdout CLV/PnL fail

## Honest limit

Copying NBA PaceEff without EPA/SP-equivalent = cargo-cult. Football “offense and defense” means **EPA and pace**, not win/loss Elo alone.
