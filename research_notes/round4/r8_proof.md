# R8 proof: IStrategy plug-in + V0 port

**Date:** 2026-11-07  
**Owns:** `PossiblyMaybe.Domain/Strategies/StrategyCore.cs`, `Strategies/V0/V0TotalsStrategy.cs`, `StrategyTests`.

## What shipped

| Piece | Behavior |
|---|---|
| `IStrategy` | `Learn(newResults)`, `Decide(slate, view) → StrategyDecision[]` with P / EV |
| `StrategySlate` | Pre-built `SlateCandidate` rows (no DB on the strategy) |
| `StrategyRegistry` | ModuleInitializer auto-register from `Strategies/<name>/` |
| `V0TotalsStrategy` + `V0TotalsParams` | Calls `VariantRegistry.Evaluate(V0)` per candidate |

Oregon exclusion stays inside Evaluate — never lifted.

## Proof (`StrategyTests`)

| Claim | Result |
|---|---|
| Registry exposes auto-registered `V0` | PASS |
| NBA A1 fixture: strategy decision == Evaluate(V0) (Pick/Side/Line/odds/P/EV/edge/skip/assumed) | PASS |
| MLS fixture: same unit match | PASS |
| Portland Trail Blazers → EXCLUDED matches Evaluate | PASS |
| Learn no-op; multi-candidate order preserved | PASS |

```bash
dotnet build PossiblyMaybe.Tests -c Debug
dotnet PossiblyMaybe.Tests/bin/Debug/net10.0/PossiblyMaybe.Tests.dll --disable-logo
# 272 succeeded (incl. 5 StrategyTests)
```

## Limits / risks

- shortcut: V0 ignores AsOfView contents — candidates stay pre-built by DailySlate; upgrade when R9 builds slate rows from the view.
- shortcut: Learn is a no-op for V0 (ratings live on candidates); upgrade for online books in R9.
- shortcut: only V0 ported — wave-B families stay VariantRegistry filters until rewritten as strategies.
- Tier A/B season numbers are Evaluate-equivalent via the shared path, not a re-run of full backtest folds here.
