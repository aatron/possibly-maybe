# A2 proof: line tracker → all leagues + OddsSnapshots import

**Branch:** `r4/a2`  
**Owns:** `scripts/line_tracker.py`, `scripts/tests/*`, `PossiblyMaybe.Cli/Import/LineTrackerImporter.cs`

## What changed

| Before | After |
|---|---|
| `SPORTS` = NFL, MLB only | NBA, NCAAB, NCAAF, NFL, MLB, MLS, LIGUE1, LIGAMX, SERIEA, LALIGA, EFL (+ optional `data/line_tracker_sports.json` merge for census leagues) |
| Missing venue → crash risk on hard-coded keys | `kalshi` / `cdc` optional (`None` → SKIP log, continue) |
| No inter-league pacing | `DAY_SLEEP_S` / `LEAGUE_SLEEP_S` + exponential backoff on HTTP fail |
| Importer `FindOrCreateGame` invented warehouse rows | **Match-only** via sport + resolved teams + tipoff; unmatched venue-groups counted, never new `GameId`s |
| Totals only (NFL/MLB) | Still **totals-first**; spread / team-total / 1H deferred (`shortcut:` in `line_tracker.py`) |

## Coverage methodology (slate games with true open + close)

**Definition:** a slate game has *true O+C* when `line_snapshots` has ≥2 distinct `captured_at` for that `game_key` with the earliest usable as open and a last row with `minutes_to_start > 0` as close (same rule as `LineTrackerImporter.ResolveOpenClose`). ESPN-reported `open_line` counts as open when present.

**Measure (per league):**

```bash
# 1) Capture over a slate window (cron every 20 min recommended)
python3 scripts/line_tracker.py --sports ALL --days 5

# 2) Import into warehouse OddsSnapshots (never paper.db)
dotnet run --project PossiblyMaybe.Cli -- import-line-tracker

# 3) Read the coverage lines the importer prints:
#    line-tracker coverage: N/M games (P%) have open+close
#      NBA: …   NFL: …   etc.
```

**SQL check on snapshots alone** (no warehouse needed):

```sql
WITH bounds AS (
  SELECT sport, game_key,
         MIN(captured_at) AS first_at,
         MAX(CASE WHEN minutes_to_start > 0 THEN captured_at END) AS close_at
  FROM line_snapshots GROUP BY sport, game_key
)
SELECT sport,
       COUNT(*) AS games,
       SUM(CASE WHEN first_at IS NOT NULL AND close_at IS NOT NULL
                 AND first_at < close_at THEN 1 ELSE 0 END) AS with_oc
FROM bounds GROUP BY sport;
```

Target: **≥ 80%** of slate games per live league after ≥1 day of cron (two captures before tip).

## Before → after (this worktree)

| League | Before (tracker) | After (configured) | Measured O+C % |
|---|---|---|---|
| NFL | tracked | tracked | *n/a — `data/line_snapshots.db` empty in this worktree* |
| MLB | tracked | tracked | *n/a (off-season likely 0 ESPN games)* |
| NBA, NCAAB, NCAAF, MLS, LIGUE1, LIGAMX, SERIEA | **0%** (not in `SPORTS`) | ESPN + Kalshi where series known; CDC where kind known | *run cron → re-measure* |
| LALIGA, EFL | **0%** | ESPN only (no public Kalshi/CDC series wired) | ESPN O+C still counts toward target |

**Expected after first live week:** ESPN-backed leagues with regular scoreboard odds ≥ 80% O+C if cron runs through tip; Kalshi/CDC attach rates vary by series depth and team-code matching (soccer abbr ↔ warehouse names resolved in importer via `SportTeams`).

## Tests

```bash
python3 -m unittest discover -s scripts/tests -v
dotnet test PossiblyMaybe.Tests --filter LineTrackerImporter
```

Unit tests use fixtures only (no live API).

## Leftover risk

- Kalshi soccer / NCAA ticker team codes may not match ESPN abbr → Kalshi rows missing (ESPN still tracked).
- Crypto.com kinds beyond NBA/MLS/NFL/MLB unknown → SKIP until census (A4) fills `line_tracker_sports.json`.
- Rate limits: backoff helps; a full `ALL` pass still fans out one ESPN call per day × league.
- Importer skips snapshots that do not match an existing warehouse `Games` row (by design).
