# MLS sport pack — Gate 0 / Gate 1

**Do not change NBA champ paths.** Shared Domain + ledger only.

## Soft-spot (one paragraph)

MLS is typically **softer than NBA/NFL/MLB for US retail** (DK/FD): lower liquidity and handle, less sharp attention than the Big 4, and public money that overweights brand favorites / overs while mishandling draws and travel. Offshore soccer closes are sharper; the edge thesis is **US books’ MLS totals**, not beating Pinnacle 1X2.

## Holdout (locked before peek)

Train `season` ≤ **2024**; holdout **2025**; skip **2026**. See `data/raw/NOTES_MLS.md`.

## Are we using soccer-critical metrics?

**Yes (Gate 1):** ASA team xG for/against EMA when date+team join works; else goals EMA. Rest/travel/weather not in champ yet.

## CLI

```bash
python scripts/export_mls_totals.py   # → data/raw/mls/mls_totals_open_close.csv
dotnet run --project PossiblyMaybe.Cli -- import-mls
dotnet run --project PossiblyMaybe.Cli -- sweep-mls
```

## Gate 0 checklist

- [x] Kaggle odds-movements MLS filter + OU open/close (Pinnacle)
- [x] ASA games + xgoals join
- [x] Holdout locked (2025)
- [x] Importer `import-mls` / sweep `sweep-mls`
