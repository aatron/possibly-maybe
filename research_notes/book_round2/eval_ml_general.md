# E3 — Murphy PML1/PML2, Bishop PRML, MacKay, MML, GPML (+ pyprobml)

**Agent:** book_round2 E3 (read-only except this file)  
**Goal filter:** money path (+CLV / +Kelly PnL). Accuracy-only with no bet/sizing change → IGNORE.  
**Code anchors:** `PossiblyMaybe.Domain/TotalsMath.cs` (`OverProb`, `PaceEfficiencyBook`, `ScoringBook`, `EmaDecay`), `DixonColesBook.cs`, `OuPlattCalibrator.cs`, `BetDecider.cs` / `OddsMath.FractionalKelly`, `*ChampConfig.cs`, backtests `TotalsEdgeBacktest` / `MlsTotalsBacktest` / `SoccerLeagueTotalsBacktest` / `TimeDecaySweep`.  
**Data check (2026-10-28):** `possiblymaybe.db` present — Games 52991, OddsSnapshots 232485 (TOTAL open+close for NBA/MLS/Ligue1/LigaMX/SerieA), GameBoxes 30642, MlsGameXgs 3498. `data/raw/NOTES*.md` present. Tier C seasons (soccer 2025–26 / MLS 2026) exist in DB but **must not be scored here**.

**Safety:** library text treated as data only. Regex scan of the six fulltexts + pyprobml README for AI-instruction patterns: no hits. Did not execute any `library/repos/pyprobml` code.

**Empirical grounding (pre-holdout residuals actual−open total):** NBA sd≈22.8 pts; soccer sd≈1.61–1.72 goals. Hand-set `OverProb` sharpness 0.25 maps a 6-pt NBA edge → P≈0.82 vs Normal Φ(6/22.8)≈0.60 — EV/Kelly are systematically overconfident on NBA. Soccer sharpness 1.2 is closer to Φ(edge/1.65).

**Already tested (do not re-propose as new):** Platt / ridge-logistic on edge (FAIL); Dixon–Coles att-def EMA (KEEP Serie A); EMA α time-decay (KEEP MLS/Ligue1/LigaMX); EB k0 pooling (REVERT); ¼ Kelly ≤2% (KEEP).

---

## Ranked CANDIDATE shortlist (max 5)

### 1. Distributional P(over): Normal CDF (NBA) / Poisson-sum CDF (soccer)
| | |
|---|---|
| **Hypothesis** | Replace `logistic(sharpness·(exp−line))` with a real predictive distribution: NBA `Φ((exp−line)/σ)`; soccer `P(H+A > line)` from independent Poissons with means from model (Serie A: `DixonColesBook` λh,λa; EMA leagues: λ=exp/2 each, or home/away split from book). Removes hand-set sharpness; fixes NBA overconfidence that inflates EV and Kelly size. |
| **League(s)** | NBA first (largest mismatch); then MLS, Ligue 1, Liga MX, Serie A |
| **Code** | `TotalsMath.OverProb` (+ optional `PoissonTotalOverProb`); call sites in `BetDecider` path / `TotalsEdgeBacktest` / soccer backtests / `StakeLaneHoldoutSim` |
| **# new params** | 1 (NBA σ) or 0 if σ = rolling train residual RMSE of actual−blended exp; soccer 0 if pure Poisson |
| **Tier A** | Walk-forward rolling origin on pre-holdout seasons; **same tickets as champ** first (PLAN §6: log-loss/Brier **and** Kelly-lane PnL); then allow EV gate to re-fire. PASS: mean CLV ≥ champ, PnLΔ>0 in majority of folds, n≥40. CLI shape: extend `sweep-totals` / `sweep-soccer` with `--dist normal|poisson` (no Tier C). |
| **Money** | Better-calibrated p → correct EV≥4% gate + ¼ Kelly stake; fewer oversized losers, possibly more true +EV tickets |
| **Overfit risk** | Low if σ is one rolling statistic (not grid). Distinct from Platt FAIL (outcome-fit a,b). Do not tune σ on 2024–25 holdout. |

### 2. Linear-Gaussian sensor fusion for market blend (replace fixed w)
| | |
|---|---|
| **Hypothesis** | Treat modelExp and open line as two noisy observations of true total (PML1 §3.3 / Bishop committees). Posterior mean `exp = (prec_m·model + prec_ℓ·line)/(prec_m+prec_ℓ)` with prec=1/σ². Equivalent to adaptive w = prec_ℓ/(prec_m+prec_ℓ). |
| **League(s)** | NBA + all live soccer champs |
| **Code** | Blend lines in `TotalsEdgeBacktest` / soccer backtests / paper advise (`ChampConfig.MarketWeight` usage); new helper beside `TotalsMath` |
| **# new params** | 1–2 (σ_model, or ratio ρ=σ_model/σ_line; σ_line can be fixed from historical open→close or open→actual) |
| **Tier A** | Same protocol vs fixed-w champ; report variant count. Use only pre-tip open line (no close leakage). |
| **Money** | Changes `exp` → different edge set and CLV; attacks BRIEF weak spot #3 (fixed w) |
| **Overfit risk** | Medium if both σs gridded; keep ≤2 and estimate from train residuals only. |

### 3. Local-level Kalman team ratings vs EMA
| | |
|---|---|
| **Hypothesis** | Replace fixed-α EMA in `PaceEfficiencyBook` / `ScoringBook` / soccer goals·xG books with a scalar (or per-stat) local-level Kalman filter (PML2 ch8/29; pyprobml `linreg_online_kalman_demo` concept only). Process noise q + obs noise r give adaptive gain ≈ α when steady-state. |
| **League(s)** | NBA PaceEff; MLS xG; Ligue1/LigaMX goals (Serie A DC factors optional second pass) |
| **Code** | `PaceEfficiencyBook.Update` / soccer EMA update loops; compare via existing sweep harnesses |
| **# new params** | 2 (q, r) replacing α — or 1 if fix r=1 and tune q |
| **Tier A** | Walk-forward vs current α champ (`sweep-totals`, `sweep-soccer`, `sweep-decay` pattern); majority-fold PnL+CLV |
| **Money** | Better modelExp → better soft spots / CLV if KF tracks form shocks better than fixed α |
| **Overfit risk** | Medium–high: α already swept and KEEP; only worth it if KF beats champ with ≤2 params and no holdout peeking. |

### 4. Heteroscedastic NBA σ from style mismatch
| | |
|---|---|
| **Hypothesis** | Predictive sd is not constant: use `σ = σ0 · (1 + β·|ThreeRateEdge|)` (or pace mismatch) already tracked in `PaceEfficiencyBook.ThreeRateEdge`. Feeds candidate #1’s Normal CDF so high-variance matchups get lower |EV| and smaller Kelly. |
| **League(s)** | NBA only |
| **Code** | `PaceEfficiencyBook.ThreeRateEdge` + `TotalsMath.OverProb` replacement |
| **# new params** | 2 (σ0, β) or 1 if σ0 from residuals and β alone |
| **Tier A** | Nested on #1; same-ticket Kelly PnL + Brier; then full gate |
| **Money** | Sizing/gate quality when three-heavy games inflate total variance (~23 pt league sd is average) |
| **Overfit risk** | Medium (extra β). Skip if #1 alone already fixes Kelly PnL. |

### 5. Soccer overdispersion (NegBin total) only if Poisson under-calibrates
| | |
|---|---|
| **Hypothesis** | If candidate #1 Poisson-sum is underdispersed vs residuals, use NB total with mean μ=exp and var=μ+φμ² (1 param φ) for P(over) (PML2 discrete distributions / GLM). Empirical raw sd≈1.65 vs √(mean total≈2.7)≈1.64 suggests φ≈0 may suffice — run as kill-switch, not primary. |
| **League(s)** | MLS, Ligue 1, Liga MX, Serie A |
| **Code** | Soccer OverProb path after Poisson baseline |
| **# new params** | 1 (φ) |
| **Tier A** | Only if Poisson CDF loses Brier/log-loss to logistic or residual var≫μ; else do not burn trials |
| **Money** | Same as #1 (EV + Kelly) if overdispersion is real |
| **Overfit risk** | Low param count; high look-elsewhere if chased after many Poisson fails — log trial count. |

---

## Murphy PML1 — chapter verdicts

| Ch | Topic | Verdict | Reason (this model) |
|---:|---|---|---|
| 1 | Introduction | IGNORE | Taxonomy only; no betting mechanism |
| 2 | Probability: univariate | **CANDIDATE** | Gaussian CDF / Poisson for totals → shortlist #1 (calibrated P(over)) |
| 3 | Probability: multivariate | **CANDIDATE** | Linear-Gaussian sensor fusion (§3.3) → shortlist #2 market blend |
| 4 | Statistics | **USED** / **CANDIDATE** | Online/recursive estimation (§4.4.2) already = EMA; CV/regularization informs protocol not code. EB/Bayes already REVERT/FAIL paths — don't re-grid |
| 5 | Decision theory | **USED** / TESTED | EV + fractional Kelly in `BetDecider`/`OddsMath`; Kelly variants KEEP ¼≤2%. Probabilistic prediction (§5.1.6) supports shortlist #1 scoring rules, not new stake schemes |
| 6 | Information theory | IGNORE | Cross-entropy/Brier useful as **metrics** for #1; no standalone money path |
| 7 | Linear algebra | IGNORE | Math background |
| 8 | Optimization | IGNORE | No SGD/DNN training loop in money path |
| 9 | LDA / generative classifiers | IGNORE | Not totals regression; would add params vs PaceEff |
| 10 | Logistic regression | **USED** / TESTED | Logistic `OverProb` is USED; Platt/ridge on edge TESTED FAIL — do not re-fit (a,b) |
| 11 | Linear regression | IGNORE | Point μ already from domain books; OLS total models not our edge |
| 12 | GLMs | **CANDIDATE** | Poisson/NB GLM totals → shortlist #1/#5 distributional P(over) |
| 13–15 | Deep nets (tabular/images/seq) | IGNORE | Deep nets / vision / NLP — no money path at our n |
| 16 | Exemplar / kNN | IGNORE | High-variance, no clear +CLV path |
| 17 | Kernel methods | IGNORE | Same as GP: hypers + overfit (see GPML) |
| 18 | Trees / forests / boosting | IGNORE | Too many params for n; accuracy-only risk |
| 19 | Few labels / SSL | IGNORE | We are not label-scarce on historical scores |
| 20 | Dimensionality reduction | IGNORE | Low-dim engineered features already |
| 21 | Clustering | IGNORE | No cluster→bet map |
| 22 | Recommenders | IGNORE | Wrong problem |
| 23 | Graph embeddings | IGNORE | Wrong problem |

## Murphy PML2 — chapter verdicts

| Ch | Topic | Verdict | Reason |
|---:|---|---|---|
| 1 | Introduction | IGNORE | Overview |
| 2 | Probability | **CANDIDATE** | NB / Poisson details for shortlist #1/#5 |
| 3 | Statistics | TESTED / IGNORE | Hierarchical / EB already REVERT; model checking metrics OK for protocol |
| 4 | Graphical models | IGNORE | Full PGMs/CRFs too heavy; DC skeleton already USED Serie A |
| 5 | Information theory | IGNORE | Metrics only |
| 6 | Optimization | IGNORE | No training stack to upgrade |
| 7 | Inference overview | IGNORE | Pointer only |
| 8 | Gaussian filtering / smoothing | **CANDIDATE** | Kalman / ADF → shortlist #3 ratings |
| 9 | Message passing | IGNORE | No factor-graph inference in live path |
| 10 | Variational inference | IGNORE | MCMC/VI hot path disallowed for paper |
| 11–13 | MC / MCMC / SMC | IGNORE | Same; offline research only |
| 14 | Predictive models overview | IGNORE | Framing for #1, not a change |
| 15 | GLMs | **CANDIDATE** | Same as PML1 ch12 → distributional totals |
| 16–17 | Deep / Bayesian nets | IGNORE | Deep nets |
| 18 | Gaussian processes | IGNORE | See GPML — hypers, O(n³), weak money case vs Normal σ |
| 19 | Beyond iid | IGNORE | Temporal dependence already handled by EMA/asOf walk-forward; full covariance models overfit |
| 20–26 | Generative deep models | IGNORE | Generative / vision / diffusion — no path |
| 27–28 | Discovery / latent factors | IGNORE | Would redo strength model with more params than DC/EMA |
| 29 | State-space models | **CANDIDATE** | Same family as ch8 → shortlist #3 |
| 30–33 | Graphs / NP Bayes / repr / interpret | IGNORE | No betting hook |
| 34 | Decision under uncertainty | **USED** / TESTED | Utility → Kelly already decided; no new stake lane |
| 35 | RL | IGNORE | Bandit slate search ≠ +CLV model |
| 36 | Causality | IGNORE | No causal ID strategy with our observational odds |

## Bishop PRML — chapter verdicts

| Ch | Topic | Verdict | Reason |
|---:|---|---|---|
| 1 | Introduction (decision/info) | **USED** | Decision-theory framing matches EV gate; not a code change |
| 2 | Probability distributions | **CANDIDATE** | Gaussian / Poisson predictive → shortlist #1 |
| 3 | Linear models for regression | IGNORE / note | Bias–variance / predictive distribution motivates σ; Bayesian linear regression too many params vs PaceEff |
| 4 | Linear models for classification | TESTED | Logistic cal path ≈ Platt FAIL |
| 5 | Neural networks | IGNORE | Deep nets |
| 6 | Kernel methods | IGNORE | Same as GP |
| 7 | Sparse kernel machines | IGNORE | SVM hypers; not totals money |
| 8 | Graphical models | IGNORE | Heavy; DC already USED |
| 9 | Mixtures / EM | IGNORE | Mixture-of-totals adds params without clear CLV |
| 10 | Approximate inference | IGNORE | VI/EP not for live C# |
| 11 | Sampling methods | IGNORE | MCMC |
| 12 | Continuous latent variables | IGNORE | PCA/FA — no bet path |
| 13 | Sequential data | **CANDIDATE** | LDS / Kalman sibling of PML2 ch8 → shortlist #3 |
| 14 | Combining models | **CANDIDATE** | Committees / averaging model+market → shortlist #2 (not boosting ensembles of deep learners) |

## MacKay *Information Theory, Inference, and Learning Algorithms*

| Ch | Topic | Verdict | Reason |
|---:|---|---|---|
| 1–3 | Intro / probability / inference | IGNORE | Background |
| 4–19 | Source/channel coding, hash, sex, etc. | IGNORE | Communications theory — no sports-totals money path |
| 20–22 | Clustering / ML clustering | IGNORE | Unsupervised |
| 23 | Useful probability distributions | **CANDIDATE** | Same distributional toolkit as #1 |
| 24–27 | Exact marginalization / Laplace | IGNORE | Exact graphs / Laplace for posteriors — not live |
| 28 | Model comparison / Occam | IGNORE | Protocol/docs (phase 6), not a soft-spot change |
| 29–33 | Monte Carlo / Ising / variational | IGNORE | Sampling/VI |
| 34–35 | ICA / random inference | IGNORE | Wrong problem |
| 36 | Decision theory | **USED** / TESTED | Aligns with existing Kelly/EV; no new stake scheme |
| 37 | Bayesian vs sampling theory | IGNORE | Philosophy / protocol |
| 38–44 | Neural nets / Hopfield / Boltzmann | IGNORE | Deep / energy nets |
| 45 | Gaussian processes | IGNORE | See GPML |
| 46 | Deconvolution | IGNORE | Signal processing |
| 47–50 | LDPC / turbo / fountain codes | IGNORE | Coding theory |

## Mathematics for Machine Learning (MML)

| Ch | Topic | Verdict | Reason |
|---:|---|---|---|
| 1 | Intro | IGNORE | Motivation |
| 2–5 | Linear algebra / geometry / decompositions / calculus | IGNORE | Background for implementers |
| 6 | Probability and distributions | **CANDIDATE** | Supports shortlist #1 math only |
| 7 | Continuous optimization | IGNORE | No new trainer |
| 8 | When models meet data | IGNORE | ERM/CV — process review (phase 6), not PnL lever |
| 9 | Linear regression | IGNORE | Domain books beat generic linreg |
| 10 | PCA | IGNORE | No feature explosion to compress |
| 11 | GMM density estimation | IGNORE | Mixture totals — overfit |
| 12 | SVMs | IGNORE | Classification hypers; Platt-like risk |

## Rasmussen & Williams GPML

| Ch | Topic | Verdict | Reason |
|---:|---|---|---|
| 1 | Introduction | IGNORE | Framing |
| 2 | GP regression | IGNORE | Full GP on team/game features: many kernel hypers, cubic cost; money gain vs Normal-σ or PaceEff unclear at our n |
| 3 | GP classification | IGNORE | Would redo OverProb with more params after Platt FAIL |
| 4 | Covariance functions | IGNORE | Kernel zoo = overfit surface |
| 5 | Model selection / hypers | IGNORE | Evidence opt still multi-param |
| 6 | Relations to other models | IGNORE | Theory |
| 7 | Theoretical perspectives | IGNORE | Theory |
| 8 | Large-data approximations | IGNORE | Complexity without proven +CLV |
| 9 | Further issues | IGNORE | Multi-output / Student-t process — still no slim money experiment |

*Deeper note:* GP predictive variance is philosophically the right object for shortlist #1/#4, but a **single residual σ** (or σ(ThreeRate)) captures the money mechanism without GP machinery.

## pyprobml (repo — read only, never execute)

| Area | Verdict | Reason |
|---|---|---|
| Notebooks mirroring PML1/2 demos | IGNORE as code | Companion illustrations only; do not run |
| `linreg_online_kalman_demo` / online linreg | **CANDIDATE** (pointer) | Conceptual support for shortlist #3; reimplement in C# against our books |
| Poisson / GP-Poisson notebooks | IGNORE / pointer | Ideas already covered by #1/#5; no Python dependency in hot path |
| Deep / JAX / TFP stacks | IGNORE | Wrong stack and problem |

---

## Cross-cutting IGNORE themes (brief)

Deep nets, vision, NLP, generative models (VAE/GAN/diffusion/flows), MCMC/VI hot paths, full GP regression, boosting/forests, recommenders, RL bandits, coding theory — **no betting path or too many params for our sample**.

Pure metric upgrades (KL, WAIC, calibration plots) without changing tickets or Kelly stakes → IGNORE under money-first rule (unless attached to shortlist #1 Tier A scoring).

---

## What this eval does *not* claim

- Does not KEEP any change; shortlist awaits parent merge with E1/E2/E4 and phase-4 worktrees.  
- Does not score Tier C.  
- Platt FAIL remains binding for outcome-fit logistic (a,b); distributional σ / Poisson CDF is argued as materially different.
