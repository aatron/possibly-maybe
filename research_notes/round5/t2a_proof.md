# T2a proof: Import NHL SBRO

**Branch:** `r5/t2a`  
**CLI:** `import-nhl-sbro [db] [dir]`  
**Source:** `data/raw/round3_odds/archive_org/sbro_nhl/*.xlsx` (16 on disk; **2022-23 skipped**)  
**DB:** `possiblymaybe.db` (backed up to `possiblymaybe.db.bak-pre-r5-t2a` before import)

## Behaviour

| Item | Result |
|---|---|
| Parser | `SbroXlsxParser.ParseNhlFile` — totals from `OpenOU`/`CloseOU`; over juice from the unlabeled column after each; ML `Open`/`Close` and `PuckLine` ignored for totals |
| Snapshots | `Book=Consensus`, `Market=TOTAL`, `Side=over`, `PriceKind` Open+Close, `ClvQuality=OpenClose`, `AssumedJuice=false` when juice present (consensus O→C ≠ same-book venue close) |
| Skip | `nhl_odds_2022-23.xlsx` (mid-season only) |
| Short season | `nhl_odds_2021.xlsx` → season **2020-2021** |
| Oregon | Importer asserts 0 Oregon-excluded rows; SBRO cities include **Anaheim** (not “Ducks”) |
| Unmapped | 0 |

## Coverage (after import)

| Season | Games | O+C | Real-juice snaps |
|---|---:|---:|---:|
| 2007-2008 | 1315 | 1315 | 2630 |
| 2008-2009 | 1316 | 1316 | 2632 |
| 2009-2010 | 1318 | 1318 | 2636 |
| 2010-2011 | 1319 | 1319 | 2638 |
| 2011-2012 | 1315 | 1315 | 2630 |
| 2012-2013 | 806 | 806 | 1612 |
| 2013-2014 | 1322 | 1322 | 2644 |
| 2014-2015 | 1319 | 1319 | 2638 |
| 2015-2016 | 1321 | 1321 | 2642 |
| 2016-2017 | 1317 | 1317 | 2634 |
| 2017-2018 | 1355 | 1355 | 2710 |
| 2018-2019 | 1358 | 1358 | 2716 |
| 2019-2020 | 1212 | 1212 | 2424 |
| 2020-2021 | 950 | 950 | 1899 |
| 2021-2022 | 1400 | 1400 | 2800 |
| **SBRO total** | **18943** | **18943** | **~37881** |
| 2026-2027 (live ESPN, pre-existing) | 212 | 0 | 0 |

**O+C coverage on imported seasons: 100%** (18943 / 18943). Fifteen full/partial historical seasons; 2022-23 not imported.

## Anaheim ≠ Oregon

- File spelling: `Anaheim` → `SportTeams.TryNhl` → `ANA`.
- `OregonExclusion.IsExcluded("NHL", "Anaheim"|"Anaheim Ducks"|"ANA")` is **false**.
- Oregon names (`Oregon Ducks`, `Portland Winterhawks`, …) remain excluded under `NHL`.
- Import run: `oregon=0`.

## Tests

`dotnet test PossiblyMaybe.Tests` — green (includes Anaheim exclusion + NHL SBRO parser/importer tests).
