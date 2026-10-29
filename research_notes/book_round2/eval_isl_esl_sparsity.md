# E1 — ISLP / ESL / Statistical Learning with Sparsity

**Agent:** E1 (read-only). **Sources:** `library/fulltext/ISLP_Intro_Statistical_Learning_Python.txt` (primary; ISLR2 same content), `ESL_Elements_of_Statistical_Learning.txt`, `Statistical_Learning_with_Sparsity.txt`.  
**Grounding:** `BRIEF.md`, `PLAN.md`, `TotalsMath.cs`, `DixonColesBook.cs`, `OuPlattCalibrator.cs`, `*ChampConfig.cs`, `PROGRESS.md`, `reports/library_*.md`.  
**Data check (before any “testable” claim):** `possiblymaybe.db` via `python3`+`sqlite3` — Games 52991; OddsSnapshots TOTAL present for NBA/MLS/Ligue1/LigaMX/SerieA; MlsGameXgs 3498; GameBoxes 30642. `data/raw/NOTES*.md` present. NBA TOTAL odds gap seasons 2023/2024/2026 (known).  
**Safety:** Grep for AI/agent instruction strings in the three books — none found. Library text treated as data only; no `library/` code executed.

**Money filter:** Accuracy-only ideas with no path to +CLV / +PnL / better Kelly on the same tickets → **IGNORE**. Already-tried topics not re-proposed unless materially different.

---

## ISLP — chapter + main-section verdicts

(ISLR2 mirrors these chapters; no separate table.)

| Topic | Verdict | Reason (this model) |
|---|---|---|
| **1 Introduction** | IGNORE | Framing only; no betting lever. |
| **2 Statistical Learning** | — | — |
| 2.1 What Is Statistical Learning? | USED | Supervised regression of expected total from team stats (`PaceEfficiencyBook` / `ScoringBook` / `DixonColesBook`). |
| 2.1.1 Why Estimate f? | USED | We estimate E[total] for edge vs line, not pure inference. |
| 2.1.2 How Do We Estimate f? | USED | Parametric EMA / pace×eff / Poisson att-def — few params by design. |
| 2.1.3 Accuracy vs Interpretability | USED | Champ stays interpretable; flexible learners skipped for n and overfit. |
| 2.1.4 Supervised vs Unsupervised | IGNORE | Unsupervised not on money path (see ch12). |
| 2.1.5 Regression vs Classification | USED | Totals are regression for μ; O/U side is classification via `OverProb`. |
| 2.2 Assessing Model Accuracy | CANDIDATE | Residual scale of (actual−exp) is unused; could set `OverProb` / EV (shortlist #2). |
| 2.2.1 Quality of Fit | IGNORE | Train MSE alone does not pick soft spots; we kill on CLV/PnL. |
| 2.2.2 Bias–Variance Trade-Off | USED | Motivates EMA α / fixed w / minEdge; also Tier A anti-overfit. |
| 2.2.3 Classification Setting | USED | Error rate ≠ money; we use EV≥4% + Kelly (related to class-weighted loss). |
| 2.3 Lab: Python | IGNORE | Pedagogy. |
| 2.4 Exercises | IGNORE | Pedagogy. |
| **3 Linear Regression** | — | — |
| 3.1 Simple Linear Regression | USED | Pace×eff and scoring books are linear combinations of team EMAs. |
| 3.1.x Coefficient / model accuracy | CANDIDATE | Same as 2.2: walk-forward residual σ for P(over), not more OLS features. |
| 3.2 Multiple Linear Regression | IGNORE | Extra box features (eFG) already **FAIL** (`PROGRESS` PaceEffEfg). |
| 3.3.1 Qualitative Predictors | TESTED | Rest/B2B as additive/dummy → **REVERT** (BRIEF). |
| 3.3.2 Extensions (interactions, nonlinear) | IGNORE | Style interactions (3PAr edge stored but unused) look like eFG-class overfit. |
| 3.3.3 Potential Problems | USED | Process: leakage/`asOf`, collinearity of line vs model — ridge cal already tried. |
| 3.4 Marketing Plan vignette | IGNORE | Not sports. |
| 3.5 vs KNN | IGNORE | KNN totals with tiny p still high variance; no money case vs EMA. |
| 3.6–3.7 Lab/Exercises | IGNORE | Pedagogy. |
| **4 Classification** | — | — |
| 4.1–4.2 Overview / why not LM | USED | O/U needs probabilities in (0,1) for EV/Kelly. |
| 4.3 Logistic Regression | USED | `TotalsMath.OverProb` = σ(sharpness·(exp−line)); hand-set sharpness. |
| 4.3.x Multiple / multinomial logistic | TESTED | Walk-forward Platt / ridge-logistic on edge → **FAIL** (`PROGRESS`, `OuPlattCalibrator`). |
| 4.4 Generative models (LDA/QDA/NB) | IGNORE | Binary O/U with one feature (edge); logistic already; LDA buys nothing. |
| 4.5 Comparison of classifiers | IGNORE | Accuracy tables ≠ CLV/PnL. |
| **4.6 Generalized Linear Models / Poisson** | **CANDIDATE** | Soccer μ already Poisson/DC/xG; P(over) still logistic — replace with Poisson (or NB) CDF (shortlist #1/#3). |
| 4.7–4.8 Lab/Exercises | IGNORE | Pedagogy. |
| **5 Resampling Methods** | — | — |
| 5.1 Cross-Validation | USED | Evaluation protocol Tier A = rolling-origin walk-forward (`PLAN.md`); not a model feature. |
| 5.1.x Validation / LOO / k-fold | USED | Same — prefer rolling origin over k-fold shuffle (time series). |
| **5.2 The Bootstrap** | **CANDIDATE** | Uncertainty gate on edge / σ for Kelly (shortlist #4). |
| 5.3–5.4 Lab/Exercises | IGNORE | Pedagogy. |
| **6 Linear Model Selection and Regularization** | — | — |
| 6.1 Subset Selection | IGNORE | Feature soup not our bottleneck; eFG subset already failed. |
| 6.2.1 Ridge | TESTED | Ridge-logistic cal **FAIL**; EB pooling (shrinkage cousin) **REVERT**. |
| 6.2.2 Lasso | IGNORE | p≪n team-stat regime; sparsity for selection not needed. |
| 6.2.3 Tuning parameter | USED | α / w / minEdge chosen on train then holdout — overfit risk (phase 3a). |
| 6.3 PCR / PLS | IGNORE | No high-dim box panel on money path. |
| 6.4 High dimensions | IGNORE | We are not in p≫n for champ models. |
| 6.5–6.6 Lab/Exercises | IGNORE | Pedagogy. |
| **7 Moving Beyond Linearity** | — | — |
| 7.1 Polynomial Regression | IGNORE | Poly(edge) ≈ more cal params; Platt already failed. |
| 7.2 Step Functions | IGNORE | Edge bins → many params vs n bets. |
| 7.3–7.5 Basis / regression / smoothing splines | IGNORE | Flexible μ(t) overfits league seasons. |
| 7.6 Local Regression | IGNORE | Local P(over\|edge) ≈ nonparametric Platt; high overfit. |
| 7.7 GAMs | IGNORE | Too many smooth terms for our n and ≤3-param rule. |
| 7.8–7.9 Lab/Exercises | IGNORE | Pedagogy. |
| **8 Tree-Based Methods** | IGNORE | RF/boosting/BART for totals: many params, weak CLV story vs 2–3 EMA params. |
| 8.1–8.2 Trees / bagging / RF / boosting / BART | IGNORE | Same. |
| 8.3–8.4 Lab/Exercises | IGNORE | Pedagogy. |
| **9 Support Vector Machines** | IGNORE | Margin classifiers don’t output well-calibrated EV; no money path over logistic/Poisson. |
| 9.1–9.5 SVM family | IGNORE | Same. |
| 9.6–9.7 Lab/Exercises | IGNORE | Pedagogy. |
| **10 Deep Learning** | IGNORE | n and stationarity kill NN/RNN for league totals; violates few-param rule. |
| 10.1–10.8 NN / CNN / RNN / double descent | IGNORE | Same. |
| 10.9–10.10 Lab/Exercises | IGNORE | Pedagogy. |
| **11 Survival / Censoring** | IGNORE | Games are fully observed scores; no censoring problem on totals. |
| 11.1–11.7 Survival topics | IGNORE | Same. |
| 11.8–11.9 Lab/Exercises | IGNORE | Pedagogy. |
| **12 Unsupervised Learning** | IGNORE | PCA/clustering don’t set line edge or size bets. |
| 12.1–12.4 PCA / matrix completion / clustering | IGNORE | Completion might impute missing odds but not a soft-spot model. |
| 12.5–12.6 Lab/Exercises | IGNORE | Pedagogy. |
| **13 Multiple Testing** | USED | Process only: many w×edge×α trials → FWER/FDR mindset; feeds phase 3a / Tier A, not a bet feature. |
| 13.1–13.4 Hypothesis testing / FWER / FDR / BH | USED | Same — selection-bias awareness when ranking experiments. |
| 13.5 Re-sampling FDR etc. | IGNORE | Formal BH on backtests secondary to CLV/PnL protocol. |

---

## ESL — chapter verdicts

| Ch | Title | Verdict | Reason |
|---|---|---|---|
| 1 | Introduction | IGNORE | Overview. |
| 2 | Overview of Supervised Learning | USED | Bias–variance / LS vs NN framing already shapes EMA vs flexible fits. |
| 3 | Linear Methods for Regression | TESTED / IGNORE | Shrinkage/subset = ridge/EB path already tried; LARS/lasso not needed. |
| 4 | Linear Methods for Classification | TESTED | Logistic/LDA path; L1-logistic ≈ failed ridge-logistic family. |
| 5 | Basis Expansions and Regularization | IGNORE | Splines/RKHS for μ or P(over) too flexible. |
| 6 | Kernel Smoothing Methods | IGNORE | Kernel density P(over) high variance; mixture densities interesting but >3 params if fit freely. |
| 7 | Model Assessment and Selection | USED | Optimism, effective df, right/wrong CV → Tier A rolling origin; phase 6 training review. |
| 8 | Model Inference and Averaging | **CANDIDATE** | §8.8 stacking/model averaging → season-fold blend weight w (shortlist #5). Bagging/EM/MCMC: IGNORE for hot path. |
| 9 | Additive Models, Trees, Related | IGNORE | GAM/MARS/trees — same as ISLP 7–8. |
| 10 | Boosting and Additive Trees | IGNORE | GBM for totals overfits; not few-param. |
| 11 | Neural Networks | IGNORE | Same as ISLP 10. |
| 12 | SVM and Flexible Discriminants | IGNORE | Same as ISLP 9. |
| 13 | Prototype Methods and NN | IGNORE | kNN totals not competitive with EMA. |
| 14 | Unsupervised Learning | IGNORE | No money path. |
| 15 | Random Forests | IGNORE | Same as trees. |
| 16 | Ensemble Learning | IGNORE | Rule ensembles / heavy stacking overkill; light stacking covered under ch8 candidate. |
| 17 | Undirected Graphical Models | IGNORE | Graph selection irrelevant to O/U EV. |
| 18 | High-Dimensional p≫N | IGNORE | Not our regime; FDR notes duplicate ISLP 13 (process). |

---

## Statistical Learning with Sparsity — chapter verdicts

| Ch | Title | Verdict | Reason |
|---|---|---|---|
| 1 | Introduction | IGNORE | Motivation for ℓ1; we are not in sparse high-p. |
| 2 | The Lasso for Linear Models | IGNORE | No feature-selection money lever left after eFG FAIL. |
| 3 | Generalized Linear Models (sparse) | IGNORE | Sparse Poisson/logistic for many covariates — we have 1–2 signals (model vs line). Poisson *probability* idea is ISLP 4.6, not lasso-GLM. |
| 4 | Generalizations of the Lasso | IGNORE | Elastic net / group / fused / trend filter — no ordered high-p signal. |
| 5 | Optimization Methods | IGNORE | Solver theory; no product code path. |
| 6 | Statistical Inference (lasso) | IGNORE | Post-selection inference ≠ bet sizing. |
| 7 | Matrix Decompositions / Completion | IGNORE | Netflix-style completion ≠ totals edge. |
| 8 | Sparse Multivariate Methods | IGNORE | Sparse PCA/CCA/clustering unused. |
| 9 | Graphs and Model Selection | IGNORE | Graphical lasso N/A. |
| 10 | Signal Approximation / Compressed Sensing | IGNORE | Not our observation model. |
| *(time-decay / EMA sparsity of influence)* | USED | Already extracted earlier: EMA α KEEP MLS/Ligue1/LigaMX (`reports/library_sparsity_timedecay.md`) — not a new candidate. |

---

## Ranked CANDIDATE shortlist (max 5)

### 1. Poisson CDF `OverProb` for soccer (replace logistic sharpness)

| | |
|---|---|
| **Hypothesis** | Champ soccer already produces μ = E[goals] (xG/goals EMA or DC λ_h+λ_a) but sizes bets with `σ(1.2·(exp−line))`. Under independent Poisson, P(total > line) is the Poisson CDF (half-line / push handled explicitly). That recalibrates EV≥4% and ¼ Kelly without fitting Platt a,b. |
| **League(s)** | MLS, Ligue 1, Liga MX, Serie A (all PASS soccer). |
| **Code** | New helper next to `TotalsMath.OverProb`; call sites `MlsTotalsBacktest` / `SoccerLeagueTotalsBacktest` / `StakeLaneHoldoutSim` / `DailySlate` soccer branch. DC already exposes λ via `DixonColesBook.ExpectedLambdas` — total ~ Poisson(λ_h+λ_a) if independent. |
| **# new params** | **0** (pure Poisson). |
| **Tier A** | Rolling-origin by season: train seasons &lt; k, test k, for each k before burned holdout (MLS ≤2024 folds; EU soccer seasons before 2024–25). Same tickets gate optional: (A) replace P only, keep minEdge/w; (B) allow EV gate to change. PASS: mean CLV ≥ champ, PnLΔ>0 in majority of folds, n≥40/league. Also log-loss/Brier + Kelly-lane PnL on **same** tickets (PLAN §6). **Do not score MLS 2026 / soccer 2025–26.** |
| **Money** | Better P(over) → fewer bad EV≥4% entries and correctly sized Kelly on Kalshi/RH/Crypto lanes. |
| **Overfit risk** | **Low** (0 params). Model error in μ still dominates; independence ignores DC ρ (totals rarely care). |

### 2. Walk-forward residual-σ Normal CDF for NBA `OverProb`

| | |
|---|---|
| **Hypothesis** | NBA sharpness 0.25 is arbitrary. Estimate σ² of (actual_total − blended_exp) walk-forward (EMA of squared residual, one α or fixed window), set P(over)=Φ((exp−line)/σ) (or σ(edge/σ)). Materially different from failed Platt (a,b on edge): one scale from residuals, not class-cal on bet outcomes. |
| **League(s)** | NBA (provisional KEEP rules). |
| **Code** | `TotalsMath` + `TotalsEdgeBacktest` / `DailySlate` NBA path; residuals from Games scores + existing PaceEff blend in `ChampConfig`. |
| **# new params** | **1** (residual EMA α or window length); or **0** if use expanding walk-forward MLE σ with no tune. Prefer 0–1. |
| **Tier A** | Rolling seasons with TOTAL odds (db has 2008–2021, 2025; skip gap years). Same-ticket + EV-gate variants; Kelly-lane PnL + Brier/LL. Holdout 2024–25 = Tier B only. |
| **Money** | Same soft spots, better size; EV gate may add/drop marginal 6-pt edges. |
| **Overfit risk** | **Low–medium** if α swept on burned holdout — freeze α on Tier A only. |

### 3. NegBin / overdispersed Poisson P(over) for soccer

| | |
|---|---|
| **Hypothesis** | If #1 Poisson CDF is overconfident (Brier/LL worse, Kelly oversizes), one dispersion φ (NB variance = μ+φμ²) fixes tails. Still distributional — not Platt. |
| **League(s)** | Same soccer PASS set; only run if #1 Tier A mixed. |
| **Code** | Extend Poisson helper; φ fit walk-forward on past score residuals only (`asOf`). |
| **# new params** | **1** (φ), league-specific or shared. |
| **Tier A** | As #1; compare nested to Poisson-0. Count both as trials in the log. |
| **Money** | Kelly on Crypto/Kalshi sensitive to tail P(over); dispersion → less overbetting. |
| **Overfit risk** | **Medium** — φ can soak μ bias; require Tier A majority + nested improvement. |

### 4. Bootstrap / SE-scaled edge gate

| | |
|---|---|
| **Hypothesis** | Fixed minEdge (NBA 6, soccer 0.25–0.35) ignores uncertainty in EMA state. Residual bootstrap or analytic SE of exp: bet only if \|exp−line\| ≥ z·SE (optionally max with current minEdge). |
| **League(s)** | NBA + soccer PASSes. |
| **Code** | Gate in backtest runners + `DailySlate` after exp blend; SE from recent residual variance (#2) or block bootstrap of last W games’ updates. |
| **# new params** | **1** (z or W). |
| **Tier A** | Rolling origin; primary metric CLV/PnL (bet set **will** change). Log variant count. |
| **Money** | Drop false edges → higher CLV per ticket; may reduce n (OK if PnL↑). |
| **Overfit risk** | **Medium** — z interacts with minEdge; do not grid both freely. |

### 5. Season-fold stacking weight w (model vs open)

| | |
|---|---|
| **Hypothesis** | ESL §8.8: treat market open and model as two predictors; each Tier A fold picks w* on **past folds only** (stacking), instead of one frozen champ w from a large grid that burned selection budget. |
| **League(s)** | NBA + soccer (w=0.5 / 0.4 today in `*ChampConfig`). |
| **Code** | Sweep helpers already grid w — change selection to pure rolling stack; runtime `DailySlate` uses last locked w*. |
| **# new params** | **1** (w), re-estimated per fold — not new family. |
| **Tier A** | Nested: inner choose w on history, outer test next season. Compare to frozen champ w. |
| **Money** | Better blend when open sharpness drifts → +CLV. |
| **Overfit risk** | **Medium–high** historically (w already heavily swept). Only keep if rolling stack beats frozen w on Tier A **without** touching Tier C. Else IGNORE as re-packaging. |

---

## Explicit non-candidates (avoid rework)

| Idea | Why not |
|---|---|
| Platt / ridge-logistic on edge | **TESTED FAIL** (`PROGRESS`, `OuPlattCalibrator`) |
| Lasso/EB pooling of team strengths | **REVERT** / wrong sparsity regime |
| Trees / RF / boosting / DL / SVM / GAM / splines | Too many params; no clear +CLV path |
| Unsupervised / survival / graphical / compressed sensing | No betting mechanism |
| Accuracy metrics without EV/Kelly path | Money filter → IGNORE |

---

## Data readiness (shortlist)

| Candidate | Data in `possiblymaybe.db`? |
|---|---|
| #1 Poisson soccer P(over) | Yes — Games + TOTAL odds + MlsGameXgs / DC inputs |
| #2 NBA residual σ | Yes — Games + GameBoxes + TOTAL (note season gaps) |
| #3 NegBin φ | Same as #1 |
| #4 Bootstrap gate | Same residuals/odds |
| #5 Stacking w | Same (process change on existing sweeps) |
