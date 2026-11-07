# A1 proof: candidate store + variants-as-filters

**Branch:** `r4/a1` · **Worktree:** `../pm-r4-a1`

## What shipped

1. **Candidate store** (`CandidateEntry` in `data/variants.db`): one row per `(Sport, GameKey)` with model outputs, open/current line, DK over/under american, injury/roster/season-phase placeholders. Written by `VariantEvaluate.RecordAsync` and `variants-backfill` **before** shadow decisions.
2. **Variants as filters:** `VariantRegistry.Evaluate` reads a `SlateCandidate` (from live slate or `VariantLedger.ToSlate`). New family file + `variants-reeval` refreshes shadow rows without rebuilding models.
3. **Registry split** under `PossiblyMaybe.Domain/Variants/`:
   - `VariantCore.cs` — types + `Evaluate` + `VariantRegistry`
   - `ChampFamily.cs` — V0, V0L
   - `GuardsFamily.cs` — V1–V5
   - `LeagueFilterFamily.cs` — V-SA0  
   Each family `[ModuleInitializer]`-registers into `VariantRegistry` (ordered). Wave-B = one new file; no edit to other families.
4. **Backfill** covers every season present in warehouse for r3 leagues: NBA, MLS, LIGUE1, LIGAMX, SERIEA. Shadow settled PnL/CLV columns unchanged (additive Candidates table only).
5. **V5 juice:** when `UseRealJuice` and DK americans are on the candidate, Evaluate uses them (fallback −110 only if null/0).

## Verify V0 / V3 reproduce (bit-identical Evaluate)

```bash
cd ../pm-r4-a1
dotnet test --filter "FullyQualifiedName~Evaluate_V0_V3_Fixture_StableAfterSplit"
```

Fixture: NBA BOS vs MIA, line 220, model 232, σ 19, open=220, juice −110.  
Expect V0 and V3 both **PICK OVER**, same `AbsEdge` / `ModelProb` / `Ev`, american −110. Same numbers as pre-split `Variants.cs` Evaluate.

Full suite:

```bash
dotnet test
```

Also: `Filter_*` tests from r3 still assert V0/V1/V2/V3/V4/V-SA0 behavior.

## Verify candidate round-trip + re-eval (no model re-run)

```bash
dotnet test --filter "FullyQualifiedName~CandidateStore_RoundTrip"
```

`RecordAsync` → `CandidateEntries` row → `ToSlate` → V5 juice preserved → `ReEvalStoredAsync` rewrites shadow from store only.

CLI (after a backfill or daily shadow):

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- variants-reeval
```

## Verify V5 real DK juice

```bash
dotnet test --filter "FullyQualifiedName~Filter_V5_UsesRealDkJuice"
```

Candidate with `DkOverAmerican=-105`: V3 still −110; V5 AmericanOdds = −105 and higher EV.

## Adding a filter variant = one file

1. Add `PossiblyMaybe.Domain/Variants/MyFamily.cs` with variants +  
   `[ModuleInitializer] internal static void Init() => VariantRegistry.Register(order, ...);`  
   (use `Order` ≥ 100 so it sorts after the launch set).
2. `dotnet run … -- variants-reeval` — filters every stored candidate; **no** `variants-backfill` / model walk-forward.

## Daily run

Unchanged path: `pm-calculate` / slate → `DailySlate` emits candidates → `VariantEvaluate.RecordAsync` upserts Candidates then shadow. Target remains &lt; 5 min (candidate upsert is O(games), not O(history)).

## Not in A1

Wave-B families (absence, pooling, …), non-r3 leagues in backfill (NFL/MLB/… wait on C0/C*), season-phase tagging beyond the nullable column.
