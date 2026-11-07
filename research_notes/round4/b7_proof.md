# B7 proof: calibration / filters

**Branch:** `r4/b7` · **Worktree:** `../pm-r4-b7`

## What shipped

`PossiblyMaybe.Domain/Variants/CalibrationFamily.cs` (Order 170) + optional fields on `Variant`:

| Id | Label | Behavior |
|---|---|---|
| `V-FC-NBA` … `V-FC-SERIEA` | `+fitted-curve-*` | League-only; `FittedCurve` → Normal P(over) with locked residual σ |
| `V-SOC-STRICT` | `soccer-strict` | Soccer leagues; \|edge\|≥0.5; EV 4–20% |
| `V-NBA-STRICT` | `nba-strict` | NBA; \|edge\|≥8; EV 4–20% |
| `V-EV-04-10` | `ev-band-4-10` | All leagues; EV ∈ [4%, 10%] |
| `V-EV-10-20` | `ev-band-10-20` | All leagues; EV ∈ [10%, 20%] |
| `V-EV-04-20` | `ev-band-4-20` | All leagues; EV ∈ [4%, 20%] |

`Evaluate` honors `MinAbsEdge` / `MinEv` overrides and `FittedCurve` before champ OverProb.

## σ locked before scoring (reliability prior)

Pre-holdout residual sd from `eval_ml_general` / `ExpandingResidualSigma.FallbackSigma` — **not** re-fit on look-once:

| League | σ |
|---|---|
| NBA | 22.8 |
| MLS | 1.65 |
| Ligue 1 | 1.68 |
| Liga MX | 1.70 |
| Serie A | 1.62 |

## Verify (unit)

```bash
cd ../pm-r4-b7
dotnet test PossiblyMaybe.Tests/PossiblyMaybe.Tests.csproj
```

Expect: registry includes B7 ids; fitted NBA P(over) uses σ=22.8 (calmer than candidate σ=10); soccer-strict NO_EDGE below 0.5; nba-strict NO_EDGE at champ edge=6; EV-band 10–20 LOW_EV on thin edges.

## Tier A (walk-forward; run after merge / with warehouse)

Per variant vs V0 on same tickets:

1. Folds: NBA seasons through 2023–24 train / 2024–25 hold; soccer champ train/hold splits per `*ChampConfig`.
2. Metrics: Brier + log-loss reliability table (deciles of P(over)); Kelly-lane PnL + mean CLV; N bets.
3. Keep only if CLV ≥ V0 (or Brier improves **and** Kelly PnL not worse on same tickets — protocol §6).
4. Print `N_trials` = number of B7 cells scored under BH with the rest of wave B.

**`soccer-strict` fold table (Round 5 T4c):** scored once 2026-11-07 — [`../round5/t4c_proof.md`](../round5/t4c_proof.md). Verdict **FAIL** → Shadow marked FAIL (n=31&lt;40; mean CLV 0.076 &lt; V0 0.175). Remaining B7 cells (`+fitted-curve-*`, `nba-strict`, EV-bands) → Round 5 T4b.

Reliability table template (fill after Tier A run):

| Variant | Decile | Mean P | Hit rate | n |
|---|---|---|---|---|
| V-FC-NBA | … | | | |

## Not in B7

Live OuPlatt refit (already FAIL), conformal (B8), backfill CLI changes, PROGRESS merge to main.
