# C6-epl proof: EPL sport pack (FD O→C + shadow)

**Branch:** `r4/c6-epl`  
**Date:** 2026-11-07  
**Census:** A4 #1 — football-data / Kaggle Premier League O→C, Kalshi `KXEPLTOTAL`, Crypto.com `kind=EPL`, no Oregon clubs.

## Holdout lock (BEFORE any gate tuning)

| | Season |
|---|---|
| Tier A train max | `2024-2025` |
| **Tier C look-once** | **`2025-2026`** |
| Skip after holdout | _(none in dump yet)_ |

Pre-registered Tier A fold labels: `2018-2019` … `2024-2025`.  
**Do not** score `2025-2026` until gates are fixed from Tier A / train-only sweeps. Untuned live gates = goals EMA α=0.18, w=0.40, \|exp−line\|≥0.25, EV≥4% (same starting band as other FD soccer packs).

## What landed

| Piece | Path |
|---|---|
| Champ / shadow gates | `PossiblyMaybe.Domain/EplChampConfig.cs` |
| Pack | `PossiblyMaybe.Domain/SportPacks/EplSportPack.cs` |
| Registry | `SportPackRegistry` + `SportPackRegistry.Epl` |
| Aliases | `SportTeams.TryEpl` / `ResolveSoccer("EPL", …)` |
| Import | existing `import-soccer EPL premier_league` (FD/Kaggle CSV) |
| Backtest | existing `sweep-soccer EPL 2024-2025` (Tier A); Tier C = `2025-2026` once |
| Live | `slate-soccer EPL` → **shadow only** (no paper-go); `refresh-results` + tracker + Kalshi/Crypto wired |
| Test | `SportPackTests.Epl_Pack_Holdout_Locked_Before_Tuning` |

## Oregon

No EPL Oregon clubs. `OregonNames: []` but `OregonExclusion.IsExcluded` still runs on every candidate / backtest pick (cardinal rule). Test asserts Timbers/Ducks excluded under sport `EPL` and Arsenal not excluded.

## Not done here

- No w/edge/α sweep or Tier C score (holdout locked first).
- Not promoted to `pm-calculate` paper SOCCER_PASS.
- Poisson CDF not enabled for EPL (stays logistic until a KEEP proof).
