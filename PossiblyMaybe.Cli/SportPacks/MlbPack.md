# MLB sport pack — Gate 0 checklist

Scaffold only. **Do not change NBA import/backtest paths.** Shared Domain (`Sport` on games/markets) is fine; no MLB importer until files are local (see `data/raw/NOTES_MLB.md`).

## Scope (first pass)

| Market | Priority | Notes |
|---|---|---|
| **Totals (over/under runs)** | First | Open→close when available; else close-only + flag |
| **Run line** | First | Parallel to NBA spread; own CLV series |
| Moneyline | Later | After totals/run line ingest QC |

## Gate 0 done-when

- [ ] Human download **M1** (and **M2** only if useful) into `data/raw/` — URLs in `NOTES_MLB.md`
- [ ] `NOTES_MLB.md` updated: local paths, download date, license, row-count QC
- [ ] Import targets mapped (no code yet until files present):
  - [ ] `Games` — date, teams, final scores (runs)
  - [ ] `OddsSnapshots` — totals + run line; `PriceKind=open|close`; `ClvQuality=open_close|close_only`
- [ ] Coverage sanity: holdout season has scores + usable prices (report % if open incomplete)
- [ ] **Own holdout** locked before any Gate 1 metrics (separate from NBA 2023–24). Record season in `PROGRESS.md` when locked — look once

## Explicit non-goals (this scaffold)

- Full MLB importer / CLI command
- Umpire/ref features, pitcher load, news
- Shared holdout or shared “champ” with NBA

## After Gate 0

Gate 1 kill is **per sport**: walk-forward CLV on MLB totals/run line only. Infra reuse (odds math, Kelly, ledger `Sport`) yes; NBA Elo/ref recipes no.
