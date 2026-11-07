# T3 proof: two-week ESPN line backfill

**Window:** 2026-10-24 → 2026-11-06  
**CLI:** `backfill-espn-lines [db] [from=yyyyMMdd] [to=yyyyMMdd]`  
**Owns:** `PossiblyMaybe.Cli/Import/EspnLineBackfill.cs`, `EspnLineBackfillTests`, this proof.

## Rules enforced

| Rule | How |
|---|---|
| Open + close TOTAL | ESPN core odds `open`/`close` (fallback: summary `pickcenter`); `PriceKind` Open vs Close |
| DK price when given | `AmericanOdds` from over american; else −110 with `AssumedJuice=true` |
| No cheating | Close `CapturedAt` = tipoff; open = tipoff − 7d. `AsOfView` at `SlateAsOf` sees open only |
| Single line | Stored as `Close` + `ClvQuality=CloseOnly`; `ReplayShouldSkip` → true (do not bet) |
| OregonExclusion | Counted per game; exclusion stays at bet time (odds still stored for CLV) |
| DB safety | Backup `possiblymaybe.db.bak-pre-r5-t3` before import; no `import-box` / `import-mls` |

## Coverage (warehouse games in window × ESPN)

| League | Games | Open found | Close found | Real-price snaps | Replay skip (close-only) | Fetch miss |
|---|---:|---:|---:|---:|---:|---:|
| NBA | 96 | 96 | 96 | 192 | 0 | 0 |
| MLS | 44 | 44 | 44 | 88 | 0 | 0 |
| LIGUE1 | 18 | 18 | 18 | 36 | 0 | 0 |
| LIGAMX | 19 | 19 | 19 | 38 | 0 | 0 |
| SERIEA | 30 | 30 | 30 | 60 | 0 | 0 |
| NFL | 11 | 11 | 11 | 22 | 0 | 0 |
| NCAAF | 0 | 0 | 0 | 0 | 0 | 0 |
| NCAAB | 0 | 0 | 0 | 0 | 0 | 0 |
| NHL | 97 | 97 | 97 | 194 | 0 | 0 |
| EPL | 20 | 20 | 20 | 40 | 0 | 0 |
| BUNDESLIGA | 18 | 15 | 15 | 30 | 0 | 3 |
| LALIGA | 20 | 20 | 20 | 40 | 0 | 0 |
| EFL | 36 | 36 | 36 | 72 | 0 | 0 |
| **Total** | **409** | **406** | **406** | **812** | **0** | **3** |

- NCAAF / NCAAB: warehouse has no 2026 games in this window (historical SBRO ends 2022); nothing to attach.
- Bundesliga miss = 3 Hamburger SV fixtures where ESPN returned no totals (`401884755`, `401884752`, `401884739`).
- Real-price snaps: every written open/close carried a DK american (AssumedJuice=0). Replay close-only skips: **0**.

## Leak proof (`EspnLineBackfillTests`)

| Check | Result |
|---|---|
| `AsOfView` at `SlateAsOf` sees open (`CapturedAt=tip−7d`), not close (`CapturedAt=tipoff`) | PASS |
| Same via `AsOfViewFactory.FromDbAsync` | PASS |
| `ReplayShouldSkip` true when only Close / CloseOnly | PASS |
| Parser: core open/close + pickcenter nested + single `overUnder` → close-only | PASS |

## Reproduce

```bash
cp possiblymaybe.db possiblymaybe.db.bak-pre-r5-t3   # if not already
dotnet run --project PossiblyMaybe.Cli -- backfill-espn-lines possiblymaybe.db
dotnet test PossiblyMaybe.Tests
```
