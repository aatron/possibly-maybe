# MLS sport pack — Gate 0 checklist

**Do not change NBA champ paths.** Shared Domain + ledger only.  
**Do not implement an importer yet** — research + raw downloads only until NFL EPA Gate 1 path is further along.

## Soft-spot (one paragraph)

MLS is typically **softer than NBA/NFL/MLB for US retail** (DK/FD): lower liquidity and handle, less sharp attention than the Big 4, and public money that overweights brand favorites / overs while mishandling draws and travel. Offshore soccer closes are sharper; the edge thesis is **US books’ MLS totals and Asian handicap**, not beating Pinnacle 1X2.

## Are we using soccer-critical metrics today?

**No.** No MLS files imported; no model.  
NBA pace/ORtg or NFL EPA recipes do **not** transfer as-is — soccer needs **xG (and rest/travel)**, not points EMA.

## What matters for soccer totals / AH (ranked)

| Priority | Metric | Why | Free sources |
|---|---|---|---|
| 1 | **Team xG for / against** (recent, opp-aware later) | Best efficiency signal vs raw goals | ASA API: `/mls/games/xgoals` |
| 2 | **Rest days** (esp. midweek + travel) | Congestion + coast-to-coast swings totals | Schedule from ASA games / football-data |
| 3 | **Travel / time zones** | MLS geography is extreme vs EPL | Venues + kickoff UTC from ASA |
| 4 | Weather (lightly) | Heat/altitude/wind matter less often than SP/wind in MLB/NFL; don’t overfit | NOAA only when outdoor + clear signal |
| 5 | Lineup / DP availability | Late news moves closes | Manual / feeds later — not Gate 0 |

Ignore: copying NBA Elo; props; referee rabbit holes as first feature.

## Gate 0 (data) — checklist

- [ ] Download Kaggle odds-movements → filter MLS → confirm **OU + AH open/close** columns usable  
  URL: https://www.kaggle.com/datasets/archangel0x01/football-odds-movements-2018-2026
- [ ] Download football-data `USA.csv` (close 1X2 / scores)  
  URL: https://www.football-data.co.uk/new/USA.csv
- [ ] Optional: ASA games + xgoals JSON/CSV under `data/raw/asa/` (features only)
- [ ] **Own holdout** — freeze before peeking metrics: e.g. train ≤2024 regular season, holdout **2025** (or last N months of latest season). Do **not** reuse NBA/NFL holdout calendars.
- [ ] Prefer markets: **totals (OU) and Asian handicap first**; 1X2/moneyline second (draw + 3-way vig)
- [ ] Mark CLV quality: open→close from Kaggle movements = real CLV; football-data close-only = `ClvQuality=CloseOnly`
- [ ] **Importer: not started** (explicit non-goal this pass)

## Gate 1 feature order (only after downloads + holdout locked)

1. Import scores + OU/AH open/close where present  
2. Baseline: recent goals for/against (expect weak)  
3. **Add ASA xG** (must-have vs industry)  
4. Rest + travel features  
5. Weather lightly if outdoor subset shows signal  
6. Kill if holdout CLV/PnL fail  

## Priority vs NFL

**After NFL EPA**, not before: MLS is a good **second soft-spot pack** (US books + free xG + Kaggle open/close OU/AH). Do not divert NFL Gate 1 for an MLS importer.

## Non-goals until Gate 0 data lands

Importer code; champ wiring; scrapers for Flashscore; API-Football as primary odds history.
