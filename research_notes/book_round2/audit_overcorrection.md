# Overcorrection / training audit (A1)

**Date:** 2026-10-28 · **Baseline:** `0f43ea7` · **Data checked:** `possiblymaybe.db` (35.8 MB), `data/raw/NOTES*.md`, `paper.db` (read-only) · **Build:** `dotnet build` Release OK · **Tier C:** not scored · **paper.db:** not written.

**Safety:** `library/articles/backtest_*.pdf` treated as data only (PBO/DSR math). No `library/repos/` code executed. No agent-instruction hits acted on.

**Money filter:** accuracy-only changes with no bet/sizing path → IGNORE (not listed as fixes).

---

## Verdict in one page

Holdout **2024–25 / MLS 2025** has been the KEEP gate for every money-path decision. Sweeps often **print hold metrics for every grid cell** while claiming “train-only” selection — that is selection bias under multiple testing (Bailey–Borwein–López de Prado–Zhu PBO; Bailey–López de Prado DSR). Soccer **2025–26 / MLS 2026** are in the DB with open/close but are correctly **skipped** by backtests (do not score). Walk-forward books update **after** the bet (`Update` / `platt.Observe` post-decision) — no classic asOf feature leak found in totals path. Live paper CLV is **broken**: `settle-go`’s optional `closeLine` is fed **final goals** by `pm-calculate.sh`. DK juice in slate/warehouse is **hardcoded −110**, not a scraped DK price. Tiny-sample champs (Liga MX, Serie A) have PnL CIs that cross zero. Protocol below is **confirmed and tightened** (never loosened).

---

## 1. Holdout reuse & selection bias (PBO / DSR)

### Papers (data)

- **PBO** (`library/articles/backtest_probability_of_overfitting.pdf`): single holdout unreliable once many strategies are tried on the same sample; need trial count / CSCV-style deflation.
- **DSR** (`library/articles/backtest_deflated_sharpe_ratio.pdf`): reported performance must be deflated for **number of trials** and non-normality; a backtest without N_trials is not decision-grade.

### How many times was the same holdout used?

Same locked seasons (**NBA `Season=="2025"` = 2024–25**, **MLS 2025**, **soccer `2024-2025`**) appear as the KEEP/FAIL gate in at least:

| Peek family | Evidence |
|---|---|
| NBA PaceEff champ + Scoring | `PROGRESS.md`, `TotalsSweep` |
| PaceEffEfg / prior eFG | `PROGRESS.md` PaceEff vs Efg |
| Platt / ridge cal | `PROGRESS.md`, `reports` cal table |
| Dixon–Coles (6 leagues) | `reports/library_dixon_coles.md` |
| EMA α / B2B | `reports/library_sparsity_timedecay.md` |
| De-vig | `reports/library_devig.md` |
| EB k0 pooling | `reports/library_partial_pooling.md` |
| Stake-lane Kelly book | `reports/live_kelly_holdout.md` |

**≥ 8 named experiment families** (dozens of grid cells each) → **holdout is burned**. Matches BRIEF “single holdout reused for every KEEP.” Round-2 Tier B correctly demotes it to report-only.

### Grid size vs n

| Sweep | Grid | Hold n (PASS leagues) | Trials ≫ √n? |
|---|---|---|---|
| `TotalsSweep` | 3 models × 4 `w` × 7 `minEdge` = **84** (+ cal gates) | NBA hold bets **62** | Yes |
| `MlsTotalsBacktest.SweepAsync` | 5×5 = **25** | MLS **100** | Borderline |
| `Mls…SweepDixonColesAsync` | 4α×5×5 = **100** | (research) | Yes |
| `SoccerLeague…SweepAsync` | 5×5 = **25** | Ligue1 **53**, LigaMX **52**, SerieA DC **113** | Yes for Ligue1/MX |
| `Soccer…SweepDixonColesAsync` | **100** | SerieA **113** | ~1:1 |
| `TimeDecaySweep` | ~**9** α/league (+ optional B2B) | same holds | Mild; code notes DSR if cells≥25 |

**Bug vs stated process:** `TotalsSweep`, `MlsTotalsBacktest.SweepAsync`, and `SoccerLeagueTotalsBacktest.SweepAsync` call `RunAsync` and **print hold CLV/PnL for every cell**, then pick max **train** PnL. Selection objective is train-only, but the operator (and any future automation) **sees hold ranks for all cells** → classic peek / researcher degrees of freedom. `TimeDecaySweep` is closer to honest (train pick → hold once) but still scored the burned holdout for KEEP.

Approximate multiple-testing load for soccer goals EMA alone: **25 cells × 6 leagues ≈ 150** hold peeks before α/DC/pooling families.

---

## 2. Leakage / asOf

| Check | Finding |
|---|---|
| Feature timing | Totals books: predict from EMA state, **then** `Update` with final scores (`TotalsEdgeBacktest`, `MlsTotalsBacktest`, `SoccerLeagueTotalsBacktest`). Platt/ridge: `Observe` **after** the bet. |
| Close used as feature? | No — close only for CLV label / `|open−close|≥0.25` filter (betting on known movers is intentional filter, not feature). |
| Domain `asOf` API | No `asOf` helper in Domain; walk-forward order substitutes. |
| Post-holdout seasons in train metrics | Soccer: `season > holdout` → update book, **skip** train/hold acc. MLS: `2026` skipped same way. NBA: non-hold seasons go to train acc; **NBA 2023/2024/2026 lack TOTAL odds** in DB so they do not create train bets. |

**Verdict:** historical totals path is leakage-safe for model state. **Live paper CLV path is not** (section 5).

---

## 3. Do sweeps include soccer 2025–26 / MLS 2026? (report only — do not score)

| League | Games in DB | TOTAL open+close rows | In train/hold scoring? |
|---|---:|---:|---|
| MLS 2026 | 218 | 218+218 | **No** — explicit skip (`MlsTotalsBacktest`) |
| LIGUE1 2025-2026 | 168 | yes | **No** — `season > holdout` skip |
| LIGAMX 2025-2026 | 193 | yes | **No** |
| SERIEA 2025-2026 | 183 | yes | **No** |
| EFL / LALIGA 2025-2026 | 214 / 200 | yes | **No** (FAIL leagues; same skip) |

Tier C inventory is **present and unused**. Parent must score once in batch after phase 4. This audit did **not** compute Tier C metrics.

---

## 4. Hand-set `OverProb` sharpness & calibration

| Item | Evidence |
|---|---|
| Definition | `TotalsMath.OverProb` = `σ(sharpness · (exp−line))`; default **sharpness=0.25** |
| Live / soccer | `DailySlate`, soccer backtests, `advise-mls`: **1.2** hardcoded |
| Other sports | NFL **0.2**, MLB **0.45** in their backtests — also hand-set |
| Origin | No train fit / documentation of MLE; comments only (“NBA≈0.25, soccer≈1.2”). |
| Calibration on TRAIN | `OuPlattCalibrator` walk-forward on past games only (`MinFit=50`); ridge same. **FAIL** vs PaceEff hold PnL (`PROGRESS.md`) — correctly not in champ. |
| Money path | Sharpness drives EV gate and Kelly `modelProb` → **sizing**, not only Brier. Pure accuracy without stake change is IGNORE; sharpness **is** a money knob. |

Platt FAIL does **not** prove 1.2/0.25 are calibrated — only that unconstrained 2-param fit hurt hold PnL under current gates.

---

## 5. `paper.db` CloseLine / ClvPts, DK −110, 2026-10-28 4/6

### Settle path (root cause)

1. CLI: `settle-go <key> <W|L|P> [closeLine] [closeOdds]` → `PaperLedger.SettleGroupAsync` → `ApplySettle`.
2. `CloseLine` ← optional arg; `ClvPts` = `closeLine − Line` (OVER) or `Line − closeLine` (UNDER).
3. **`scripts/pm-calculate.sh`** ESPN probe prints:
   `settle-go "…" {result} {total}` where `total = away_score + home_score` (**final goals**), not market close.

**Confirmed on settled rows:** e.g. `20261028 COL@SJ` OVER 2.5 → `CloseLine=4.0`, `ClvPts=+1.5` (4−2.5). Same pattern HOU@SEA `CloseLine=1.0`, CLB@DAL `2.0`, etc. Parent’s COL@SJ note is correct: **live ClvPts ≈ signed distance to final total, not CLV**. Backtest CLV (warehouse open→close) is a different quantity.

### DK −110: default, not verified DK price

| Source | Behavior |
|---|---|
| `DailySlate` NBA/soccer | `var juice = -110` always in GO line / `paper-go` hint |
| Warehouse `OddsSnapshots` TOTAL | **74615/74615** `AmericanOdds=-110`, `AssumedJuice=1` |
| `paper.db` | DK tickets **72× −110**; Kalshi shows real asks (−163/−150/−104) |
| `PaperLedger` multi-venue | DK uses caller odds; Kalshi/RH/Crypto fetch live asks |

**Verdict:** DK −110 on live tickets is the **slate default / assumed juice**, not proof of fill price. Kalshi odds are real public asks.

### Why 4/6 ≈ break-even (juice asymmetry)

Six MLS keys on 2026-10-28 slate; Flat lane:

| Venue | Record | Notes |
|---|---|---|
| DK | 4–2 @ **−110** | Flat PnL **+$16.36** on $60 (looks fine) |
| Kalshi | 2–1 | Wins @ **−163**, **−150**; loss @ **−104** → Flat only **+$2.80** on $30 |

Break-even win rates: −110 ≈ 52.4%, −150 = 60%, −163 ≈ 62%. A **66.7% hit rate** looks strong in W/L but Kalshi **short prices on winners / near-even on losers** compress dollar EV. Story of “4 of 6 cashed” overstates edge once juice is marked to market — especially on Kelly5 stakes sized from uncalibrated `p`.

---

## 6. CIs for tiny-sample champs (Liga MX, Serie A)

Rough i.i.d. −110 unit bets (illustrative; real variance higher with juice/CLV filter):

| League | Hold n | Hold PnL | Hold CLV | Approx PnL 95% band | CLV ±1.96·(0.75/√n) |
|---|---:|---:|---:|---|---|
| Liga MX EMA | 52 | +3.5u | +0.24 | **[~−10, +17]u** | +0.24 ± 0.20 |
| Serie A DC | 113 | +1.7u | +0.03 | **[~−18, +22]u** | +0.03 ± 0.14 |
| Ligue 1 | 53 | +10.1u | +0.02 | [~−3, +23]u | +0.02 ± 0.20 (CLV ≈0) |
| MLS | 100 | +15.7u | +0.19 | [~−3, +34]u | +0.19 ± 0.15 |
| NBA | 62 | +16.3u | +2.64 | [~+2, +30]u | wide but mean large |

**Liga MX and Serie A:** point PnL > 0 but **CI includes large losses**; Serie A CLV CI includes ≤0. Treat as **provisional paper only** until Tier A walk-forward + Tier C agree. Ligue 1 PnL looks better but **CLV≈0** — soft CLV kill risk.

---

## 7. Other look-elsewhere (lanes × venues)

- Stake trial: **6 schemes × 4 venues = 24** independent $1000 books (`StakeSchemes` × DK/Kalshi/RH/Crypto).
- `paper-go` EV-gates each venue separately → more chances for one book to look hot.
- Kelly lanes reuse the same burned holdout tickets (`reports/live_kelly_holdout.md`) — sizing KEEP is less overfit than model KEEP but still one peek.
- De-vig / assumed −110 warehouse: fair methods identical (`reports/library_devig.md`) — no extra signal; IGNORE until two-way real juice exists.

---

## Ranked fixes by money risk (do these; protocol below)

| Rank | Risk | Issue | Fix (money path) |
|---:|---|---|---|
| **1** | **Critical** | Live `CloseLine`/`ClvPts` = finals via `pm-calculate.sh` → Gate 2 “CLV trust” is fake | Settle must take **market close** (or null CLV); store **FinalTotal** separately. Ask before editing WIP `PaperLedger.cs`. Fix script settle hint. |
| **2** | **Critical** | Holdout burned + grid prints hold every cell | Phase 4: **train-only** selection logs; hold scored **once** per candidate; log **N_trials**; Tier B never KEEP alone (already). |
| **3** | **High** | Liga MX / Serie A / thin-CLV Ligue 1 may be noise | No new stake until Tier A PASS + Tier C; label current KEEP **provisional**; optional raise hold n or require CLV lower bound > 0 with CI. |
| **4** | **High** | DK −110 default vs Kalshi real juice | Paper/GO must use **entered or venue ask**; never invent −110 when quote exists. Warehouse AssumedJuice stays labeled assumed. |
| **5** | **Medium** | Hand-set sharpness → Kelly/EV | Any sharpness/distribution change: Tier A log-loss/Brier **and** Kelly-lane PnL on **same tickets** (protocol §6); ≤3 params. |
| **6** | **Medium** | 24 lane×venue books | Pre-register **primary** scheme+venue for promotion; others are diagnostics. |
| **7** | **Low** | DSR note only if cells≥25 | Always report N_trials; if N_trials≥25 require Tier A majority folds (not single hold). |

Accuracy-only recalibration with no stake/EV change → **IGNORE**.

---

## Evaluation protocol — confirm + tighten

**Confirmed** (unchanged intent): Tier A walk-forward; Tier B burned report-only; Tier C look-once soccer 2025–26 / MLS 2026 scored **once** by parent; NBA provisional; ≤3 new params; sizing judged on scores **and** Kelly PnL.

**Tightenings** (additive; never relax Tier C or n floors):

1. **Tier A:** keep mean CLV ≥ champ, PnLΔ>0 in **majority of folds**, **n ≥ 40 per league overall**. **Add:** folds must be **pre-declared** (rolling origin on seasons `< holdout`); publish fold table in `exp_*.md`.
2. **Tier B:** report only — **explicit ban** on KEEP/merge justified solely by 2024–25 / MLS 2025 (already). **Add:** do not print or sort on Tier B inside sweep loops.
3. **Tier C:** unchanged. KEEP needs Tier A PASS **and** Tier C CLV ≥ champ **and** Tier C PnL > 0. Ledger row before any score.
4. **NBA:** provisional (Tier A+B) + live paper — **but live CLV only after fix #1**.
5. **N_trials:** mandatory beside every reported metric (grid cells × leagues × model families touched in that experiment). If **N_trials ≥ 25**, Tier A majority-fold rule is mandatory (DSR caution); single-hold “PASS” insufficient.
6. **Sizing/probability:** unchanged §6; sharpness counts as sizing.
7. **NEW — Sweep hygiene:** selection metrics = **train (or Tier A) only**. Hold/Tier C evaluated in a **separate command** after the candidate is frozen.
8. **NEW — Tiny-sample:** if hold n < 80 **or** |mean CLV| < 0.05 goals (soccer), label **provisional**; cannot be sole justification to raise live stake.
9. **NEW — Venue/lane:** promote on a **pre-registered** primary venue+scheme; other cells are exploratory.
10. **NEW — Juice:** backtests using AssumedJuice −110 must say so; live tickets require venue ask or human-entered American odds.

---

## Shortlist for parent (protocol amendments to paste into PLAN)

- Ban hold-metric printing during grids; log N_trials; N_trials≥25 ⇒ Tier A mandatory for KEEP.
- Fix settle CLV vs final total before trusting live paper CLV.
- Liga MX / Serie A provisional until Tier A+C.
- Pre-register primary lane×venue; DK −110 not a real quote.

---

## Skipped / not checked

Did not re-run full sweeps or compute Tier C PnL/CLV. Did not implement settle/script fixes. CI bands are −110 toy variance, not bootstrap of actual bet PnL series.
