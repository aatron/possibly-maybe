# T4b proof: NBA Tier A once (B5 refs + B7 calibration)

**Date:** 2026-11-07  
**Branch:** `r5/t4b` · **Worktree:** `../pm-r5-t4b`  
**Owns:** `PossiblyMaybe.Cli/Backtest/NbaCalibrationTierA.cs`, CLI `tier-a-t4b`, `NbaCalibrationTierATests`, durable trial rows in `data/vault.db`.

## Spec

Run deferred Round 4 Tier A fold tables **once** for the NBA family against V0 on the same O→C tickets:

| Source | Variant ids |
|---|---|
| B5 | `refs-nba` |
| B7 | `V-FC-NBA` (+fitted-curve-nba), `V-NBA-STRICT` (nba-strict), `V-EV-04-10`, `V-EV-10-20`, `V-EV-04-20` |

Keep rule (pre-declared, B8-style): mean fold CLV ≥ V0 **and** n≥40 → **PASS → Shadow only** (`PromotionPolicy.EnterLiveShadow`). Else **FAIL marked** (stays shadow). No paper promotion. No re-tune after seeing numbers. OregonExclusion unchanged.

## Method

- Folds: `2015`…`2022` (same as `NbaNormalSigmaTierA`; holdout `2025` unused for selection).
- Single walk-forward: PaceEff book + expanding residual σ + `RefEffects.ObserveTotal` (Donaghy excluded).
- Ticket filter: TOTAL open+close in [170,280], \|O−C\|≥0.25; Oregon games skipped inside `VariantRegistry.Evaluate`.
- Same candidate per game for every arm; variants only change residual / σ / gates.
- Each variant logged once to durable `TrialLedger` (`N_trials=6`).

```bash
# DB backup before vault write
cp -a ../possibly-maybe/possiblymaybe.db ../possibly-maybe/possiblymaybe.db.bak-pre-r5-t4b
dotnet run --project PossiblyMaybe.Cli -c Release -- tier-a-t4b ../possibly-maybe/possiblymaybe.db
dotnet test PossiblyMaybe.Tests -- --treenode-filter '/*/PossiblyMaybe.Tests/NbaCalibrationTierATests/*'
```

## Fold table (mean CLV / flat PnL / n)

| Variant | meanClv V0 | meanClv | ΔCLV | pnl V0 | pnl | n V0 | n | folds CLV≥V0 | mean Brier V0 | mean Brier | Verdict |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| refs-nba | 0.823 | 0.898 | +0.075 | +21.0 | −0.6 | 233 | 671 | 5/8 | 0.2517 | 0.2527 | **PASS → Shadow** |
| V-FC-NBA | 0.823 | 0.823 | 0.000 | +21.0 | +21.0 | 233 | 233 | 8/8 | 0.2517 | 0.2508 | **PASS → Shadow** |
| V-NBA-STRICT | 0.823 | 0.000 | −0.823 | +21.0 | 0.0 | 233 | 0 | 1/8 | 0.2517 | 0.2517 | **FAIL** |
| V-EV-04-10 | 0.823 | 0.000 | −0.823 | +21.0 | 0.0 | 233 | 0 | 1/8 | 0.2517 | 0.2517 | **FAIL** |
| V-EV-10-20 | 0.823 | 0.000 | −0.823 | +21.0 | 0.0 | 233 | 0 | 1/8 | 0.2517 | 0.2517 | **FAIL** |
| V-EV-04-20 | 0.823 | 0.000 | −0.823 | +21.0 | 0.0 | 233 | 0 | 1/8 | 0.2517 | 0.2517 | **FAIL** |

Trial footer: `N_trials=6 DSR=0.000 BH_q=0.10 (adj over 6 trials)` → `data/vault.db`.

## Notes

- **refs-nba:** more tickets (crew residual moves model); CLV/bet clears V0; flat PnL does not — Shadow only, no paper.
- **V-FC-NBA:** locked σ=22.8 matches V0 bet set on these folds; Brier/log-loss improve slightly (0.2508 / 0.6947 vs 0.2517 / 0.6966). PASS on CLV≥V0.
- **nba-strict / EV-bands:** zero bets on these folds — \|edge\|≥8 never clears, and V0-eligible EVs sit above the EV-band caps (Normalσ + edge≥6 → EV often >20%). FAIL marked; no re-tune.

## Tests

`NbaCalibrationTierATests`: registry ids, keep→Shadow never paper, durable TrialLedger reload = 6, fold-2019 smoke when DB present.

## Round-4 pointers

- `research_notes/round4/b5_proof.md` → this file  
- `research_notes/round4/b7_proof.md` → this file  

## Not in T4b

Paper promotion, soccer-strict / conformal / kalman (T4c), re-tuning gates after FAIL.
