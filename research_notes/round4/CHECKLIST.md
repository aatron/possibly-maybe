# Checklist: planned but not implemented / not tested

Compiled 2026-11-07 from `PLAN.md` (root), `book_round2/*`, `round3/*` (final ledger), `round3/revisit_candidates.md`, code and DB. Each `[ ]` maps to a round-4 plan item (→ id).

## A. Carried over from round 3
- [ ] Serie A keep/fix/drop verdict (r3 W9) → D2
- [ ] NCAAB totals pack (r3 W10) → C1
- [ ] Training-process review across all books (r2 phase 6 / r3 W11) → D3
- [ ] Round-3 value report: proof tables + variant leaderboard (r3 W11) → D4
- [ ] archive.org odds history (r3 W16, **agent still running**) → import in C0 / league items
- [ ] Always-on tracker while laptop sleeps (r3 W4), **deferred by user**, stays parked
- [ ] Verify V5 `+realjuice` reads real DK prices on every league, not −110 → A1

## B. Variants never built (backtestable)
- [ ] `+pooling`: shrink to league mean, early season / promoted clubs (EB was within ~1u of champ) → B2
- [x] `nba-volume` (w=0.6, edge 3.5) + PaceEffEfg arm (+11.5u/147, +14.7u/201 vs +16.3u/62) → B3
- [x] `mls-dc` (+13.2u/116 vs +15.7u/100) → B4
- [x] `+poisson-all` for MLS / Liga MX (near-tie CLV) → B4
- [x] Negative-binomial φ goal counts (r2 deferred) → B4
- [x] Dixon–Coles ρ low-score correction (r2 deferred) → B4
- [x] Fitted P(over) curve per league (learned σ / sharpness, not hand-set; r2 deferred) → B7
- [x] `soccer-strict` (edge ≥ 0.5 goals, EV 4–20%), `nba-strict`, EV-band variants → B7
- [ ] Referees for **totals** (NBA `RefereeAssignments` 72k rows; `RefEffects.cs` only fits home-win) → B5
- [x] NBA absence / DNP adjustment (`AbsenceBook.cs` wired to backtest, **no result recorded**) → B1
- [x] Conformal-interval bet filter (r2 deferred) → B8
- [x] Kalman / adaptive-rate team ratings (r2 deferred) → B8
- [ ] Inverse-variance blend weight (coin flip 7/14 folds) → B9

## C. Variants never built (live-only)
- [x] `+injury-guard`: ESPN injury / status changes in the last 24h (NBA, NFL, NCAA) → B1
- [ ] `+roster-reset`: after major trades / transfers, for promoted / relegated clubs → B2
- [x] `consensus-outlier` (Kaunitz): bet the venue that's off from the cross-venue consensus → B6
- [x] `line-move-follow`: bet only when the line moved toward us → B6

## D. Leagues / markets never or not properly tested
- [ ] NCAAF totals (16 SBRO seasons imported in r3 W15) → C2
- [ ] NFL: Tier A on filled O→C; `nfl-wind` alone; 1H / quarter / team totals (Kalshi lists them) → C3
- [ ] MLB: Tier A 2011–2019 + look-once 2021 (never run); F5 + team totals for spring → C4
- [x] La Liga, EFL back in **shadow** (FAILED as champs; waves may still appear) → C5
- [x] New leagues the venues list (EPL, Bundesliga, UCL, NHL, WNBA, NWSL, NCAAW, …), data-gated → A4 → C6
- [x] Spreads / moneylines (DB has ML + SPREAD; only NBA ATS was ever tracked) → C7
- [ ] Line tracker covers NFL / MLB only; needs **every** league for live CLV / consensus → A2

## E. Infrastructure for "waves of soft spots"
- [ ] Shadow ledger stores **every candidate** (all games × all model outputs), so a variant is a filter, not a re-run → A1
- [ ] Wave radar: rolling CLV per league × market × variant, sequential alerts + false-discovery control → A3
- [x] Seasonality scan: historical CLV by season phase → wave calendar → A5
- [ ] Trial registry + promotion / demotion ladder (shadow → paper → user-approved real) → A3
- [x] Generic sport-pack template so a league is a config, not a project → C0

## F. Wave R (point-in-time / no-cheating)
- [x] As-of cutoff in every model builder + leak test → R1
- [x] Append-only decision log (run id, SHA, watermark, venues, injury) + settlement table → R2
- [x] Same-book close → R3
- [x] Checkpoint reporter → R5
- [ ] Real DK price on paper tickets → R6
- [ ] AsOfView / IStrategy / day engine / vault → R7–R10
