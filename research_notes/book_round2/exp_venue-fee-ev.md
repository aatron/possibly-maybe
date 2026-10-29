# Experiment: `venue-fee-ev`

**Branch:** `exp/venue-fee-ev`  
**Date:** 2026-10-28  
**Source:** V1 `venue_placement.md` gap #1  
**Goal:** stop false GOs / overstated Kelly on Kalshi / Robinhood / Crypto.com by pricing EV and size off all-in cost, not raw ask.

## Change

- `OddsMath.BinaryTakerFeePerContract` / `EffectiveBuyPrice(P, venue)`
  - Kalshi & Crypto.com: `fee ≈ 0.07·P·(1−P)` (large-C; ignore centicent round-up)
  - Robinhood: `min(0.01, 0.10·P·(1−P)) + 0.01` exchange (no-Gold `k=0.10`)
  - DraftKings: fee `0` (American juice already in the quote)
- `PaperLedger.PlaceFromBinaryQuoteAsync` converts `AmericanFromBuyPrice(EffectiveBuyPrice)` for the **EV ≥ 4% gate**, scheme sizing, and ledger `AmericanOdds` (so settle PnL matches all-in).

## N_trials

| # | Variant | Kept? |
|---|---|---|
| 1 | Fee-blind ask (baseline) | no — overstates EV |
| 2 | Documented large-C taker fees (Kalshi/Crypto 0.07; RH no-Gold + $0.01 exch) | **yes** |

**N_trials = 2** (formula family only; no price grid). Skipped: RH Gold `k=0.05`, exact `round_up` fee tables — would need account confirmation / per-fill C; upgrade later via `shortcut` in code.

## Tier A

**Not run.** `OddsSnapshots` TOTAL books are Consensus/sportsbook only — **no historical Kalshi / Crypto / RH binary ask series** in `possiblymaybe.db`. Inventing synthetic books would fake CLV. This is a **live/paper-path pricing fix** with unit proof against the venue_placement BE table.

## Unit proof

`OddsMathTests`: Kalshi/Crypto all-in matches BE table (0.20→0.2112, 0.50→0.5175, 0.80→0.8112); RH mid-book +2¢; DK unchanged; at ask=0.50 / p=0.53 fee-blind EV>4% while fee-adjusted EV<4%.

## Decision (for parent)

- **Provisional KEEP** for merge after sibling experiments: money-correct on binary venues; zero free params; cannot claim Tier A CLV.
- Primary venue+scheme for later live check: **Kalshi / Kelly2** (pre-registered exploratory vs RH which should GO less often at same ask).
- Do **not** score Tier C here.

## Safety

No auth, no orders, no Tier C, library unused.
