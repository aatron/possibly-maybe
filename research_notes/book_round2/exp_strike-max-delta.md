# Experiment: strike-max-delta

**Branch:** `exp/strike-max-delta`  
**Goal:** Reject Kalshi / Crypto.com totals matches when `|strike − model_line|` exceeds sport max Δ so paper-GO cannot attach to the wrong line.  
**Source:** `venue_placement.md` fix #2.

## Change

| Constant | Value | Use |
|---|---:|---|
| `StrikeMatch.MaxNbaDelta` | **0.5** | NBA |
| `StrikeMatch.MaxSoccerDelta` | **0.25** | MLS / other soccer |

- `KalshiQuotes.TryGetTotalAsync` — after event filter, keep markets with `dist ≤ MaxDelta(sport)` only.
- `CryptoComQuotes.TryGetTotalAsync` — same gate on Over/Under contract strikes.
- Shared helper: `PossiblyMaybe.Domain/StrikeMatch.cs`. Tests: `StrikeMatchTests`.

Robinhood paper path uses Kalshi quotes → inherits the gate.

## Evaluation

**Cannot run Tier A** on warehouse totals: strike matching is **live-path hygiene** (venue contract ↔ model line). Historical `OddsSnapshots` / Consensus rows are Consensus-only and do not store Kalshi/Crypto floor strikes, so walk-forward CLV/PnL folds do not apply.

| Item | Value |
|---|---|
| **N_trials** | **1** (fixed thresholds from venue_placement; no grid) |
| Tier A | **N/A** — live matching, not model selection |
| Tier B | **N/A** |
| Tier C | **not run** (protocol) |

**Verdict (provisional):** **SHIP as hygiene** — money path is fewer false GOs on distant strikes; no holdout claim.

## Safety

Public GETs only; no auth/orders; `library/` = data only; no `paper.db` writes.
