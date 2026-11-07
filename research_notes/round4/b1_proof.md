# B1 proof: Availability family (`+absence`, `+injury-guard`)

**Branch:** `r4/b1` · **Worktree:** `../pm-r4-b1`

## What shipped

1. **`PossiblyMaybe.Domain/Variants/AvailabilityFamily.cs`** (wave-B one-file family)
   - `+absence` — V0 stack, NBA-only, `AbsenceAdjust=true`; applies `SlateCandidate.AbsenceAdjPts` via reused `AbsenceBook.MissingPtsAdjustment`
   - `+injury-guard` — V0 stack, NBA/NFL/NCAAB/NCAAF, `InjuryGuard=true`; skips with `VariantSkip.Injury` when `InjuryFlag`
   - Live ESPN injuries helper (`ApplyEspnInjuryFlagsAsync` / `ParseInjuryReport`) — Out / Doubtful / Day-To-Day, or dated within 24h; team match via `TeamCodes`
2. **`VariantCore`** trailing flags only (`AbsenceAdjust`, `InjuryGuard`, `AbsenceAdjPts`) — **no edits to other families' ids**
3. Live slate / shadow calls `ApplyEspnInjuryFlagsAsync` before `VariantEvaluate.RecordAsync`

## Tier A — `+absence` (same-gate PaceEff vs PaceEffAbsence)

Reuse existing walk-forward `TotalsEdgeBacktest` + tip-time DNP CSV (`data/raw/nba_box/espn_player_box.csv`).

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- sweep-totals possiblymaybe.db --absence
```

Holdout season 2025, gates `w=0.5` `|edge|≥6` `EV≥4%` `absenceScale=0.5` (2026-11-07):

| Model | hold_n | CLV | PnL | Brier | LogLoss |
|---|---:|---:|---:|---:|---:|
| PaceEff | 61 | 2.66 | +17.3 | 0.2674 | 0.7371 |
| PaceEffAbsence | 223 | 1.69 | −7.3 | 0.3025 | 0.8609 |

`absenceGames=2450` mean adj `−9.08` pts — data path works. **Verdict: does not beat PaceEff** on promote rule (PnL and CLV both worse). Variant stays registered for shadow / wave radar; not a promote candidate from this holdout.

Unit path: `AvailabilityTests.Filter_Absence_AppliesAdjPts_VsV0` (synthetic −20 adj kills OVER edge vs V0).

## Live — `+injury-guard`

No free historical pre-game injury archive → **live-only**. Proof target (later): guarded-out bets' CLV &lt; 0.

```bash
dotnet test PossiblyMaybe.Tests -- --treenode-filter '/*/*AvailabilityTests/*'
```

- `Filter_InjuryGuard_SkipsWhenFlagged` / `_PassesWhenUnflagged`
- `ParseInjuryReport_FlagsOutWithin24h`
- `EspnInjuryFeed_Nba_SkipWhenEmpty` — **returns without fail** when feed empty or no material BOS/MIA injuries

## Verify registry

```bash
dotnet test PossiblyMaybe.Tests
```

Expect launch ids plus `+absence` / `+injury-guard`. Full suite green (174 tests on this branch).

## Not in B1

- Storing `AbsenceAdjPts` on `CandidateEntry` (re-eval from store still sees 0 until a later column)
- NFL / NCAA on daily slate (injury sports allow-list ready; live flag path runs when those candidates exist)
- Promote / paper-lane wiring for either variant
