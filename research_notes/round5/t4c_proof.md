# T4c proof: Tier A once — B8 + soccer-strict

**Date:** 2026-11-07  
**Branch:** `r5/t4c` · **Worktree:** `../pm-r5-t4c`  
**CLI:** `dotnet run --project PossiblyMaybe.Cli -c Release -- tier-a-t4c`  
**Warehouse:** `../possibly-maybe/possiblymaybe.db`  
**TrialLedger:** `data/vault.db` (append-only; ΔN_trials=3)

Round-4 registration: [`b8_proof.md`](../round4/b8_proof.md), [`b7_proof.md`](../round4/b7_proof.md).

## What shipped

| Piece | Role |
|---|---|
| `HigherRiskTierA` | Reused B8 walk (`tier-a-b8`); now logs durable trials + PASS→Shadow |
| `SoccerStrictTierA` | B7 `soccer-strict` vs league V0 on the same fold tickets |
| `TierAShadowVerdict` | Keep = mean fold CLV ≥ V0 and n≥40; Shadow only |
| `T4cTierA` / `tier-a-t4c` | One-shot orchestrator for both families |
| `T4cTierATests` | Keep rule, durable ledger, soccer smoke, Oregon still on |

No paper-go / settle. OregonExclusion untouched. No Tier C. No re-tune after seeing numbers.

## Keep rule (pre-declared)

Mean fold CLV ≥ V0 **and** total bets ≥ 40. PASS → `LadderStage.Shadow` (radar may later decide paper). FAIL → stays shadow, marked FAIL. **No paper promotion from this table.**

## B8 fold table (NBA PaceEff vs V0; folds 2015–2022)

Gates: w=0.5 \|edge\|≥6 EV≥4%. Conformal α=0.1. Kalman steady gain ≈0.15.

| fold | nV0 | clvV0 | pnlV0 | nCf | clvCf | pnlCf | nKal | clvKal | pnlKal |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 2015 | 14 | 0.79 | 1.3 | 0 | 0.00 | 0.0 | 14 | 0.79 | 1.3 |
| 2016 | 22 | 0.80 | 0.9 | 0 | 0.00 | 0.0 | 22 | 0.80 | 0.9 |
| 2017 | 18 | 1.61 | 4.9 | 0 | 0.00 | 0.0 | 18 | 1.61 | 4.9 |
| 2018 | 20 | −0.15 | 7.7 | 0 | 0.00 | 0.0 | 20 | −0.15 | 7.7 |
| 2019 | 48 | 0.88 | −5.0 | 0 | 0.00 | 0.0 | 48 | 0.88 | −5.0 |
| 2020 | 29 | 0.38 | 9.2 | 0 | 0.00 | 0.0 | 29 | 0.38 | 9.2 |
| 2021 | 29 | 0.97 | 7.3 | 0 | 0.00 | 0.0 | 29 | 0.97 | 7.3 |
| 2022 | 53 | 1.32 | −5.3 | 0 | 0.00 | 0.0 | 53 | 1.32 | −5.3 |

| Metric | V0 | +conformal | kalman |
|---|---:|---:|---:|
| Mean fold CLV | 0.823 | 0.000 | 0.823 |
| Sum flat PnL | 21.0 | 0.0 | 21.0 |
| Bets total | 233 | 0 | 233 |
| PnL>V0 folds | — | 2/8 | 0/8 |

## B7 soccer-strict (MLS / Ligue 1 / Liga MX / Serie A vs champ V0)

Strict: \|edge\|≥0.5, EV ∈ [4%, 20%]. Same walk / open+close tickets as V0.

| Metric | V0 | soccer-strict |
|---|---:|---:|
| Mean fold CLV | 0.175 | 0.076 |
| Sum flat PnL | 43.7 | 4.4 |
| Bets total | 1926 | 31 |
| PnL>V0 folds | — | 11/24 |

Strict tickets were almost all Ligue 1 (24) + Serie A (7); MLS and Liga MX produced **zero** strict GOs (edge floor 0.5 rarely clears after w=0.4 blend). Per-fold CLV/PnL/log-loss/Brier printed in the CLI log (`/tmp/t4c_run.txt` this session).

## Verdicts

| Variant | Verdict | Stage |
|---|---|---|
| `+conformal` | **FAIL** (n=0; abstains every GO) | Shadow marked FAIL |
| `kalman` | **PASS** (CLV = V0, n=233≥40) | Shadow (radar decides paper) |
| `soccer-strict` | **FAIL** (n=31&lt;40; mean CLV &lt; V0) | Shadow marked FAIL |

`N_trials=3` DSR=0.197 BH_q=0.10 (adj over 3 trials).

## Tests

```bash
dotnet test PossiblyMaybe.Tests -c Debug -- --treenode-filter '/*/PossiblyMaybe.Tests/T4cTierATests/*'
dotnet test PossiblyMaybe.Tests
```

Expect: keep-rule + durable TrialLedger round-trip; soccer-strict fold n ≤ V0 on same games; Oregon NBA/MLS still excluded; suite green.

## Limits

- Kalman matched V0 ticket-for-ticket on these folds (same n/CLV/PnL) — PASS under ≥, not a claim of independent edge.
- Conformal half-width never excluded the line on champ GOs → empty filter; FAIL is correct, not a tuning invite.
- soccer-strict n&lt;40 overall; do not promote. No re-run after seeing Tier B/C.
