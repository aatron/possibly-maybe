# Revisit candidates: past approaches worth running side by side

2026-11-07. Sources: `PROGRESS.md`, `reports/library_*.md`, `research_notes/book_round2/exp_*.md`, `hunt_*.md`, `tier_c_*.md`, root `PLAN.md`.
Purpose: decide which ruled-out or never-tested approaches become **variants** in the shadow ledger (W1), next to V0.

## 1. Are two months of bets enough signal?

**For profit, no. For CLV, yes,** once live CLV is fixed (W5).

| Metric | Two-month volume (est.) | Std. error | Can it detect a real ~3% edge? |
|---|---|---|---|
| Flat PnL, wide variant (V0) | ~400 bets | ±4.8% ROI (sd ≈ 0.95u / bet at −110) | **No.** The edge is smaller than the noise. |
| Flat PnL, strict variant | ~100 bets | ±9.5% ROI | **No.** |
| CLV, wide variant | ~400 bets | ≈ ±0.15 prob-pts (sd ≈ 3 pts / bet) | **Yes.** A +1 pt CLV shows up at 6σ+. |
| CLV, strict variant | ~100 bets | ≈ ±0.3 prob-pts | **Yes**, at +1 pt. |

Rules so two months isn't misleading:
- **Rank variants by mean CLV, not PnL.** PnL only confirms over 6–12+ months (~1,500 bets for 2σ at a 5% edge).
- **Compare variants on the bets where they disagree.** Shared bets cancel out, so the paired difference is much less noisy.
- **Count every variant as a trial.** With ~10–15 variants, the "best" after two months is partly luck (winner's curse). Promote only if its CLV lead persists in month 3, and keep losing variants visible.
- **Track variants that skip bets** by how the bets they skipped did. That's their whole value.

## 2. Injuries, trades, lineups: never ruled out, never tested

Nothing in the history tested injuries or trades. Root `PLAN.md` planned news extraction (`INewsExtractor`, player status / minutes) for "post-edge", and `PROGRESS.md` lists "Injuries/lineups" as still missing. The blocker is that **there's no free historical archive of pre-game injury reports**, so it can't be backtested. **Live shadow variants are the only way to test it**, which makes it the strongest case for running side by side. It is also defense #1 against false edges: a big edge is often a star missing that our model doesn't know about.

## 3. Past approaches: result, how close, re-include?

**Closeness:** gap to the champ at the time on the same holdout, in units (u). Small n means "close" can be noise either way.

### Re-include as variants (close, cheap, already coded or data on disk)
| Approach | Past result | How close | Variant idea |
|---|---|---|---|
| **EB partial pooling** (shrink team rates to league mean) | MLS −0.9u, Ligue 1 −1.0u, NBA ±0 vs champ | **Very close** (within noise) | `+pooling`: matters most **early season and for promoted clubs** (Le Mans, Atlante), exactly now |
| **Dixon–Coles for MLS** | +13.2u / 116, CLV +0.18 vs α-EMA champ +15.7u / 100 | **Close** (−2.5u, more bets, same CLV) | `mls-dc` |
| **Poisson P(over) for MLS / Liga MX** | MLS Tier A FAIL; Liga MX Tier C FAIL | Tie-ish on CLV | `+poisson-all`: honest probabilities for leagues still on the hand-set logistic curve |
| **NBA volume setting** (PaceEff w=0.6, edge 3.5) | +11.5u / 147 vs champ +16.3u / 62 | **Close**, 2.4× the bets | `nba-volume`: answers few-big vs many-small for NBA directly |
| **PaceEffEfg at volume** (w=0.6, edge 3.5) | +14.7u / 201 vs +16.3u / 62 | **Close** | Fold into `nba-volume` as a second arm |
| **Legacy NBA logistic** | Same tickets as Normal σ; bigger Kelly paid in 2015–22 | Tie on flat | Already planned (V0L) |
| **Inverse-variance blend weight** | NBA ΔPnL > 0 in 7/14 folds; MLS CLV slightly better | Coin flip | Low priority, cheap |

### Never tested, data on disk: backtest first, then variant
| Approach | Status | Why it could pay |
|---|---|---|
| **Referees for totals** (NBA) | `RefereeAssignments` has 72k rows; ref features were built for the moneyline Gate 1b. **No totals result was recorded.** | Ref crews' foul rates move totals; it's public but often under-priced. Backtestable now. |
| **NFL wind / weather extremes only** | Full NFL model FAILED (hold −1.6u); wind was one input among many, never tested **alone** | A classic narrow soft spot; very few bets, so it pairs with the CLV metric |
| **Negative-binomial / DC-ρ goal counts** | Deferred in round 2 | Fixes over-confident P near 2.5 / 3.5, the same problem as the absurd EVs |

### Live-only variants (can't be backtested, so side by side is the only test)
| Approach | Variant idea |
|---|---|
| **Injury guard** (ESPN injuries / status feed) | `+injury-guard`: skip when a top-minutes player's status changed in the last 24h, or when the edge points the way the injury explains. Pure false-edge defense. |
| **Trades / roster change** | `+roster-reset`: after a major trade (or for promoted / relegated clubs), widen uncertainty or shrink the team toward the league mean for N games |
| **Kaunitz consensus** (bet the venue that's off from the others) | `consensus-outlier`: the line tracker now records DK / Kalshi / Crypto.com side by side; no team model needed |
| **Line-move follow** | Already W2 (guard); also run as a **positive** signal variant: bet only when the line moved *toward* us |

### Don't revisit (not close, or no money path)
| Approach | Past result | Reason |
|---|---|---|
| Dixon–Coles Ligue 1 / Liga MX / EFL | +1.0u / 21; −9.6u; −2.2u | Far behind |
| Platt / ridge calibration (NBA) | +9.1u / 31 (log-loss broken); +8.2u / 30 | Halves the bets, loses PnL |
| De-vig methods | Identical to champ | No effect at −110; superseded by real prices (V5) |
| B2B / rest | β≈0 on train in NBA / MLS / Ligue 1 | No signal |
| MLB SP-FIP + park | Hold +1.4u / 100, CLV −0.03; later −0.8u (n=18) | Revisit only through line-tracker CLV next season |
| Confidence / edge-band staking | Lost to ¼ Kelly | Already running as paper lanes |

## Suggested variant set to add (high level)
1. `+injury-guard`, live-only (highest value: false-edge defense)
2. `+pooling` (early season, promoted clubs)
3. `nba-volume` (many-small vs few-big, NBA)
4. `mls-dc`
5. `+poisson-all`
6. `refs-nba` (after a backtest)
7. `nfl-wind` (after a backtest; shadow-only, NFL not live)
8. `consensus-outlier`, live-only
9. `+roster-reset`, live-only
10. `soccer-strict` (edge ≥ 0.5 goals, EV 4–20%), from the earlier slate discussion

Each one is a new immutable id in the W1 registry. Build is Composer's job; this file is the spec input.
