# C0 proof: sport-pack template + MLS identical V0

**Branch:** `r4/c0`  
**Date:** 2026-11-07

## What landed

| Piece | Path |
|---|---|
| Template | `PossiblyMaybe.Domain/SportPacks/SportPack.cs` |
| Registry | `PossiblyMaybe.Domain/SportPacks/SportPackRegistry.cs` |
| MLS instance | `PossiblyMaybe.Domain/SportPacks/MlsSportPack.cs` |
| Test | `PossiblyMaybe.Tests/SportPackTests.cs` |

Pack fields: league id, results source, odds sources (Kaggle / archive.org / SBRO / tracker), rating-book kind, Oregon name list + `ResolveAlias`, season phases (placeholders), Tier A folds, train max / Tier C holdout / skip season, and V0 champ gates.

## Identical V0 (how verified)

1. **Single source of champ numbers.** `MlsSportPack.Instance` reads `Market`, `Model`, `MarketWeight`, `MinEv`, `MinAbsTotalEdge`, `EmaAlpha`, `BankrollSeed` from existing `MlsChampConfig` consts — no duplicated magic numbers.
2. **Unit proof.** `SportPackTests.Mls_Pack_Matches_ChampConfig_And_Oregon` asserts pack gates equal those consts, holdout lock is train≤2024 / holdout 2025 / skip 2026, alias hook resolves Timbers→POR, and Oregon exclusion still fires for Timbers / Thorns / Fire (Chicago Fire not excluded).
3. **Code paths that consume the pack** still see the same values:
   - `DailySlate` MLS book/gates → `SportPackRegistry.Mls` (same numbers as before via consts).
   - `MlsTotalsBacktest.HoldoutSeason` → `MlsSportPack.Instance.TierCHoldoutSeason` (`"2025"`).
4. **Unchanged.** NBA `ChampConfig` / slate paths; other soccer champ configs; `SoccerOverProbConfig` (MLS stays logistic). No C1–C7 packs.

## Not re-run

Full `sweep-mls` / holdout backtest against a live DB was not re-run here; identity is structural (same consts → same gates / α / EV). Re-run `dotnet run --project PossiblyMaybe.Cli -- sweep-mls` on a filled DB if you want numeric CLV/PnL echo.
