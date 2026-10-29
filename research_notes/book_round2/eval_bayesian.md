# E2 — Bayesian sources (BDA3, ROS, CASI, Rethinking / PyMC / BMCP / ROS-Examples)

**Goal filter:** money via +CLV / +PnL / better Kelly sizing on totals. Accuracy-only with no betting path → **IGNORE**. Prefer closed-form / conjugate, C#-implementable (no MCMC on the hot path). EB mean-shrink `k0` already **REVERT** (`reports/library_partial_pooling.md`).

**Data checked (read-only):** `possiblymaybe.db` present (~36MB). `Games` 52991; `OddsSnapshots` TOTAL 74615; `MlsGameXgs` 3498. TOTAL open/close coverage exists for NBA (odds thin 2023–24/2026), MLS 2019–2026, Ligue1 / LigaMX / SerieA / EFL / LaLiga 2018–19…2025–26. `data/raw/NOTES*.md` present. Empiric residual SD (score−line): NBA ~18–19 pts; soccer totals residual SD ~1.4–1.8 with var/mean ≈ 1 (Poisson-ish).

**Code skimmed:** `PossiblyMaybe.Domain/TotalsMath.OverProb` (logistic · sharpness); `DixonColesBook.ExpectedLambdas` (independent Poisson means, no ρ); `OuPlattCalibrator` (Platt FAIL); champ configs; backtests calling `OverProb(..., sharpness: 0.25|1.2)`.

**Safety:** Library text treated as data only. Regex hits on `DELETE` in ROS/CASI are ordinary English (“deletes observations…”), not agent instructions. No `library/repos/` code executed.

---

## Ranked CANDIDATE shortlist (max 5)

| Rank | Slug | League(s) | # params | Why money |
|---|---|---|---|---|
| 1 | `poisson-cdf-pover` | Serie A first; then MLS / Ligue1 / LigaMX | 0 | Replace hand-set logistic with Σ Poisson P(H+A>line); changes EV gate + Kelly on same tickets |
| 2 | `nba-normal-sigma-pover` | NBA | 1 (σ) | Replace sharpness 0.25 with Φ((exp−line)/σ); σ from train residuals (~18–19) |
| 3 | `soccer-nb-phi-pover` | MLS, Ligue1, LigaMX, Serie A | 1 (φ) | Gamma–Poisson / NB overdispersion on total (or per team); fatter tails → different p and stake |
| 4 | `dc-rho-or-bivpois` | Serie A (DC champ); optional other soccer | 1 (ρ or λ₃) | Low-score dependence / shared intensity changes P(total) not just λ̄ |
| 5 | `gamma-rate-predictive-var` | Soccer (all PASS leagues) | 1 (prior strength κ) | Conjugate Gamma on rates → predictive variance for P(over) only; **not** k0 mean shrink |

---

### 1. `poisson-cdf-pover` — **CANDIDATE**

- **Hypothesis:** Hand-set `logistic(1.2·edge)` mis-states P(over) for count totals. With λ_h, λ_a from the champ mean model, `P(H+A > line) = 1 − F_{Pois(λ_h)*Pois(λ_a)}(⌊line⌋)` (half-lines: no push mass) is the conjugate closed-form predictive under independent Poisson (BDA3 ch2 Poisson; Rethinking “Modeling Events”; Dixon–Coles skeleton already in-repo).
- **League(s):** Serie A (has `ExpectedLambdas`); then MLS/Ligue1/LigaMX with λ_h=λ_a=exp/2 or home-split from `ScoringBook` for/against.
- **Code:** Add `TotalsMath.PoissonTotalOverProb(lh, la, line)`; wire in `SoccerLeagueTotalsBacktest.cs` / `MlsTotalsBacktest.cs` / `StakeLaneHoldoutSim.cs` / `DailySlate.cs` instead of `OverProb(..., sharpness: 1.2)`. Serie A: `DixonColesBook.ExpectedLambdas` (`DixonColesBook.cs:26–38`).
- **# new params:** 0 (distributional assumption only). Optional later: ρ → rank 4.
- **Tier A:** Walk-forward rolling origin on pre-holdout seasons; same champ `w` / `minEdge` / EV≥4% gates; compare mean CLV vs champ, Kelly-lane PnL delta > 0 in majority of folds, n≥40; also log-loss/Brier on same tickets (PLAN §6). Log variant count (=1). **Do not score 2025–26 / MLS 2026.**
- **Money mechanism:** Better-calibrated p flips EV≥4% membership and ¼-Kelly size without retuning the mean model.
- **Overfit risk:** Low (0 params). Model risk if independence / Poisson fails (check residual var/mean; escalate to NB).

### 2. `nba-normal-sigma-pover` — **CANDIDATE**

- **Hypothesis:** NBA totals are continuous-ish; logistic sharpness 0.25 is an arbitrary scale. Train-only residual SD σ̂ of `(actualTotal − blendedExp)` → `P(over)=1−Φ((line−exp)/σ)` (BDA3 normal predictive; ROS ch9/11 σ; CASI conjugate normal). Optional Student-t with fixed ν (BDA3 ch17) if tails are heavy — still ≤1 free param if ν locked.
- **League(s):** NBA (provisional KEEP protocol: Tier A+B only).
- **Code:** `TotalsMath.OverProb` call sites in `TotalsEdgeBacktest.cs` / `StakeLaneHoldoutSim.cs` / `DailySlate.cs`; estimate σ on train folds only inside backtest (do not reuse Platt path).
- **# new params:** 1 (σ), or 0 if σ locked to historical ~19 from train seasons only before each fold.
- **Tier A:** Rolling origin on pre-2024–25 NBA with totals odds; same PaceEff w=0.5 edge≥6 EV≥4%; Kelly PnL + Brier/LL; report Tier B 2024–25 but do not KEEP on B alone.
- **Money mechanism:** EV gate + Kelly sizing on NBA (largest $ holdout book).
- **Overfit risk:** Low–medium if σ is swept; keep σ = rolling train RMSE (0 free grid cells) preferred. Distinct from Platt FAIL (2-param logistic on edge).

### 3. `soccer-nb-phi-pover` — **CANDIDATE**

- **Hypothesis:** Poisson understates uncertainty in λ (BDA3 §2 NegBin = Gamma–Poisson mixture; ch16.4 / 17.2 overdispersed Poisson). One dispersion φ: total ~ NB(mean=exp, φ) or independent NB goals; `P(over)` from NB CDF. C# via recursive NB PMF or continued fraction — no MCMC.
- **League(s):** MLS, Ligue1, LigaMX, Serie A (PASS leagues first).
- **Code:** New helper beside `TotalsMath`; swap soccer `OverProb` in `*TotalsBacktest.cs` / slate. Mean model unchanged (EMA / DC).
- **# new params:** 1 (φ). Fit φ on train residuals only (method of moments: φ from var/mean), or tiny grid ≤3 values on train.
- **Tier A:** Same as (1) with φ chosen per fold on train only; n≥40; majority-fold Kelly PnL uplift + CLV≥champ mean.
- **Money mechanism:** Wider predictive tails shrink overconfident p near the gate → fewer bad large Kelly bets / different ticket set.
- **Overfit risk:** Medium if φ grid is large; lock to MoM. Empiric var/mean≈1 suggests φ large (near Poisson) — may collapse to (1); still worth one Tier A pass.

### 4. `dc-rho-or-bivpois` — **CANDIDATE**

- **Hypothesis:** Current `DixonColesBook` is independent Poisson, no ρ (file shortcut). Low-score correlation / bivariate Poisson shared intensity changes P(total) at common lines (2.5/3.5) without changing much the mean (penaltyblog bivariate / DC ρ; BDA3 multiparameter Poisson). Closed-form bivariate Poisson PMF is a finite sum — C#-ok.
- **League(s):** Serie A first (DC is champ); research-only on MLS if Tier A passes.
- **Code:** Extend `DixonColesBook` with optional ρ or λ_shared; `P(over)` from joint PMF; keep EMA α=0.18.
- **# new params:** 1.
- **Tier A:** Serie A rolling origin pre-2024–25; fix α/w/edge at champ; only ρ (or λ₃) from train; Kelly+CLV rules; no Tier C here.
- **Money mechanism:** Distributional P(over) + slight mean coupling → EV/Kelly; possible small mean-model CLV change.
- **Overfit risk:** Medium (ρ weakly identified on n~200/season). Prefer ρ∈{0} vs one train MoM estimate, not a wide grid.

### 5. `gamma-rate-predictive-var` — **CANDIDATE** (materially ≠ EB k0)

- **Hypothesis:** EB `k0` shrunk **means** toward league and **REVERT**ed. Instead keep EMA/DC means; put a Gamma(κ·μ, κ) prior on team rates and use the **posterior predictive** NegBin (BDA3 ch2 conjugate; CASI ch6/13 conjugate EB) only to widen P(over). κ = prior strength in “pseudo-games.”
- **League(s):** Soccer PASS leagues.
- **Code:** Thin wrapper over λ from `ScoringBook`/`DixonColesBook` → predictive variance `μ(1+μ/κ)`; NB or Poisson–Gamma CDF for P(over). Do **not** replace EMA update with James–Stein means.
- **# new params:** 1 (κ).
- **Tier A:** Identical gates; κ from train MoM or ≤3 train values; require Kelly PnL uplift (PLAN §6). If it only improves Brier with flat stakes flat → FAIL.
- **Money mechanism:** Same as NB — calibrated uncertainty → stake/EV.
- **Overfit risk:** Medium; easy to rediscover k0 if someone “also” shrinks means — kill rule: means path frozen to champ.

---

## Per-chapter / per-unit verdicts

Verdict key: **USED** / **TESTED** / **CANDIDATE** / **IGNORE** — reason specific to this totals stack.

### BDA3 — Bayesian Data Analysis (Gelman et al.)

| Ch | Title | Verdict | Reason |
|---|---|---|---|
| 1 | Probability and inference | IGNORE | Foundations only; no new bet path. |
| 2 | Single-parameter models | **CANDIDATE** | Poisson / Gamma–Poisson → NegBin predictive is the closed-form engine for shortlist 1/3/5. |
| 3 | Introduction to multiparameter models | IGNORE | Multiparameter Normal/etc. generalities; NBA σ is enough from ch2/3 normal facts without full multiparameter workflow. |
| 4 | Asymptotics / non-Bayesian connections | IGNORE | Theory; no sizing/CLV lever. |
| 5 | Hierarchical models | **TESTED** | Partial pooling / EB shrink of team means → k0 **REVERT**; full hierarchical needs MCMC we refuse on hot path. |
| 6 | Model checking | IGNORE | PPC is process gold for research, but alone does not pick tickets; no money path unless turned into a bet filter (not proposed ≤3 params). |
| 7 | Evaluating, comparing, expanding models | IGNORE | LOO/WAIC/elpd useful for α choice later; accuracy-only vs our CLV/PnL protocol unless wired as train selector (out of scope / param-creep). |
| 8 | Modeling accounting for data collection | IGNORE | Survey/design sampling ≠ sports odds generation. |
| 9 | Decision analysis | **USED** | Decision under uncertainty ≈ EV + Kelly lanes already; no new Bayesian decision layer needed. |
| 10 | Introduction to Bayesian computation | IGNORE | Points at simulation/MCMC we will not run live. |
| 11 | Basics of Markov chain simulation | IGNORE | MCMC hot path forbidden. |
| 12 | Computationally efficient MCMC | IGNORE | Same. |
| 13 | Modal and distributional approximations | IGNORE | Laplace/VI interesting offline; not needed if conjugate CDF works; extra complexity. |
| 14 | Introduction to regression models | IGNORE | Generic regression; our means are EMA/DC not BDA regression. |
| 15 | Hierarchical linear models | **TESTED** | Same family as EB pooling / James–Stein team effects — mean shrink failed. |
| 16 | Generalized linear models | **CANDIDATE** | Overdispersed Poisson (16.4) motivates shortlist 3; implement as NB CDF not full GLM MLE. |
| 17 | Models for robust inference | **CANDIDATE** | Overdispersed / t errors → NBA t-CDF or soccer NB tails (pair with shortlist 2–3); keep ≤1 param. |
| 18 | Models for missing data | IGNORE | We lack a missingness model for odds gaps; no soft-spot. |
| 19 | Parametric nonlinear models | IGNORE | No nonlinear structural totals model on table. |
| 20 | Basis function models | IGNORE | Splines/basis = too many params for our n. |
| 21 | Gaussian process models | IGNORE | GP team strength = MCMC/approx heavy; no C# hot path. |
| 22 | Finite mixture models | IGNORE | Mixture regimes (e.g. pace states) ≫3 params / identification risk. |
| 23 | Dirichlet process models | IGNORE | Nonparametric Bayes — not implementable in our constraint set. |

### ROS — Regression and Other Stories

| Ch | Title | Verdict | Reason |
|---|---|---|---|
| 1 | Overview | IGNORE | Pedagogy. |
| 2 | Data and measurement | IGNORE | Good hygiene; not a bet feature. |
| 3 | Basic math and probability | IGNORE | Background (distributions already assumed). |
| 4 | Statistical inference | IGNORE | Significance critique ≠ CLV. |
| 5 | Simulation | **USED** | Fake-data / predictive simulation mindset matches walk-forward backtests; no new param. |
| 6 | Background on regression modeling | IGNORE | Regression-to-mean story; mean-shrink already TESTED via k0. |
| 7 | Linear regression with a single predictor | IGNORE | Not our mean model. |
| 8 | Fitting regression models | IGNORE | lm/stan_glm tooling, not money. |
| 9 | Prediction and Bayesian inference | **CANDIDATE** | `posterior_predict` / propagate uncertainty → distributional P(over) (shortlist 1–2); closed-form stand-in for posterior sims. |
| 10 | Linear regression with multiple predictors | IGNORE | Multi-predictor totals regressions = param bomb vs EMA. |
| 11 | Assumptions, diagnostics, and model evaluation | IGNORE | Residual/σ diagnostics inform shortlist 2 but chapter itself is process; CV ideas already in PLAN Tier A. |
| 12 | Transformations and regression | IGNORE | Log etc. not needed for OU edge on blended totals. |
| 13 | Logistic regression | **TESTED** | Platt / ridge-logistic on edge **FAIL** (`PROGRESS.md`, `OuPlattCalibrator`). |
| 14 | Working with logistic regression | **TESTED** | Same calibration family as ch13 — already failed as money path. |
| 15 | Other generalized linear models | **CANDIDATE** | Poisson / overdispersed counts → shortlist 1/3 (use PMF/CDF, not new GLM fit of att/def). |
| 16 | Design and sample size decisions | IGNORE | Power calculations; n constraints already in protocol. |
| 17 | Poststratification and missing-data imputation | IGNORE | No MRP/poststrat use case for league totals. |
| 18–21 | Causal inference chapters | IGNORE | We are not estimating treatment effects; betting ≠ causal ID. |
| 22 | Advanced regression and multilevel models | **TESTED** / IGNORE | Multilevel pointer = hierarchical shrink path already REVERT; Stan demos = MCMC IGNORE. |

### CASI — Computer Age Statistical Inference (Efron & Hastie)

| Ch | Title | Verdict | Reason |
|---|---|---|---|
| 1 | Algorithms and Inference | IGNORE | Framing. |
| 2 | Frequentist Inference | IGNORE | Theory. |
| 3 | Bayesian Inference | IGNORE | Conceptual Bayes vs frequentist; no implementable totals change. |
| 4 | Fisherian Inference and MLE | IGNORE | MLE att/def batch fit would fight our EMA walk-forward (Serie A already EMA-DC). |
| 5 | Parametric Models and Exponential Families | **CANDIDATE** | ExpFam / Poisson–Gamma conjugacy underpins shortlist 3/5. |
| 6 | Empirical Bayes | **TESTED** | EB pooling of team strengths → k0 **REVERT**. |
| 7 | James–Stein and Ridge | **TESTED** | JS baseball shrink = same mean-pooling idea; ridge-logistic cal **FAIL**. |
| 8 | GLMs and Regression Trees | **CANDIDATE** | Poisson GLM section supports count P(over); trees IGNORE (overfit). |
| 9 | Survival Analysis and EM | IGNORE | Censoring/survival not our totals problem. |
| 10 | Jackknife and Bootstrap | IGNORE | CIs for research OK; not a stake rule (would need explicit Kelly mapping). |
| 11 | Bootstrap Confidence Intervals | IGNORE | Same. |
| 12 | Cross-Validation and Cp | **USED** (process) | Reinforces walk-forward / holdout discipline already in PLAN; not a market feature. |
| 13 | Objective Bayes and MCMC | IGNORE | MCMC / BIC model pick on hot path — no. Conjugate subsection overlaps shortlist 5 without MCMC. |
| 14 | Postwar Statistical Inference | IGNORE | History. |
| 15 | Large-Scale Hypothesis Testing / FDRs | IGNORE | Massive parallel tests ≠ ~1 bet/day slate. |
| 16 | Sparse Modeling and the Lasso | IGNORE | Feature lasso on totals already out of scope / sparsity time-decay handled elsewhere; many params. |
| 17 | Random Forests and Boosting | IGNORE | Black-box, high capacity, weak money path vs EMA. |
| 18 | Neural Networks and Deep Learning | IGNORE | No DL hot path. |
| 19 | SVMs and Kernel Methods | IGNORE | Same. |
| 20 | Inference After Model Selection | IGNORE | Important for audit/overcorrection agents; not a soft-spot feature. |
| 21 | Empirical Bayes Estimation Strategies | **TESTED** | g-modeling / advanced EB still mean-pooling family that failed as k0. |

### Repos (read-only; not executed)

#### `library/repos/stat_rethinking_2024` (McElreath lectures/scripts)

| Unit | Verdict | Reason |
|---|---|---|
| W1–2 ch1–4 (garden, geocentric, curves) | IGNORE | Teaching sims. |
| W3 ch5–6 confounds | IGNORE | Causal confounds ≠ OU edge. |
| W4 ch7 overfitting | **USED** (process) | Overfit caution aligns with ≤3 params / Tier A. |
| W4 ch8 MCMC | IGNORE | No MCMC hot path. |
| W5 ch10–11 counts / Poisson GLMs (`10_confounds_poisson.r`) | **CANDIDATE** | Count generative models → shortlist 1/3. |
| W6 ordered categories | IGNORE | Not totals. |
| W6–7 multilevel / GLMM (`12_intro_multilevel_models.r`, `13_*`, `14_*`) | **TESTED** | Partial pooling of unit intercepts ≈ EB k0 path. |
| W8 social networks / GPs | IGNORE | Wrong structure / GP heavy. |
| W9 measurement error / missing | IGNORE | No measurement-error odds model in scope. |

#### `library/repos/pymc-resources` (Rethinking_2, BDA3 ports, etc.)

| Unit | Verdict | Reason |
|---|---|---|
| Rethinking_2 Chp notebooks (esp. counts / multilevel) | Same as Rethinking table | PyMC ports are reference; do not run. |
| BDA3 PyMC port | IGNORE | MCMC illustrations of BDA3; we want conjugate CDF not ports. |
| BCM / BSM / Bayes_Rules trees | IGNORE | Cognitive / textbook ports; no totals soft-spot beyond ideas already covered. |

#### `library/repos/BookCode_Edition1` (BMCP — Bayesian Modeling and Computation in Python)

| Ch | Title (from markdown) | Verdict | Reason |
|---|---|---|---|
| 1 | Bayesian Inference | IGNORE | Intro. |
| 2 | Exploratory Analysis of Bayesian Models | IGNORE | ArviZ/PPC tooling — process only. |
| 3 | Linear Models and PPLs | IGNORE | PPL stack ≠ C# EMA. |
| 4 | Extending Linear Models | **CANDIDATE** | Hierarchical/robust extensions; only the **predictive variance** piece (shortlist 5), not full multilevel fit. |
| 5 | Splines | IGNORE | Too flexible. |
| 6 | Time Series | IGNORE | State-space vs EMA is an E3-ish topic; BMCP TS examples are MCMC. |
| 7 | BART | IGNORE | Tree ensembles, high capacity. |
| 8 | Approximate Bayesian Computation | IGNORE | Likelihood-free — unnecessary. |
| 9 | End to End Bayesian Workflows | IGNORE | Workflow/LOO — process (Phase 6), not a bet feature. |
| 10 | Probabilistic Programming Languages | IGNORE | Tooling. |
| 11 | Appendiceal Topics | IGNORE | Appendix. |

#### `library/repos/ROS-Examples`

| Example cluster | Verdict | Reason |
|---|---|---|
| `PoissonExample`, count-ish demos | **CANDIDATE** | Illustrates Poisson prediction → shortlist 1. |
| `CrossValidation`, `FakeKCV` | **USED** (process) | Echoes Tier A rolling origin. |
| `LogisticPriors`, logistic examples | **TESTED** | Logistic cal already FAIL. |
| Multilevel / ARM-style hierarchies | **TESTED** | Mean pooling family. |
| Causal / elections / golf / etc. | IGNORE | Wrong outcome type. |

---

## Explicit non-shortlist notes (focus topics)

| Topic | Verdict | Note |
|---|---|---|
| Hierarchical team strength (mean shrink) | **TESTED** | EB k0 REVERT — do not re-propose unless predictive-variance-only (shortlist 5). |
| Overdispersed counts NB / bivariate Poisson | **CANDIDATE** | Shortlist 3–4. |
| Posterior predictive P(over) vs logistic sharpness | **CANDIDATE** | Shortlist 1–2 (core money bet of this eval). |
| Model checking (PPC plots) | IGNORE | No ticket path. |
| LOO / WAIC | IGNORE | Accuracy model choice without CLV/PnL wiring. |
| Full Stan/PyMC/MCMC ratings | IGNORE | Violates C# / no-MCMC constraint. |

---

## Suggested Tier A CLI shape (for parent / phase-4 implementers)

Not run in this eval. Illustrative only:

```text
# after experiment flag exists — same tickets judged on LL/Brier + Kelly PnL
dotnet run --project PossiblyMaybe.Cli -c Release -- sweep-soccer-dc SERIEA <preHoldSeason> [db] --pover poisson
dotnet run --project PossiblyMaybe.Cli -c Release -- sweep-totals [db] --pover normal-sigma
dotnet run --project PossiblyMaybe.Cli -c Release -- sweep-mls [db] --pover nb
```

Never score soccer 2025–26 / MLS 2026 in experiment agents (Tier C = parent once).

---

## Data / implementability checklist

| Need | Status |
|---|---|---|
| Historical totals lines + results | Yes (`OddsSnapshots` TOTAL ⨝ `Games`) |
| Soccer team goals for Poisson λ | Yes |
| MLS xG for mean model | Yes (`MlsGameXgs`); P(over) can stay goals-space or xG-λ |
| Serie A DC λ_h, λ_a | Yes (`DixonColesBook.ExpectedLambdas`) |
| NBA boxes for PaceEff | Yes (`GameBoxes`) |
| MCMC / Stan runtime | Not required for shortlist |
