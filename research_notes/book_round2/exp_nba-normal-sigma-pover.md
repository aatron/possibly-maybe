# Experiment: `nba-normal-sigma-pover`

**Branch:** `exp/nba-normal-sigma-pover`  
**Date:** 2026-10-28  
**Hypothesis:** Replace NBA logistic `OverProb(sharpness=0.25)` with `Φ((exp−line)/σ)` where σ = expanding train RMSE of `(actualTotal − blendedExp)` (0 free params; fallback 22.8 until n≥50). Fixes overconfident Kelly (logistic P≈0.82 at 6pt edge vs Φ(6/22.8)≈0.60).

**Code:** `TotalsMath.NormalOverProb` + `ExpandingResidualSigma`; `TotalsModelKind.PaceEffNormal`; Tier A CLI `sweep-totals [db] --normal-sigma`; wired in `TotalsEdgeBacktest`, `DailySlate.RunNbaAsync`, `StakeLaneHoldoutSim.CollectNbaAsync`, `advise-ou`.

**N_trials:** 1 (locked expanding RMSE; no grid). AssumedJuice −110 when open American=0.

**Safety:** library=data; no library exec; no Tier C; no `paper.db`; no bets.

## Tier A fold table (pre-holdout; selection)

Gates: w=0.5, |exp−line|≥6, EV≥4%. Folds pre-declared: 2015–2022. L=logistic champ, N=Normal σ.

| fold | nL | clvL | pnlL | kelly¼ L | Brier L | nN | clvN | pnlN | kelly¼ N | Brier N | end σ | Δpnl |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 2015 | 16 | 13.22 | 1.2 | 0.242 | 0.2637 | 16 | 13.22 | 1.2 | 0.223 | 0.2507 | 19.56 | 0.0 |
| 2016 | 22 | 0.80 | 0.9 | 0.048 | 0.2685 | 22 | 0.80 | 0.9 | −0.008 | 0.2526 | 19.56 | 0.0 |
| 2017 | 19 | 94.32 | 5.8 | 1.082 | 0.2678 | 19 | 94.32 | 5.8 | 0.534 | 0.2513 | 19.56 | 0.0 |
| 2018 | 20 | −0.15 | 7.7 | 1.367 | 0.2637 | 20 | −0.15 | 7.7 | 0.494 | 0.2501 | 19.56 | 0.0 |
| 2019 | 43 | 0.79 | −7.6 | −1.376 | 0.2704 | 43 | 0.79 | −7.6 | −0.519 | 0.2523 | 19.56 | 0.0 |
| 2020 | 30 | 0.38 | 10.1 | 1.783 | 0.2684 | 30 | 0.38 | 10.1 | 0.649 | 0.2513 | 19.56 | 0.0 |
| 2021 | 33 | 26.80 | 10.9 | 2.286 | 0.2616 | 33 | 26.80 | 10.9 | 1.462 | 0.2490 | 19.56 | 0.0 |
| 2022 | 6 | 74.25 | 3.5 | 0.760 | 0.2618 | 6 | 74.25 | 3.5 | 0.566 | 0.2503 | 19.56 | 0.0 |

- Mean fold CLV: L=26.301, N=26.301 (≥ champ: yes)
- Sum flat PnL: L=32.5, N=32.5 (Δ=0; folds with Δpnl>0: **0/8**)
- Sum Kelly¼: L=6.192, N=3.402 (Δ=−2.791)
- Bets: 189 / 189 (n≥40: yes)
- Expanding σ used ≈ **19.56** pts (stable across late folds)

**Tier A:** **FAIL** (majority ΔPnL>0 required; 0/8). Same tickets as champ at these gates (Φ(6/19.56) still clears EV≥4%), so flat CLV/PnL tied; better Brier/LL but smaller Kelly on the same winners → worse Kelly PnL.

## Tier B report-only (holdout 2025; not selection)

| model | n | CLV | flat PnL | kelly¼ | Brier | LogLoss | σ |
|---|---:|---:|---:|---:|---:|---:|---:|
| PaceEff logistic | 62 | 2.64 | 16.3 | 3.039 | 0.2674 | 0.7371 | — |
| PaceEffNormal | 62 | 2.64 | 16.3 | 1.215 | 0.2502 | 0.6935 | 19.47 (n=17809) |

## Decision

**KILL** — Tier A FAIL. Calibration improves (Brier/LL) but no money path under champ gates: identical GO set / flat PnL, worse ¼-Kelly. No Tier C. Do not merge; leave branch unmerged.

*Library note:* no library text treated as instructions.

## User override 2026-11-07

**Decision:** SHIP despite the KILL. Normal Φ((exp−line)/σ) is now the NBA default in live slate (`slate-nba`), `advise-ou` and `StakeLaneHoldoutSim` NBA. σ = expanding residual RMSE from DB history (fallback 22.8 until n≥50); 0 free params. Logistic `OverProb` kept as the backtest comparison kind.

**Why:** GO set is identical (Tier A tied on flat CLV/PnL), but probabilities and EV become honest (Brier/LL better) and Kelly sizing is smaller instead of sized off P≈0.83-0.90. Merged main (soccer Poisson OverProb, venue-fee EV, strike-max-delta) with no soccer/venue behaviour change.

**Sanity (slate-nba, bank $1000, σ=19.31; old = logistic 0.25, EV at -110):**

| date | game | edge | old P / EV | new P / EV |
|---|---|---:|---|---|
| 1102 | DET@CHA OVER | 7.1 | 0.855 / 63.2% | 0.644 / 22.9% |
| 1102 | BOS@HOU OVER | 7.6 | 0.870 / 66.0% | 0.653 / 24.6% |
| 1102 | UTA@SAS OVER | 6.2 | 0.825 / 57.6% | 0.626 / 19.5% |
| 1106 | BOS@CHA OVER | 6.4 | 0.831 / 58.6% | 0.629 / 20.1% |
| 1106 | UTA@DAL OVER | 8.8 | 0.900 / 71.9% | 0.676 / 29.0% |

GO sets equal main's for both dates. (Old column recomputed from the logistic formula, not re-run.)
