# T2b proof: Import AUS NFL totals

**Branch:** `r5/t2b` · **Worktree:** `../pm-r5-t2b`  
**CLI:** `import-nfl-aus [db]`  
**Source:** `data/raw/round3_odds/archive_org/aussportsbetting/nfl_*.xlsx`  
**DB backup:** `possiblymaybe.db.bak-pre-r5-t2b` (before import; `import-box` / `import-mls` not run)

## What shipped

1. `AusNflXlsxParser` — reads Aussportsbetting sheet; requires `Total Score Open/Close` + over/under open/close decimal odds; skips serial-1900 junk dates and rows without totals.
2. `AusNflOddsImporter` — matches **existing** NFL warehouse games only (no duplicates). Appends real-price TOTAL snapshots (`Book=Other`, `AssumedJuice=false`, O+C, over+under). **Never overwrites** older assumed −110 rows.
3. Oregon exclusion on every source row (raw names + mapped codes). **0 hits** (none expected in NFL).
4. Tests in `AusNflOddsImporterTests` (parse/junk date, odds mapping, Oregon, match-only, append-not-overwrite + idempotent re-run).

## Import run

```
AUS NFL (Aussportsbetting): files=1 parsed=3010 window=3010 matched=3010 unmatched=0 skipHaveReal=0 oregon=0 snaps=12040
```

## 2022 gap

| Metric | Value |
|---|---|
| NFL Season=2022 games | **284** |
| With TOTAL Open | **284** (was 0) |
| With AUS real-price O+C | **284 (100%)** |
| Assumed −110 close rows still present | 284 (not overwritten) |

**2022 fill status: filled.**

## Seasons with AUS real-price O→C

| Season | Games | O+C (any) | AUS real O+C | AUS real % |
|---|---:|---:|---:|---:|
| 2014 | 268 | 268 | 267 | 99.6% |
| 2015 | 280 | 280 | 267 | 95.4% |
| 2016 | 267 | 267 | 267 | 100% |
| 2017 | 267 | 267 | 267 | 100% |
| 2018 | 267 | 267 | 267 | 100% |
| 2019 | 267 | 267 | 267 | 100% |
| 2020 | 269 | 269 | 269 | 100% |
| 2021 | 286 | 286 | 285 | 99.7% |
| **2022** | **284** | **284** | **284** | **100%** |
| 2023 | 323 | 322 | 285 | 88.2% |
| 2024 | 342 | 341 | 285 | 83.3% |
| 2025 | 285 | 0 | 0 | 0% |

Calendar-year 2025 rows in the xlsx are late **2024** season (playoffs / Super Bowl) and attach to Season=2024. The xlsx has no early-2025 regular-season totals yet (archive note: 2025 partial).

## % with real prices

| Scope | Real-price games | Denominator | % |
|---|---:|---:|---:|
| AUS window games matched | 3010 | 3010 parsed with totals 2014–2025 | **100%** of imported rows carry real juice |
| Warehouse NFL Season 2014–2025 | 3010 | 3405 | **88.4%** |
| TOTAL snapshot rows 2014–2025 | 12040 real / 5995 assumed still kept | 18035 | **66.8%** of snaps are real (assumed rows retained by design) |

Headline for T6 / progress: **88.4% of 2014–2025 NFL games now have real AUS prices; 2022 is 100% filled.**

## Verify

```bash
cd ../pm-r5-t2b
dotnet test PossiblyMaybe.Tests
dotnet run -c Release --project PossiblyMaybe.Cli -- import-nfl-aus   # idempotent: skipHaveReal rises
```

Oregon: importer throws if any row hits `OregonExclusion` (run logged `oregon=0`).
