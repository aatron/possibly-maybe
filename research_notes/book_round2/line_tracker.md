# Line tracker (NFL / MLB totals, open -> close history)

`scripts/line_tracker.py` appends a snapshot of TOTALS lines per game and venue to `data/line_snapshots.db`
(sqlite, gitignored via `*.db`, table `line_snapshots`, append-only, never touches paper.db).

## Records
Per run (one `captured_at` UTC): for every pre-game ESPN event in the next 5 days (`--days`):
- `espn`: `overUnder` close line, over/under American odds (+ implied price), ESPN-reported `open_line`, provider (Draft Kings) in `source`.
- `kalshi`: every open strike of the totals series: yes(over)/no(under) bid and ask in dollars.
- `cryptocom`: every full-game `Over X` contract: `over_ask` = yes, `under_ask` = no (no bids published).
- `game_key` = `yyyymmdd AWAY@HOME HHMM` (US Eastern date + kickoff, repo team codes, e.g. WSH -> WAS for NFL), `start_utc`, `minutes_to_start`.
Games already started/finished are skipped, so the last stored snapshot is the pre-start close. Off-season (MLB now): ESPN returns 0 events, nothing is fetched or stored. Source failures are logged and skipped. Rerunning in the same second is a no-op (UNIQUE key). Runtime ~10 s.

## Schedule (not installed)
```
*/20 * * * * cd /home/a/source/projects/possibly-maybe && python3 scripts/line_tracker.py >> data/line_tracker.log 2>&1
```
Every 20 min all year is fine (no-op in off-season); tighten to 5 min inside the last 90 min before kickoff if you want a sharper close.

## Report
`python3 scripts/line_tracker.py --report`: per upcoming game, first vs latest line per venue (Kalshi/Crypto.com line = interpolated strike where mid P(over)=0.5) plus best over/under price at the ESPN line. Tests: `python3 -m unittest discover -s scripts/tests`.

## Becoming O->C data
Importer (not built): per `game_key` x `venue`, open = earliest `captured_at`, close = last with `minutes_to_start > 0`; map venue/source to `Book`, ESPN rows to `OddsSnapshot` (Market `TOTAL`, `Line`, `AmericanOdds`, `CapturedAt`), prediction-market rows via `AmericanFromBuyPrice(ask)` with `PriceKind` = exchange ask. Use the first snapshot as open only for history started by this logger (ESPN's own `open_line` is the book's real open).

## Kalshi series (public API, checked 2026-11-07)
- NFL: `KXNFLTOTAL` (open, ~30 strikes/game, event `KXNFLTOTAL-26NOV12WASNYG` = date + AWAY + HOME). Also `KXNFL1HTOTAL`, `KXNFL1QTOTAL`..`4QTOTAL`, `KXNFLTEAMTOTAL`.
- MLB: `KXMLBTOTAL` (0 open now; settled format `KXMLBTOTAL-26OCT282000MILTB` = date + ET start HHMM + codes, strikes x.5 runs). Also `KXMLBF5TOTAL`, `KXMLBTEAMTOTAL`.
- NCAAF: `KXNCAAFTOTAL` (open; many small-school games). NCAAB: `KXNCAAMBTOTAL` (men, open), `KXNCAAWBTOTAL` (women). `KXNCAABBTOTAL` is college baseball.
Add a sport by one entry in `SPORTS` (ESPN path, Kalshi series, Crypto.com `kind`); NCAA team naming for Crypto.com matching is untested.
Crypto.com: `GET /predictions/events?kind=NFL&type=match` lists games (MLB kind has no match events off-season).
