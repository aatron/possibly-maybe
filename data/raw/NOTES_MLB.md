# Gate 0 raw data — MLB (not downloaded yet)

Download when ready (manual Kaggle). Do **not** mix into NBA paths under `data/raw/kaggle/`. Suggested local layout after download: `data/raw/kaggle/mlb_*`.

## Status summary

| ID | Source | Status | Path |
|---|---|---|---|
| **M1** | Kaggle `minmaxer/mlb-closing-odds-2025` | **Not downloaded** | (human) |
| **M2** | Kaggle `christophertreasure/major-league-baseball-vegas-data` | **Not downloaded** — evaluate if useful | (human) |

---

## M1 — minmaxer MLB closing odds 2025 (primary candidate)

- **URL:** https://www.kaggle.com/datasets/minmaxer/mlb-closing-odds-2025
- **Expect:** open/close **totals** + **run line** for 2025
- **Use:** first MLB Gate 0 market stack (totals / run line before moneyline sprawl)
- **License / row counts:** confirm on Kaggle after download; record here

---

## M2 — Christopher Treasure MLB Vegas data (optional)

- **URL:** https://www.kaggle.com/datasets/christophertreasure/major-league-baseball-vegas-data
- **Use:** only if schema/coverage fills gaps M1 cannot (history, books, open lines). Skip if redundant.
- **License / row counts:** confirm on Kaggle after download; record here

---

## Longer history (not Kaggle-locked)

Free Kaggle slices above are thin for multi-year walk-forward. Longer open/close history may need:

- **Odds Warehouse** (or similar paid/retail odds archives)
- Other public scrapes / community dumps — document URL + license before ingest

**Princeton DSS** MLB holdings are **institutional** (campus access / terms) — not a default personal Gate 0 download path.

---

## Rules

- Raw files stay local (`data/raw/**` gitignored). Only `NOTES.md` / `NOTES_*.md` are tracked.
- No MLB importer until local files exist and this note lists paths + QC row counts.
- NBA Gate 0 notes remain in `NOTES.md` — do not overwrite.
