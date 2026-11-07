# B2 proof: Uncertainty / roster (`+pooling`, `+roster-reset`)

**Branch:** `r4/b2` · **Worktree:** `../pm-r4-b2` · **Agent:** Auto/Composer

## What shipped

1. **`PossiblyMaybe.Domain/Variants/PoolingFamily.cs`** (Order 120) — self-registers:
   - `V-POOL` / `+pooling` — V0 stack + shrink model total (and λ) toward a sport league-mean when `SeasonPhase` ∈ `{opening-weeks, early-season, promoted, promoted-first-games}`.
   - `V-ROSTER` / `+roster-reset` — V0 stack + shrink toward league mean and ×1.25 NBA σ when `RosterFlag == true`.
2. **Immutable ids only** — no edits to V0–V5 / V-SA0 behavior. Optional `Variant.Pooling` / `Variant.RosterReset` (default false) + one `PoolingFamily.Apply` call in `Evaluate`.
3. **Weights:** early-season `w=0.5`, roster-reset `w=0.4` (toward model); NBA mean 224, soccer mean 2.70 (placeholder until candidates store live league means).

## Unit checks

```bash
cd ../pm-r4-b2
dotnet test --treenode-filter "/*/PossiblyMaybe.Tests/VariantsTests/*"
```

Expect:

| Test | Expectation |
|---|---|
| `Registry_LaunchSet_ImmutableIds` | ids include `V-POOL`, `V-ROSTER`; flags set |
| `Pooling_ShrinksOnOpeningPhase_NoopOtherwise` | opening-weeks AbsEdge &lt; V0; playoffs bit-identical to V0 |
| `RosterReset_ShrinksAndWidensSigma_WhenFlagged` | flagged AbsEdge &lt; V0 and σ×1.25; `RosterFlag=false` ≡ V0 |

## Tier A / live (next, not blocking register)

- **Tier A (pooling):** re-eval stored candidates once A5 (or pack) tags `opening-weeks` / `promoted*` on season openers + promoted clubs; compare CLV / PnL vs V0 on that subset only (EB was within ~1u full-season — hypothesis is the soft spot is the tagged subset).
- **Live shadow (roster-reset):** set `RosterFlag` on candidates after major trades / transfers; shadow settles automatically; no Tier A (live-only per checklist).

| Round 5 | Status |
|---|---|
| **T4a pooling fold table (once)** | Done 2026-11-07 — `research_notes/round5/t4a_proof.md` (`+pooling` on MLS / Ligue 1 / Liga MX / Serie A). All four **FAIL** (thin opener n or no Kelly majority) → Shadow-FAIL. No paper promotion. |

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- variants-reeval
```

## Not in B2

Filling `SeasonPhase` / `RosterFlag` from ESPN / transfer feeds (candidate emission); live league-mean column; stacking pooling onto V3 guards (add a new id if wanted).
