# NFL sport pack — Gate 0 checklist

Totals-first, same playbook as NBA champ. Do **not** change NBA totals paths.

## Gate 0

- [ ] Unpack Kaggle **tobycrabtree/nfl-scores-and-betting-data** → games + scores + long betting history under `data/raw/kaggle/…`
- [ ] Unpack Kaggle **minmaxer/nfl-odds-2026** → **open and close** totals (and spreads) under `data/raw/kaggle/…` — required for real CLV
- [ ] Optional fallback: `data/raw/nfl/nflverse_games.csv` (close-only; weaker CLV)
- [ ] Import: games + open/close **totals** into the lab DB (separate from NBA or sport-tagged; do not overwrite NBA Gate 0)
- [ ] Lock **one full NFL season** as holdout **before** looking at metrics (candidate: most recent complete regular season in the open/close window — freeze once, peek once)
- [ ] Row-count / season QC in `PROGRESS.md` (or NFL notes) after first import
- [ ] Totals-first soft-spot / edge path only after import + holdout freeze; no full backtest until open/close (or documented close-only) data is present and small enough to reason about

## Out of scope for this scaffold

- Full NFL importer / totals sweep / paper ledger
- Touching NBA `Gate0Importer`, `TotalsSweep`, or champ promotion
