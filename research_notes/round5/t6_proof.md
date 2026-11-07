# T6 proof: NHL + NFL re-test on T2a/T2b data

**Branch:** `r5/t6`  
**Date:** 2026-11-07  
**DB:** `/home/a/source/projects/possibly-maybe/possiblymaybe.db` (backed up `possiblymaybe.db.bak-pre-r5-t6` before runs; read-only warehouse)  
**Vault:** `data/vault.db` (T1 durable; NHL Tier C burn)

## Verdicts (shadow / radar only — no paper)

| Arm | Verdict | Notes |
|---|---|---|
| NHL goals-EMA Tier A | **FAIL** | 1/6 PnL folds; meanClv=+0.434; n=2147 |
| NHL Tier C `2021-2022` | **PASS** (look-once report) | n=494 clv=+0.51 pnl=+27.9; vault burned |
| NFL EPA+roof Tier A | **FAIL** | 4/9 PnL folds; meanClv=−0.158; n=414 (2022 included) |
| NFL `nfl-wind` alone | **PASS** | 6/9 ΔPnL; meanClvW=+0.060 ≥ base; nW=443 |

PromotionPolicy: FAIL Tier A stays shadow marked FAIL. Wind PASS is still not money (prior NFL HOLD FAIL). No `paper-go` / settle from this agent.

## What landed

| Piece | Path |
|---|---|
| NHL pack unlock | `NhlSportPack` — `TierAReady=true`, folds 2015-16→2020-21, Tier C `2021-2022`, skip `2022-2023` |
| NHL backtest + Tier A | `NhlTotalsBacktest` / `NhlTotalsTierA` → CLI `tier-a-nhl` |
| NFL 2022 fold | `NflSportPack.TierAFolds` includes `2022` |
| Real prices | `NflTotalsBacktest` prefers non-AssumedJuice AmericanOdds |
| NFL EPA redo | `NflEpaTierA` → CLI `tier-a-nfl` |
| Wind arm | existing `tier-a-nfl-wind` re-run once |
| Tests | `SportPackTests.Nhl_Pack_TierAReady_*`, `T6TierATests` |

## NHL Tier A table (once)

Gates frozen on `NhlShadowConfig` (w=0.40, edge≥0.25, EV≥4%, α=0.18). Poisson over-prob on goals EMA lambdas. O+C from T2a SBRO (100% coverage).

| fold | n | clv | pnl | hit |
|---|---:|---:|---:|---:|
| 2015-2016 | 258 | +0.07 | −16.3 | 42.6% |
| 2016-2017 | 334 | +0.28 | +4.3 | 49.1% |
| 2017-2018 | 409 | +0.47 | −30.7 | 47.7% |
| 2018-2019 | 411 | +0.46 | −7.7 | 49.6% |
| 2019-2020 | 413 | +0.71 | −14.3 | 48.7% |
| 2020-2021 | 322 | +0.44 | −44.0 | 42.9% |

**SUMMARY** folds=6 winFolds=**1/6** n=2147 meanClv=+0.434 pnl=−108.7 → **TierA=FAIL**

### Tier C vault burn (T1 `--sim --final`)

- Day `20220115` → InferSeason `2021-2022` (sealed).
- Strategy hash `f65f8fe47c6bf74e` (`nhl-goals-ema`).
- First final: burn ok; score n=494 clv=+0.51 pnl=+27.9 → **TierC=PASS** (report only).
- Second process: **REFUSED** (`VaultAlreadyUsedException`) — durable across restarts.

## NFL EPA+roof Tier A (once; no wind)

| fold | n | clv | pnl | hit |
|---|---:|---:|---:|---:|
| 2015 | 17 | −9.44 | −1.4 | 47.1% |
| 2016 | 18 | +0.94 | −4.5 | 38.9% |
| 2017 | 36 | +0.54 | +2.9 | 55.6% |
| 2018 | 40 | +1.10 | +0.0 | 50.0% |
| 2019 | 38 | +1.12 | +6.6 | 60.5% |
| 2020 | 36 | −4.61 | −9.2 | 36.1% |
| 2021 | 64 | +1.72 | +0.5 | 51.6% |
| **2022** | **61** | **+0.45** | **−1.9** | 49.2% |
| 2023 | 104 | +0.00 | −5.7 | 49.0% |

**SUMMARY** folds=9 winFolds=**4/9** n=414 meanClv=−0.158 pnl=−12.8 → **TierA=FAIL**

## NFL `nfl-wind` alone (once; separate arm)

| fold | n0 | pnl0 | nW | pnlW | dPnL |
|---|---:|---:|---:|---:|---:|
| 2015 | 17 | −1.4 | 22 | +3.3 | +4.8 |
| 2016 | 18 | −4.5 | 24 | −6.5 | −2.1 |
| 2017 | 36 | +2.9 | 38 | +2.8 | −0.1 |
| 2018 | 40 | +0.0 | 43 | +2.7 | +2.7 |
| 2019 | 38 | +6.6 | 40 | +6.4 | −0.1 |
| 2020 | 36 | −9.2 | 44 | −2.2 | +7.0 |
| 2021 | 64 | +0.5 | 71 | +6.6 | +6.2 |
| **2022** | **61** | **−1.9** | **58** | **−0.8** | **+1.1** |
| 2023 | 104 | −5.7 | 103 | −2.8 | +2.9 |

**SUMMARY** folds=9 winFolds=**6/9** nW=443 meanClv0=−0.158 meanClvW=+0.060 → **TierA=PASS** vs no-wind baseline.

## Oregon

- NHL: Winterhawks stay excluded; Anaheim / Anaheim Ducks **not** excluded (T2a trap pinned in tests).
- NFL: no Oregon franchise; cardinal rule still on global name list.
- Never lifted `OregonExclusion`.

## Verify

```bash
cd ../pm-r5-t6
dotnet test PossiblyMaybe.Tests
dotnet run -c Release --project PossiblyMaybe.Cli -- tier-a-nhl   # second run: TierC=REFUSED
dotnet run -c Release --project PossiblyMaybe.Cli -- tier-a-nfl
dotnet run -c Release --project PossiblyMaybe.Cli -- tier-a-nfl-wind
```

**Tests:** 302 passed (includes T6 vault + pack assertions).
