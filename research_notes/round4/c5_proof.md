# C5 proof: La Liga + EFL shadow

**Branch:** `r4/c5`  
**Date:** 2026-11-07  
**Status:** FAIL as champs (unchanged) — still on for shadow / wave radar.

## What landed

| Piece | Path |
|---|---|
| Shadow gates | `LaLigaShadowConfig.cs`, `EflShadowConfig.cs` (goals EMA α=0.18, w=0.40, edge≥0.25, EV≥4%) |
| Packs | `SportPacks/LaLigaSportPack.cs`, `SportPacks/EflSportPack.cs` |
| Registry | `SportPackRegistry` (+ `LaLiga`, `Efl`) |
| Aliases | `SportTeams.TryLaLiga` / `TryEfl` + `ResolveSoccer` |
| Live slate | `DailySlate.SoccerEspnPaths` → `esp.1` / `eng.2`; shadow-only print (no `paper-go`) |
| Backfill / shutoff | `VariantBackfill`, `ShutoffProof` include LALIGA + EFL |
| Settle data | `ResultsRefresh.Leagues` + ESPN paths |
| Tier A CLI | `tier-a-shadow-soccer [SPORT\|all] [db]` → `SoccerShadowTierA` |
| Test | `SportPackTests.LaLiga_And_Efl_Packs_Shadow_Gates_And_Oregon` |

Oregon: no clubs expected; packs call `OregonNamesExcluded()` (vacuous) and tests still hit `OregonExclusion` for Timbers / Ducks on both league ids.

## Tier A fold summary (refresh 2026-11-07)

Command:

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- tier-a-shadow-soccer
```

Gates frozen on shadow configs. Each pack fold = holdout season; burned Tier C = `2024-2025`.

### LALIGA (`LaLigaGoalsEma`)

| fold | n | CLV | PnL u | hit |
|---|---:|---:|---:|---:|
| 2018-2019 | 72 | +0.115 | +3.5 | 54.2% |
| 2019-2020 | 75 | +0.087 | +13.1 | 57.3% |
| 2020-2021 | 63 | +0.087 | −4.2 | 38.1% |
| 2021-2022 | 55 | +0.068 | −7.1 | 41.8% |
| 2022-2023 | 65 | +0.146 | −9.5 | 43.1% |
| 2023-2024 | 76 | −0.013 | −15.5 | 36.8% |
| **burned 2024-2025** | 62 | −0.036 | −11.3 | 40.3% |

SUMMARY: foldPass=2/6 n=406 meanClv=+0.080 pnl=−19.8 → **VERDICT FAIL** (shadow stays on).

### EFL (`EflGoalsEma`)

| fold | n | CLV | PnL u | hit |
|---|---:|---:|---:|---:|
| 2018-2019 | 76 | +0.191 | +2.3 | 53.9% |
| 2019-2020 | 76 | +0.092 | −13.0 | 43.4% |
| 2020-2021 | 57 | +0.114 | +3.2 | 54.4% |
| 2021-2022 | 55 | +0.105 | −1.5 | 50.9% |
| 2022-2023 | 63 | +0.008 | +11.5 | 61.9% |
| 2023-2024 | 78 | +0.119 | +0.3 | 52.6% |
| **burned 2024-2025** | 54 | +0.023 | −0.5 | 50.0% |

SUMMARY: foldPass=4/6 n=405 meanClv=+0.107 pnl=+2.6; burned PnL≤0 → **VERDICT FAIL** (shadow stays on).

## Live shadow

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- slate-soccer LALIGA 20261101
dotnet run -c Release --project PossiblyMaybe.Cli -- slate-soccer EFL 20261031
# also covered by: variants-shadow (iterates SoccerEspnPaths)
```

Observed: LALIGA 4 candidates → 32 variant×game rows; EFL 11 → 88 rows; both labeled `SHADOW-ONLY` (no `paper-go`).  
Warehouse finals for these sports stop at ~2026-05 → `STALE DATA` until:

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- refresh-results
```

(now includes LALIGA + EFL). After refresh, stale guard lifts and open shadow picks settle via `variants-settle` / `variants-shadow`.

## Not done

- Paper promotion (FAIL).
- Full cold `refresh-results` from May→Nov in this session (rate-limit heavy); documented re-run above.
- Kalshi series for LALIGA/EFL (none wired; shadow uses ESPN/−110).
