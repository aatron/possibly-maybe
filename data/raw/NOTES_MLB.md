# Gate 0 raw data — MLB

## Status summary

| ID | Source | Status | Path |
|---|---|---|---|
| **M1** | Kaggle `minmaxer/mlb-closing-odds-2025` | **Downloaded** (`archive(5).zip`) | `kaggle/mlb_closing_odds_2025/mlb_2025_closing_odds.csv` (~2477 rows) |
| **M2** | Kaggle `christophertreasure/major-league-baseball-vegas-data` | **Not downloaded** | — |

**Metrics:** see `SportPacks/MlbPack.md`. Starting pitcher is mandatory for a real totals model — not implemented yet.

## M1 columns (present)

Open/close home win probs, run line, **open_total / close_total**, scores. Good for CLV on totals once SP features exist.

## Longer history

One season of open/close is thin. Multi-year walk-forward may need Odds Warehouse or similar later.
