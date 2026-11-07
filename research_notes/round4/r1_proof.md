# R1 proof: as-of cutoff in model builders

**Date:** 2026-11-07  
**Owns:** `PossiblyMaybe.Domain/PointInTime.cs`, `Cli/Paper/DailySlate.cs` builders, Serie A window relative to as-of.

## What shipped

| Piece | Behavior |
|---|---|
| `PointInTime.AsOf` | Keeps items with `tipoff < asOf`; null asOf ⇒ passthrough |
| `PointInTime.SlateAsOf(yyyymmdd)` | UTC midnight of slate day |
| DailySlate builders | NBA / NCAAB / soccer / NHL train only on tipoffs before slate as-of |
| Serie A 3y window | `(asOf ?? UtcNow).AddYears(-3)` — reproducible vs wall-clock |
| Backfill / ShutoffProof | Serie A cutoff from last warehouse tipoff (not `UtcNow`) |

## Leak proof (`PointInTimeTests`)

| Claim | Result |
|---|---|
| AsOf drops tipoff ≥ cutoff | PASS |
| ScoringBook ratings identical with vs without a future final when asOf precedes it | PASS |
| DixonColes + Elo same under asOf with future final present | PASS |

```bash
dotnet build PossiblyMaybe.Tests -c Debug
dotnet PossiblyMaybe.Tests/bin/Debug/net10.0/PossiblyMaybe.Tests.dll --disable-logo
# 260 succeeded (incl. PointInTimeTests)
```

## Limits / risks

- shortcut: walk-forward backfill still does not rebuild a rolling 3y Dixon–Coles book per game (O(n²)); live slate rebuilds from asOf−3y. Upgrade if historical Serie A DC must match live day-by-day.
- PaceEfficiency residual σ uses the same asOf game list; odds snapshots are not tipoff-filtered here (R7 AsOfView).
