# Experiment: `inv-var-blend-w`

**Hypothesis:** Replace fixed market blend `w` with inverse-variance fusion  
`exp = (prec_m·model + prec_ℓ·line)/(prec_m+prec_ℓ)`, σ from train-fold residual MoM only.

**Code:** `MarketBlend` in `PossiblyMaybe.Domain/TotalsMath.cs`; Tier A runner `InvVarBlendTierA.cs`; CLI `sweep-totals --inv-var`.

**Params:** 0 free grid (2 MoM σs estimated per fold on train only). **N_trials = 2** (1 family × NBA + MLS). No Tier B in grid; no Tier C.

**Gates:** frozen champ (NBA PaceEff w=0.5 edge≥6 EV≥4%; MLS xG-EMA w=0.4 edge≥0.25 EV≥4%).

## Tier A fold tables

### NBA (test seasons 2009–2022; holdout 2025 unscored)

| fold | σ_m | σ_ℓ | w_inv | n_c | CLV_c | PnL_c | n_i | CLV_i | PnL_i | ΔPnL |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 2009 | 19.32 | 18.11 | 0.532 | 71 | 0.61 | −10.8 | 54 | 0.82 | −9.1 | +1.7 |
| 2010 | 19.16 | 17.99 | 0.531 | 59 | 1.24 | +0.3 | 45 | 1.33 | +0.9 | +0.6 |
| 2011 | 18.83 | 17.72 | 0.530 | 29 | 18.47 | +3.5 | 18 | 29.06 | −0.8 | −4.3 |
| 2012 | 18.72 | 18.21 | 0.514 | 30 | 6.17 | +6.3 | 26 | 7.23 | +6.5 | +0.2 |
| 2013 | 18.56 | 18.05 | 0.514 | 29 | 6.59 | +5.4 | 26 | 7.33 | +4.5 | −0.8 |
| 2014 | 18.44 | 18.09 | 0.510 | 16 | 0.22 | +6.9 | 14 | 0.46 | +7.0 | +0.1 |
| 2015 | 18.34 | 17.97 | 0.510 | 16 | 13.22 | +1.2 | 13 | 16.31 | +0.4 | −0.8 |
| 2016 | 18.28 | 18.05 | 0.506 | 22 | 0.80 | +0.9 | 20 | 0.72 | −0.9 | −1.8 |
| 2017 | 18.28 | 18.02 | 0.507 | 19 | 94.32 | +5.8 | 19 | 94.32 | +5.8 | 0.0 |
| 2018 | 18.27 | 24.50 | 0.357 | 20 | −0.15 | +7.7 | 72 | 0.88 | +15.9 | +8.2 |
| 2019 | 18.30 | 23.97 | 0.368 | 43 | 0.79 | −7.6 | 99 | 0.77 | −6.2 | +1.5 |
| 2020 | 18.42 | 23.58 | 0.379 | 30 | 0.38 | +10.1 | 63 | 0.79 | +5.7 | −4.4 |
| 2021 | 18.51 | 23.30 | 0.387 | 33 | 26.80 | +10.9 | 58 | 15.83 | +12.6 | +1.7 |
| 2022 | 18.55 | 23.26 | 0.389 | 6 | 74.25 | +3.5 | 9 | 50.06 | +2.5 | −1.1 |

Summary: **PnLΔ>0 in 7/14** (not majority); mean CLV champ **10.46** vs inv **8.56**; n_inv=536. **Tier A FAIL.**

### MLS (test 2020–2024; skip 2025 Tier B / 2026 Tier C)

| fold | σ_m | σ_ℓ | w_inv | n_c | CLV_c | PnL_c | n_i | CLV_i | PnL_i | ΔPnL |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 2020 | 1.82 | 1.86 | 0.489 | 49 | 0.33 | 0.0 | 34 | 0.35 | +0.6 | +0.6 |
| 2021 | 1.81 | 1.83 | 0.495 | 43 | 0.22 | +10.5 | 21 | 0.31 | +5.7 | −4.8 |
| 2022 | 1.74 | 1.76 | 0.495 | 50 | 0.25 | +0.8 | 19 | 0.21 | +6.0 | +5.2 |
| 2023 | 1.75 | 1.76 | 0.499 | 66 | 0.31 | +12.5 | 44 | 0.37 | +3.7 | −8.7 |
| 2024 | 1.72 | 1.73 | 0.499 | 52 | 0.25 | −2.4 | 36 | 0.26 | −5.5 | −3.1 |

Summary: **PnLΔ>0 in 2/5**; mean CLV inv slightly better (0.310 vs 0.275) but PnL loses; n_inv=154. **Tier A FAIL.**

## Verdict: **KILL**

Inv-var MoM σs push `w_inv` ≈ 0.5 NBA early / ≈ 0.37–0.39 later and ≈ 0.49 MLS — no majority-fold PnL lift vs frozen champ w; NBA mean CLV worse. Do **not** expand σ grid, ratio variants, or other soccer leagues. ChampConfig untouched. No Tier C.

**Reproduce:** `dotnet run --project PossiblyMaybe.Cli -c Release -- sweep-totals --inv-var`
