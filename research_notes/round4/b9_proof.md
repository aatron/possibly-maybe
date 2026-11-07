# B9 proof: `inv-var-blend`

**Branch:** `r4/b9` · **Worktree:** `../pm-r4-b9` · **Priority:** low

## What shipped

1. `PossiblyMaybe.Domain/Variants/BlendFamily.cs` — registers immutable id `inv-var-blend` (`InvVarBlend: true`).
2. `MarketBlend` helpers in `TotalsMath.cs` (inverse-variance fusion, MoM σ from residual sums).
3. `VariantRegistry.Evaluate` uses locked sport σs when `InvVarBlend` is set (ignores candidate fixed `MarketWeight`).

Locked σ (last fold of prior Tier A, not re-estimated live):

| Sport | σ_model | σ_line | implied w_line |
|---|---:|---:|---:|
| NBA | 18.55 | 23.26 | ≈ 0.389 |
| Soccer (default) | 1.72 | 1.73 | ≈ 0.497 |

## Tier A (carried from book round 2)

Full fold tables: `research_notes/book_round2/exp_inv-var-blend-w.md`.

| League | PnLΔ>0 folds | Mean CLV champ vs inv | Verdict |
|---|---|---|---|
| NBA (14 folds, 2009–2022) | **7/14** | 10.46 vs 8.56 | FAIL |
| MLS (5 folds, 2020–2024) | **2/5** | 0.275 vs 0.310 (inv better CLV, worse PnL) | FAIL |

**Round-4 status:** register for shadow / wave radar completeness. **Do not promote.** Coin-flip close; no majority-fold PnL lift. N_trials for this family = 2 (NBA + MLS) from the prior run.

## Verify

```bash
cd ../pm-r4-b9
dotnet test
```

Expect: registry includes `inv-var-blend`; `Evaluate_InvVarBlend_UsesLockedSigmas` AbsEdge ≠ V0's 6.0 and matches locked-σ blend; `MarketBlend_inverse_variance_matches_weighted_mean` identities hold.

## Not in B9

Per-fold MoM σ on candidates, Tier A re-run CLI, soccer-league-specific locked σs, promotion ladder wiring.
