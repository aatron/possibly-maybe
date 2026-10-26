# PossiblyMaybe

NBA-first betting lab: find a real edge (CLV), then take small advised bets. Money first; UI/Azure only after gates pass. See [`PLAN.md`](PLAN.md) and [`PROGRESS.md`](PROGRESS.md).

**Current money path (Gate 2 paper):** NBA **totals** — 40% open + 60% scoring EMA, `|exp−line|≥3.5`, EV≥4%. `advise-ou` → `paper-bet` OVER/UNDER → `settle` → `paper`. Holdout **+8.4u / 320 bets** (2024–25). No real cash until Gate 2b.

Methodology reference: *Introduction to Statistical Learning* (ISLP). This README defines the **math we actually use** — a small ISLP subset plus betting math — not every chapter of the book.

When `PLAN.md` changes those essentials, **update this glossary in the same change**.

---

## Math glossary

### From ISLP (required essentials)

| Term | Definition | How we use it |
|---|---|---|
| **Bias–variance trade-off** | A model that is too simple systematically misses the truth (**bias**). A model that is too flexible fits noise in the training sample and fails on new data (**variance**). Good prediction balances both. | Start with market → Elo → refs. Add complexity only when out-of-sample **CLV** improves; on a tie, keep the simpler champ. |
| **Training set** | Data used to fit parameters (Elo ratings, logistic coefficients, ref effects, etc.). | Only games **before** the walk-forward test window; features built with `asOf` so future info cannot leak in. |
| **Test / holdout set** | Data held back to judge generalization. We lock **one full season** as final holdout and look at it **once**. | Never tune thresholds or features on the holdout. |
| **Out-of-sample (OOS)** | Performance on data not used to fit that step (walk-forward folds or holdout). | Champ promotion and kill gates use OOS CLV/P&L, not in-sample fit. |
| **Overfitting** | The model looks great on training history but does not predict new games (or new prices) well. | Ban random K-fold on time series; require walk-forward + CLV; BH before shipping mined slices. |
| **asOf / leakage** | Building a feature with information that was not knowable at decision time (e.g. final score, or a ref crew not yet published). | `BuildFeatures(gameId, asOf)` must match what a bettor could know then. |
| **Walk-forward validation** | Time-ordered evaluation: train on the past, test on the next week/season slice, roll forward. Unlike random **K-fold CV**, it respects time. | Only allowed validation scheme for games. |
| **Logistic regression** | Model for a binary outcome (e.g. home win) that outputs a probability via a logistic (sigmoid) link on a linear score. | First learned model after Elo+refs (post-edge), if needed. |
| **Ridge / lasso (regularization)** | Penalties that shrink coefficients (**ridge** = L2; **lasso** = L1, can zero out features) to reduce variance when many predictors are correlated or weak. | Try shrinkage on logistic feature sets before jumping to trees. |
| **Log loss (cross-entropy)** | Average −[y log p + (1−y) log(1−p)]. Punishes confident wrong probabilities. Lower is better. | Reported on walk-forward slices; secondary to CLV/P&L for promotion. |
| **Brier score** | Mean squared error of probabilities: average (p − y)². Lower is better; rewards calibrated probs. | Same role as log loss — secondary quality metric. |
| **Calibration** | When the model says “30%,” that event should happen about 30% of the time in OOS data. | Check before trusting stake sizes; recalibrate if needed post-edge. |
| **Tree ensembles / boosting (e.g. LightGBM)** | Many weak decision trees added in sequence to correct residual errors; flexible but easy to overfit. | Only if it beats regularized logistic **and** the Elo+refs champ on OOS CLV. |
| **Multiple testing** | Running many hypothesis tests (many refs × players, many situational filters) inflates false discoveries. | |
| **Benjamini–Hochberg (BH)** | FDR-controlling procedure: rank p-values and threshold so the expected fraction of false positives among “significant” hits stays bounded. | Required before a mined ref–player / situational slice becomes a shipped feature. |

### Betting math (required)

| Term | Definition | How we use it |
|---|---|---|
| **American odds** | US quote: **−110** means risk $110 to win $100; **+140** means risk $100 to win $140. | Domain converts to/from decimal/implied prob; Gate 0 tests include −150 / +140 / −110. |
| **Decimal odds** | European quote: stake × decimal = total return (stake + profit). Implied prob ≈ 1/decimal before vig. | Internal math often uses decimal. |
| **Implied probability** | Market’s probability baked into the price (before removing juice). | Starting point for market baseline. |
| **Vig / juice / hold** | Bookmaker margin: two-sided prices imply probabilities that sum to **> 100%**. | We do not treat raw implied probs as fair. |
| **No-vig (devig) probability** | Fair-ish probabilities after removing the overround (e.g. normalize the two sides to sum to 1). | Market baseline feature and blend input. |
| **Expected value (EV)** | For a unit stake idea: (winProb × profitIfWin) − (loseProb × stake), or equivalent. Positive EV means a bet is favorable *if* winProb is right. | `IBetDecider` requires EV above a minimum at the **entered** price. |
| **Edge** | Your win probability minus the no-vig (or price-implied) probability — or the EV expressed as a rate. | We only bet when edge clears thresholds after costs. |
| **Kelly criterion** | Stake fraction that maximizes long-run log growth: roughly edge/odds for a binary bet (full Kelly). Very aggressive in practice. | We use **fractional Kelly** (default **¼ Kelly**) plus per-bet and per-day caps and a drawdown breaker. |
| **Closing line** | The last sharp (or consensus) price before the game starts. | Yardstick for whether you beat the market. |
| **Closing line value (CLV)** | You got a better price than the close (e.g. bet +150, closed +130). Consistent positive CLV is the leading sign of real edge; short-term W/L is noisy. | **Primary promotion metric.** Always vs sharp/consensus close, not vs a soft open. |
| **P&L** | Profit and loss in money terms (paper or real ledger). | Secondary to CLV for “do we have edge?”; primary for bankroll tracking. |
| **Sharp vs soft book** | **Sharp** books (e.g. Pinnacle-class) are efficient and limit winners; **soft** books (e.g. retail DK) are where recreational prices lag. | Sharp = CLV/blend reference; soft = place bets (often via manual price entry). |
| **Line shopping** | Taking the best available number across books. | In scope when one soft price kills a thin edge. |
| **Blend weight (w)** | Final probability ≈ w × model + (1−w) × sharp market. Keeps the model from ignoring the market. | Swept (~0.2–0.4 default range); chosen for wealth/CLV, not aesthetics. |

### Related app terms (short)

| Term | Definition |
|---|---|
| **Elo** | A **team strength rating** updated after every game: the winner takes rating points from the loser (more if the underdog wins). The gap between two teams’ Elo ratings converts to a **win probability**. Named after Arpad Elo (chess); widely used in sports. In this app it is the Gate **1a** baseline *before* ref/load features — not the same as betting odds, though we compare it to the market. |
| **B2B (back-to-back)** | Two games on consecutive days; tiredness / rotation risk. Known from the schedule before tip. | Gate **1c** feature. |
| **Travel / rest** | Distance or time zones traveled, and days since last game. | Gate **1c** features. |
| **Recent minutes / load** | Per-player minutes (and simple usage) from recent **previous** games — e.g. last 1/3/5/7 — to inform fatigue/rotation for the **current** slate game. Never use minutes from the game being predicted. | Gate **1c** features. |
| **Moneyline vs spread** | **ML** = bet who wins (primary). **Spread** = bet margin vs a number (harder; tracked in parallel, secondary for cash). | |
| **Champ** | The model/config that wins the OOS CLV comparison (1a vs 1b, later vs ML). |
| **Paper bet** | Simulated bet at a logged price; no real money. |
| **Advise** | CLI/UI flow: you enter the odds/line you can bet → go/no-go + stake. |
| **Holdout season** | One full season frozen before tuning; scored once at the end to test generalization. |
| **Sharp close series** | History/feed of last sharp or consensus prices before tip; used to compute CLV. |

---

## Docs

| File | Role |
|---|---|
| [`PLAN.md`](PLAN.md) | Gates, architecture, Composer packs, ISLP essentials table |
| [`PROGRESS.md`](PROGRESS.md) | Resume after interruptions |
| [`HANDOFF.md`](HANDOFF.md) | Earlier design discussion (news, decision layer) |
