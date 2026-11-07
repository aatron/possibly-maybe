# R9 proof: day-stepping engine shared with live

**Date:** 2026-11-07  
**Owns:** `PossiblyMaybe.Cli/Replay/DayEngine.cs`, `engine-day` CLI, `DayEngineTests`.

## What shipped

| Piece | Behavior |
|---|---|
| `DayEngine.Run` | Pure: reveal (`view.FinalGames`) → `Learn` → `Decide` → stub league report |
| `DayEngine.RunDayAsync` | Warehouse: `AsOfView` at `PointInTime.SlateAsOf` + same DailySlate candidate builders as live shadow |
| `engine-day` CLI | `engine-day [yyyymmdd] [strategy] [db]` — prints GOs / skips; **no** paper-go / settle |
| Report stub | Per-league `n` (picks); CLV / flat PnL null until settle closes wire |

Oregon exclusion stays inside `VariantRegistry.Evaluate` (V0 path) — never lifted.

## Proof (`DayEngineTests`)

| Claim | Result |
|---|---|
| Synthetic live slate (NBA pick / NBA skip / MLS / POR excluded): engine V0 decisions == `Evaluate(V0)` field-for-field | PASS |
| Engine as-of == `PointInTime.SlateAsOf` (same cutoff DailySlate ratings use) | PASS |

```bash
dotnet build PossiblyMaybe.Tests -c Debug
dotnet PossiblyMaybe.Tests/bin/Debug/net10.0/PossiblyMaybe.Tests.dll --disable-logo
# 274 succeeded (incl. 2 DayEngineTests)
```

## Limits / risks

- shortcut: SlateAsOf = UTC midnight of the day (shared with DailySlate), not wall-clock EOD / per-tipoff locks; upgrade when live locks pre-tip.
- shortcut: shared post-Decide layers (venue pricing, staking lanes, bankroll OUT, lock, same-book settle) not a separate pass yet — Oregon + EV already inside Evaluate(V0); upgrade when R9 full pipeline lands.
- shortcut: CLV / PnL / checkpoint stages stubbed on `DayLeagueReportStub`; upgrade with R5 + settle closes.
- shortcut: `pm-calculate` still calls slate / variants-shadow directly; rewrite deferred — `engine-day` is the wired entry.
- shortcut: multi-day reveal→settle walk and season runtime target not exercised here.
