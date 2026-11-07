# Round 4 waves report (D4)

**Close:** 2026-11-07 · **Source of truth:** `research_notes/round4/PROGRESS.md`, `CHECKLIST.md`, `*_proof.md`.  
**Numbers:** only from those proofs (or named r3 proofs). Empty / deferred cells are marked — not invented.

## Radar + BH + wave calendar

| Piece | State | Proof |
|---|---|---|
| Wave radar | Shipped: league × market × variant cells; CUSUM (`k=1`,`h=10`); WAVE OFF on rolling-40 CLV&lt;0; ladder shadow→paper needs WAVE ON + BH-sig + ≥3 weeks; paper→real = user only | `a3_proof.md` |
| BH FDR | q=0.10; `N_trials` printed | `a3_proof.md`, `a5_proof.md` |
| Synthetic detection | +2pt CLV wave ON within 60 bets; null false-alarm ≤10% | `a3_proof.md` |
| Checkpoints | 50 bail / 100–499 CLV verdict / 500 PnL-loose / 1500+ PnL confirms (+ wide stake) | `r5_proof.md` |
| Seasonality calendar | Phases **pre-registered** (NBA, MLS, L1/SA, Liga MX); BH table **pending** first `seasonality-scan` | `a5_proof.md` |
| Seasonality → radar | Not wired (radar CLV-only) | `a3_proof.md` |

## Wave A — foundation

| Id | Item | Verdict / note | Proof |
|---|---|---|---|
| A1 | Candidate store + variants-as-filters | V0/V3 Evaluate fixtures stable; 1-file families | `a1_proof.md` |
| A2 | Tracker all leagues + importer | Configured; O+C ≥80% target after cron (worktree snapshots empty at write) | `a2_proof.md` |
| A3 | Radar + BH + ladder | Synthetic PASS | `a3_proof.md` |
| A4 | League / venue census | Ranked C6 list (EPL, Bundesliga, NHL, …) | `league_census.md` |
| A5 | Seasonality scan | Phases locked; results table empty | `a5_proof.md` |

## Wave B — variant families

| Id | Variants | Status | Proof numbers (if any) |
|---|---|---|---|
| B1 | `+absence`, `+injury-guard` | Absence **not promote**; injury live-only | Hold 2025: PaceEff n=61 CLV=2.66 PnL=+17.3 vs Absence n=223 CLV=1.69 PnL=−7.3 (`b1_proof.md`) |
| B2 | `+pooling`, `+roster-reset` | Registered; Tier A subset deferred; roster live-only | `b2_proof.md` |
| B3 | `nba-volume`, `nba-volume-efg` | Shadow; **do not promote** | Tier A mean CLV/bet V0 19.11 (n=243) vs vol 5.78 (n=748); hold V0 +17.3u/61 vs vol +13.5u/145 (`b3_proof.md`) |
| B4 | `mls-dc`, `+poisson-all`, `+negbin`, `+dc-rho` | Registered; fold table deferred (D2 / radar) | `b4_proof.md` |
| B5 | `refs-nba` | Unit residual OK; fold CLV deferred | `b5_proof.md` |
| B6 | `consensus-outlier`, `line-move-follow` | Live shadow only (no Tier A) | `b6_proof.md` |
| B7 | fitted-curve / strict / EV-band | Registered; Tier A + reliability table deferred | σ lock NBA 22.8, MLS 1.65, … (`b7_proof.md`) |
| B8 | `+conformal`, `kalman` | Unit PASS; fold table deferred → shadow-only | `b8_proof.md` |
| B9 | `inv-var-blend` | **Tier A FAIL** → shadow only | NBA 7/14 folds, CLV 10.46 vs 8.56; MLS 2/5 (`b9_proof.md`) |

## Wave C — leagues / markets

| Id | League / market | Verdict | Proof |
|---|---|---|---|
| C0 | Sport-pack template | MLS identical V0 | `c0_proof.md` |
| C1 | NCAAB totals | Hold PASS on train-selected cell; **shadow-only** (KILL paper until ladder) | Train n=7346 CLV=+13.99 PnL=+1480.7u; Tier C `2021-22` n=468 CLV=+2.74 PnL=+44.7u (`c1_proof.md`) |
| C2 | NCAAF totals | HOLD PASS shadow; cold-start caveat | Train w=0.60 e=3 n=1673 CLV=+9.26 PnL=+783.7; Tier C `2022-23` n=94 CLV=+1.68 PnL=+18.7 (`c2_proof.md`) |
| C3 | NFL + `nfl-wind` | Wind arm Tier A **PASS** vs no-wind; full NFL HOLD FAIL still stands | 5/8 win folds; nW=337; meanClvW=−0.022 (`c3_proof.md`) |
| C4 | MLB redo | Tier A **PASS**; Tier C 2021 **PASS** (look-once); shadow/paper until radar + true close | Agg n=1853 mean fold CLV +0.118 sum +166.9u 7/9; 2021 n=387 CLV=+0.26 PnL=+34.9 (`c4_proof.md`) |
| C5 | La Liga + EFL | **FAIL** champs; shadow on | LALIGA foldPass=2/6 meanClv=+0.080 pnl=−19.8; EFL 4/6 meanClv=+0.107 pnl=+2.6 burned PnL≤0 (`c5_proof.md`) |
| C6-epl | EPL pack | Pack + holdout lock; not paper SOCCER_PASS | `c6_epl_proof.md` |
| C6-bundesliga | Bundesliga | Shadow-only; Tier A/C numeric pending warehouse fill | `c6_bundesliga_proof.md` |
| C6-nhl | NHL | Shadow + tracker; Tier A **gated** (`TierAReady=false`) | `c6_nhl_proof.md` |
| C7 | Spreads + ML candidates | Store + backfill; no paper-go until market family | `c7_proof.md` |

## Wave R — point-in-time / no-cheat

| Id | Item | Proof |
|---|---|---|
| R1 | As-of cutoff | `r1_proof.md` leak tests PASS |
| R2 | Append-only decision log | `r2_proof.md` |
| R3 | Same-book close | High same-book availability on ESPN→DK; **≤15m freshness near 0%** (`r3_proof.md` table) |
| R4 | replay CLI | Superseded by R7–R9 |
| R5 | Checkpoints | `r5_proof.md` |
| R6 | Real DK on paper | Before 0/180 real; AssumedJuice excludes CLV (`r6_proof.md`) |
| R7 | `AsOfView` | Leak + no-DB surface PASS (`r7_proof.md`) |
| R8 | `IStrategy` + V0 | Evaluate-equivalent fixtures (`r8_proof.md`) |
| R9 | Day engine | Live slate == engine V0 synthetic (`r9_proof.md`) |
| R10 | Vault + nested + no sim promotion | Double-use refused; sim→Shadow only (`r10_proof.md`) |

## Round-3 value tables (carried)

CLV-first ranking / two-month signal: `round3/revisit_candidates.md` §1.

| Item | Summary | Proof |
|---|---|---|
| W2 guards | Edge-cap **25%** (soccer PASS; NBA FAIL removed-bet rule); NBA line-move k=**2.0** PASS; soccer k=**0.5** PASS | `round3/w2_guard_proof.md` |
| W7 shutoff V4 | All 5 leagues DD≤V3 and PnL≥V3 | NBA V3 pnl 29.9 dd 22.8 → V4 32.3 / 20.5; Ligue1 27.5/8.5 → 32.9/8.2 (`round3/w7_shutoff_proof.md`) |
| Launch variants | V0…V5, V-SA0 in registry (r3 W1 → r4 A1) | r3 PLAN + `a1_proof.md` |
| Revisit → B-wave | pooling, nba-volume, mls-dc, poisson-all, refs, injury, consensus, … | `revisit_candidates.md` → B1–B9 |

## Still open after D4 (not this report)

D1 true close every settle · D2 Serie A verdict · D5 green tests + push · A5 first BH fill · deferred B-family Tier A tables.
