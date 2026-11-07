# R7 proof: point-in-time `AsOfView(T)`

**Date:** 2026-11-07  
**Owns:** `PossiblyMaybe.Domain/AsOfView.cs`, `PossiblyMaybe.Cli/Replay/AsOfViewFactory.cs`, `AsOfViewTests`.

## What shipped

| Piece | Behavior |
|---|---|
| `FinalGameRow` / `OddsAsOfRow` / `InjuryRosterRow` | Domain DTOs — no EF entities on the strategy surface |
| `AsOfView` (sealed) | Tipoff &lt; T finals, CapturedAt ≤ T odds / injuries, optional `SeasonPhase`; defensive re-filter in ctor; `Watermark()` fingerprint |
| `AsOfViewFactory` | `FromSnapshots` (pure) + `FromDbAsync` (DB stays in factory only) |
| Reuses R1 | Factory uses `PointInTime.AsOf` for tipoff cutoff on scored games |

Strategies (R8) will take `AsOfView` only — Domain references no EF / `AppDbContext`.

## Leak proofs (`AsOfViewTests`)

| Claim | Result |
|---|---|
| (a) Same Decide fingerprint with vs without later finals / odds / injuries | PASS |
| (a) Same via two SQLite warehouses (with vs without later rows) | PASS |
| (b) Cheater probe with only future rows sees empty lists | PASS |
| (b) `AsOfView` sealed; public surface has no DbContext / EF types; Domain refs no EF | PASS |

```bash
dotnet build PossiblyMaybe.Tests -c Debug
dotnet PossiblyMaybe.Tests/bin/Debug/net10.0/PossiblyMaybe.Tests.dll --disable-logo
# 264 succeeded (incl. 4 AsOfViewTests)
```

## Limits / risks

- shortcut: no injury/roster warehouse table yet — injuries stay empty from `FromDbAsync` until a store lands; upgrade with B1 live feed.
- shortcut: loads all scored games then filters in memory; upgrade to tipoff-range SQL when R9 walks seasons.
- Day lock / reveal sequencing is R9's job; this view only enforces the T cutoff once constructed.
