# B5 proof: refs-nba (referee totals)

**Branch:** `r4/b5` · **Worktree:** `../pm-r4-b5`

## What shipped

| Piece | Path |
|---|---|
| Totals residual on `RefEffects` | `PossiblyMaybe.Domain/RefEffects.cs` — `ObserveTotal` / `CrewTotalsBias` / `CrewPaceBias` / `CrewFoulPaceResidual`; Donaghy still excluded via `excludeFromFit` |
| Variant family | `PossiblyMaybe.Domain/Variants/RefsFamily.cs` — immutable id `refs-nba` (NBA-only, `UseRefTotals`) |
| Evaluate hook | `Variant.UseRefTotals` + `SlateCandidate.RefTotalsResidual` → added in `MarketBlend` |
| Tests | `RefEffectsTests` (high-total crew + Donaghy totals exclude); `VariantsTests` (registry + residual flips OVER + non-NBA skip) |

## Signal

Walk-forward crew mean **final total** vs league mean (points). Optional **possessions** (`PaceEfficiencyBook.Possessions` from `GameBox`, includes 0.44·FTA) stored as pace bias for diagnostics.

`shortcut: total-pts residual stands in for foul/pace (no PF column on GameBox); upgrade when PF rates land.`  
`shortcut: RefTotalsResidual not yet a CandidateEntry column — slate/backtest must set it on SlateCandidate; when 0, refs-nba ≈ NBA-only V0.`

## Verify

```bash
cd ../pm-r4-b5
dotnet test PossiblyMaybe.Tests/PossiblyMaybe.Tests.csproj
```

Expect registry includes `refs-nba` with `UseRefTotals`; Donaghy-flagged `ObserveTotal` leaves bias at 0; +12 residual on a flat model/line candidate → `refs-nba` PICK OVER while V0 skips.

## Tier A folds

Not re-run here (needs warehouse join of `RefereeAssignments` + `GameBoxes` + TOTAL open/close). Unit residual learning is the gate for merge; full fold CLV vs V0 is a follow-up once residual is filled in `VariantBackfill` / `DailySlate`.

## Not in B5

Persisting residual on `CandidateEntry`; live crew lookup in `DailySlate`; soccer refs.
