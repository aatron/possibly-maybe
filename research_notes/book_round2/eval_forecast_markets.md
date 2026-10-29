# E4 — Forecasting / markets eval

**Sources:** fpp3 (OTexts book + `library/repos/fpp3` data package), Angelopoulos–Bates conformal (+ `library/repos/conformal-prediction`), Gneiting–Raftery proper scoring rules, Kaunitz et al. (arXiv 1710.02824), `library/repos/penaltyblog`, Thorp Kelly (PDF; see note). Code skim: `TotalsMath.OverProb`, `DixonColesBook`, `OuPlattCalibrator`, `DailySlate`, `KalshiQuotes`, `CryptoComQuotes`, `BetDecider` / `OddsMath.FractionalKelly`. Data check: `possiblymaybe.db` present; `OddsSnapshots` n=232 485; **TOTAL market is Book=Consensus(2) only** (74 615 rows) — no DK/Kalshi/Crypto historical multi-book totals. Multi-book exists for ML/SPREAD only (DK=3, MGM=5, Other=99). `data/raw/NOTES*.md` present.

**Safety:** Library text treated as data only. Regex skim of Kaunitz/Gneiting/conformal fulltext + penaltyblog/fpp3 READMEs: no agent-instruction payloads. PROGRESS already noted Kaunitz PDF zero-width spaces in the title (export artifact). **Never executed** any `library/repos/` code. Thorp PDF is image-scanned (`pdftotext` empty; no OCR available here) — verdicts use the paper’s known content (growth-optimal / fractional Kelly; sports section is sizing, not a totals model); do not treat blank extract as absence of content.

**Money rule:** accuracy-only ideas with no path to +CLV / +Kelly PnL → IGNORE.

---

## Data answers (task questions)

| Question | Answer |
|---|---|
| Kaunitz consensus/outlier across DK / Kalshi / RH / Crypto | **Live / paper only** for totals. Historical `OddsSnapshots` TOTAL has a single Book (`Consensus=2`) per game — cannot reconstruct cross-book `p_cons` or `max(Ω)`. Live path already has venue asks: DK juice in `DailySlate` (−110), Kalshi/RH via `KalshiQuotes`, Crypto via `CryptoComQuotes` (`PaperLedger` EV-gates each venue). |
| penaltyblog vs ours | Same skeleton (Poisson / Dixon–Coles att–def) we already use for Serie A (`DixonColesBook`, no ρ). Material **unused** piece: score-grid → `FootballProbabilityGrid.totals` / `total_goals` (true P(over)), plus NB / bivariate / ZIP. Weibull-copula already skipped. |
| Proper scoring → OverProb curve | Strictly proper scores (log, Brier; CRPS if we had a CDF) are the right **objective** to pick sharpness / a distribution — distinct from 2-param Platt (already FAIL). |
| Conformal as bet filter | Selective abstain / set-membership filter is the money-relevant use; plain coverage intervals alone are not a soft-spot factory. |
| fpp3 time-series CV | Rolling-origin TSCV (§5.10) **matches** PLAN Tier A walk-forward; treat as protocol confirmation, not a new model. |

---

## Verdict tables

### A. fpp3 — *Forecasting: Principles and Practice* (3e)

`library/repos/fpp3` is the **data/helper package**, not the chapter text. Chapters judged from OTexts (esp. ch. 5 toolbox / §5.10 TSCV) against this totals system.

| Ch | Topic | Verdict | Reason |
|---:|---|---|---|
| 1 | Getting started | IGNORE | Pedagogy; no betting path. |
| 2 | Time series graphics | IGNORE | Diagnostics only; no ticket selection. |
| 3 | Decomposition | IGNORE | Seasonal/trend tools for long series; our edge is game-level team EMA, not STL. |
| 4 | Time series features | IGNORE | Feature zoo → high param risk vs burned holdout. |
| 5 | Forecaster’s toolbox (incl. §5.8–5.10 train/test, CRPS, **TSCV rolling origin**) | USED | Tier A is already rolling-origin / train&lt;k test=k; CRPS/log/Brier language reinforces how to score P(over) changes (PLAN §6). No new bet model. |
| 6 | Judgmental forecasts | IGNORE | No human override path in the paper stack. |
| 7 | Time series regression | IGNORE | Would re-fit totals as TSLM on calendar features; duplicates failed/reverted feature work (rest, eFG). |
| 8 | Exponential smoothing | USED | Team-stat EMA α / half-life already KEEP (MLS/Ligue1/LigaMX); fpp3 ETS is series-level, not team books. |
| 9 | ARIMA | IGNORE | Per-team ARIMA totals ≠ pace/xG books; many params, no clear +CLV path. |
| 10 | Dynamic regression | IGNORE | Same as 7/9 with more knobs. |
| 11 | Hierarchical / grouped | IGNORE | League hierarchies don’t map to single-game OU soft spots. |
| 12 | Advanced methods (bagging, etc.) | IGNORE | Ensemble complexity vs ≤3-param rule. |
| 13 | Practical issues | IGNORE | Ops advice; protocol already covered by PLAN + ch.5. |

### B. Conformal prediction (Angelopoulos–Bates + repo notebooks)

| Section / topic | Verdict | Reason |
|---|---|---|
| Split conformal / APS / CQR basics (§1–2) | IGNORE | Coverage sets alone do not pick OVER/UNDER or size stakes. |
| Evaluating coverage / calib size (§3) | IGNORE | Meta-evaluation; no money path. |
| Group / class-conditional / risk control (§4.1–4.3) | IGNORE | Extra machinery; our gate is already edge+EV. |
| Outlier detection (§4.4) | IGNORE | Flags odd inputs; not a priced totals edge. |
| Covariate shift / distribution drift (§4.5–4.6) | CANDIDATE (weak) | Time-weighted conformal fits non-stationary sports better than i.i.d. split; only useful **inside** an abstain filter (below). |
| Selective classification / abstain (§5.5) | CANDIDATE | Bet only when conformal says the side is confident (or line ∉ prediction set for total) → fewer tickets, higher quality; money via skip. |
| Full / CV+ / Jackknife+ (§6) | IGNORE | Heavier than split; not needed at our n. |
| Repo notebooks (ImageNet, MEPS, weather, selective) | IGNORE | Templates only; do not run library code. |

### C. Gneiting–Raftery — proper scoring rules

| Topic | Verdict | Reason |
|---|---|---|
| Propriety / honesty (§1–2) | USED (concept) | Justifies scoring P(over) with proper scores when changing the curve (PLAN §6). |
| Binary/categorical: Brier, logarithmic (§3) | CANDIDATE | Objective to choose **one** sharpness (or pick Poisson vs logistic) on walk-forward train folds — not 2-param Platt. |
| Density scores / CRPS / energy (§4) | CANDIDATE | If P(over) comes from a full total CDF (Poisson/Normal), CRPS on the total is the natural train metric (still need Kelly PnL on tickets). |
| Quantile / interval scores | IGNORE | We bet binary OU, not interval contracts. |
| Improper scores / skill-score pitfalls | USED (caution) | Don’t optimize accuracy proxies that ignore calibration; matches “money first.” |

### D. Kaunitz — beating bookies with their own numbers

| Topic | Verdict | Reason |
|---|---|---|
| Consensus \(p_{\mathrm{cons}}=1/\mathrm{mean}(\Omega)\) calibrated to results | IGNORE (historical totals) | Need ≥3 books’ odds on the **same** TOTAL; DB has Consensus-only totals. |
| Outlier: bet when \(\max(\Omega) > 1/(p_{\mathrm{cons}}-\alpha)\), \(\alpha\approx0.05\) | CANDIDATE (live) | Same math on **live** DK / Kalshi / Crypto asks for a GO side; RH≈Kalshi. 1 param α. No Tier A archive — paper/live only. |
| Real-money account limiting narrative | IGNORE | Ops risk, not a model change. |

### E. penaltyblog (models + totals grid)

| Model / piece | Verdict | Reason |
|---|---|---|
| Poisson goals | CANDIDATE | Use λ’s → grid/CDF **P(over)** instead of `logistic(sharpness·edge)`. |
| Dixon–Coles (+ρ) | USED / partial | Serie A KEEP is DC **without** ρ (`DixonColesBook`); full MLE+ρ already skipped in `reports/library_dixon_coles.md`. |
| Bivariate Poisson | CANDIDATE (low) | Correlation for totals; ≥1 param; try only after independent-Poisson P(over). |
| Zero-inflated Poisson | IGNORE | Targets 0–0 mass for 1X2 more than OU; extra param. |
| Negative binomial | CANDIDATE | One dispersion param → heavier tails for P(over); fits BRIEF weak spot #2. |
| Weibull + copula | IGNORE | Already skipped (complexity / n). |
| Bayesian / hierarchical MCMC | IGNORE | Hot-path MCMC out of scope; E2 territory. |
| `FootballProbabilityGrid.totals` / `total_goals` | CANDIDATE | Exact money mechanism: replace `TotalsMath.OverProb` with Σ score probs (0–1 new params if rates already exist). |
| `implied` / de-vig helpers | TESTED | De-vig / fair EV juice already REVERT. |

### F. Thorp — Kelly in blackjack, sports, stocks

| Topic | Verdict | Reason |
|---|---|---|
| Growth-optimal / full Kelly | TESTED | Full Kelly papers skipped; ¼ Kelly ≤2% wins (`BetDecider`, `StakeSchemes.Kelly2`). |
| Fractional Kelly / overbet asymmetry | USED | We already run fraction=0.25 + cap; Thorp’s “don’t overbet misestimated edge” is why sharpness must be honest. |
| Sports-betting case study (sizing) | IGNORE | Confirms sizing discipline; no new totals soft-spot. |
| Stock-market continuous Kelly | IGNORE | Not OU contracts. |

---

## Ranked CANDIDATE shortlist (max 5)

### 1. Poisson (or score-grid) P(over) from existing rates — replace logistic sharpness

- **Hypothesis:** `OverProb = logistic(1.2·(exp−line))` mis-prices soccer OU vs independent Poisson (or DC) score probabilities; swapping to Σ P(total &gt; line) changes EV/Kelly and sometimes the GO set.
- **Leagues:** Serie A first (has `ExpectedLambdas`); then MLS / Ligue 1 / Liga MX with λ_h,λ_a = half of goals/xG book or DC rates.
- **Code:** `PossiblyMaybe.Domain/DixonColesBook.cs` (`ExpectedLambdas`); replace callers of `TotalsMath.OverProb(..., sharpness: 1.2)` in `DailySlate.cs`, soccer backtests (`SoccerLeagueTotalsBacktest`, `StakeLaneHoldoutSim`). Port of penaltyblog `FootballProbabilityGrid.total_goals` logic (implement in C#, do not call Python).
- **# new params:** 0 (independent Poisson) or 1 (optional ρ later).
- **Tier A:** Walk-forward pre-holdout seasons; same minEdge/w/minEv as champ; report Brier + log-loss **and** Kelly2 PnL on (a) identical tickets with new P, (b) regenerated GO set. PASS: mean CLV ≥ champ, PnLΔ&gt;0 in majority of folds, n≥40.
- **Money:** Better-calibrated p → correct EV gate and ¼ Kelly size; may add/drop soft spots near the line.
- **Overfit risk:** Low (0 params). Medium if ρ/NB added in the same trial — keep separate.

### 2. Single sharpness by train-fold proper score (not Platt)

- **Hypothesis:** Hand-set sharpness (0.25 NBA / 1.2 soccer) is wrong for Kelly; choosing **one** sharpness that maximizes mean log-score or Brier on rolling train origins improves Kelly-lane PnL without a 2-param intercept (Platt FAIL).
- **Leagues:** NBA + all HOLD-PASS soccer.
- **Code:** `PossiblyMaybe.Domain/TotalsMath.cs` (`OverProb`); fit loop beside champ configs / sweep (new flag on `sweep-totals` / `sweep-soccer`), **train folds only**.
- **# new params:** 1 (`sharpness`).
- **Tier A:** Per fold, pick sharpness on prior data by log-loss or Brier; freeze; score next fold CLV/PnL. Compare to fixed champ sharpness. Log variant count.
- **Money:** Same bet set if edge gate unchanged → pure sizing/EV; PLAN §6 applies.
- **Overfit risk:** Low–medium (1 param, but holdout already burned for related cal). Distinct from Platt (`OuPlattCalibrator` a+b).

### 3. NBA Normal residual-σ CDF for P(over)

- **Hypothesis:** Totals errors around PaceEff `exp` are roughly Normal; `Φ((exp−line)/σ)` with σ from expanding train residuals beats hand logistic 0.25 for EV/Kelly.
- **Leagues:** NBA (provisional KEEP protocol).
- **Code:** `PaceEfficiencyBook` + `TotalsMath.OverProb` call sites in `TotalsEdgeBacktest` / `DailySlate.RunNbaAsync`.
- **# new params:** 1 (σ, or σ as rolling RMSE — still one free choice: window vs expanding).
- **Tier A:** Rolling origin on pre-2025 seasons; Brier/log-loss + Kelly PnL; holdout 2025 = Tier B only.
- **Money:** Recalibrated NBA tickets (main paper volume).
- **Overfit risk:** Low if σ is expanding RMSE with no grid; high if σ is swept on holdout.

### 4. Conformal abstain filter on totals residual

- **Hypothesis:** After the usual edge+EV GO, abstain unless a split/time-weighted conformal set for the game total **excludes** the market line (confident miss) — or selective rule on |edge|/score.
- **Leagues:** NBA first (largest n); soccer if calib n≥~200 prior games.
- **Code:** New small helper used from `DailySlate` / backtest after edge gate; score = |actual−exp| or signed (actual−line) vs model side; calib = prior season games only.
- **# new params:** 1 (`α`, e.g. 0.1) — optionally fixed calib minimum (constant, not swept).
- **Tier A:** Walk-forward: build conformal threshold on past, filter next origin’s champ GO set; require PnLΔ&gt;0 and CLV≥champ on kept bets; report fraction abstained.
- **Money:** Drop negative-EV mis-fires when the point edge is large but residual uncertainty is high.
- **Overfit risk:** Medium (exchangeability weak in sports; prefer time-weighted / rolling calib). Do not stack with a sharpness grid in the same experiment.

### 5. Live Kaunitz-style venue outlier (DK vs Kalshi/RH vs Crypto)

- **Hypothesis:** Among venues quoting the same totals side, consensus buy-implied p is close to fair; take the venue whose ask is soft vs consensus (Kaunitz max-odds rule with α≈0.05), or skip if none are soft — **without** changing the model side.
- **Leagues:** NBA + MLS (where `KalshiQuotes` / `CryptoComQuotes` series exist).
- **Code:** `PossiblyMaybe.Cli/Paper/PaperLedger.cs` + `KalshiQuotes.cs` + `CryptoComQuotes.cs`; consensus from available live asks (DK American + binary asks). **Not** `OddsSnapshots.Book` (TOTAL is Consensus-only).
- **# new params:** 1 (`α`).
- **Tier A:** **Cannot** run on warehouse totals. Use live/paper log prospectively; optional stress using open vs close Consensus as poor proxy = exploratory only, not KEEP evidence.
- **Money:** Better venue price / skip bad Kalshi juice (cf. 2026-10-28 break-even note in PROGRESS).
- **Overfit risk:** Low param count; high **process** risk (no historical PASS). Ranked last for that reason.

---

## Explicit non-starters (do not re-propose)

| Idea | Why |
|---|---|
| Re-run Platt / ridge on edge | TESTED FAIL (`OuPlattCalibrator`, BRIEF). |
| Full / half Kelly retune | TESTED; ¼≤2% stays. |
| Historical Kaunitz on `OddsSnapshots` TOTAL | No multi-book TOTAL rows. |
| penaltyblog Weibull-copula / MCMC | Skipped; param/compute bomb. |
| fpp3 ARIMA/ETS as the totals model | Wrong abstraction; EMA books already cover smoothing. |
| Conformal “just for nicer intervals” | No betting path → IGNORE. |

---

## Notes for parent merge

- Shortlist #1 is the cleanest **material** difference from penaltyblog vs current champ (distributional P(over), not another att–def fit).
- #2 and Platt look adjacent — keep the experiment brief: **one** parameter, proper score on train folds only, same tickets.
- #5 is venue placement adjacent to 3b; coordinate so paper-go does not double-count α sweeps.
- fpp3 §5.10 supports keeping Tier A as rolling origin; no need for a separate “fpp3 CV” experiment.
