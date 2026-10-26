# MLS Gate 0 raw data — pending downloads

Kaggle requires login. Fetch manually into `data/raw/` (gitignored); do not commit the CSVs.
**US retail books (DraftKings, FanDuel, and peers) post MLS** — 1X2 / moneyline, Asian handicap (spread), and goal totals — so a pack is tradeable once data is in.

## Soft vs NBA / NFL / MLB (retail)

MLS is **softer for retail bettors** than NBA/NFL/MLB on average: thinner US handle and liquidity, fewer dedicated soccer sharps in DK/FD pools, and public bias toward big-brand favorites / overs / “America knows soccer less than basketball.” Draw + 3-way ML also misprices casual money vs 2-way US sports. Not a free lunch — offshore/Pinnacle soccer markets are sharper — but **DK/FD MLS totals and AH are a credible soft-spot candidate** after NFL EPA work, not instead of it.

## Required (human download) — URLs

| Priority | Source | URL | What you get |
|---|---|---|---|
| **1** | Kaggle — odds movements (open→close) | https://www.kaggle.com/datasets/archangel0x01/football-odds-movements-2018-2026 | **Best CLV candidate.** ~3.5k MLS matches (2019–2026); 1X2 + **AH** + **OU** with timestamped moves; `match_closing` has open/close. Filter competition = MLS. |
| **2** | football-data.co.uk USA (free CSV, no auth) | https://www.football-data.co.uk/new/USA.csv | Results + **closing** 1X2 (Pinnacle / market avg/max). Index: https://www.football-data.co.uk/new_leagues.php — **no totals/AH columns** on the extra-league file. |
| **3** | Extra-leagues hub | https://www.football-data.co.uk/all_new_data.php | Same USA file + Excel; updated ~2×/week. |
| **4** | Kaggle — MLS results/events (no modern odds) | https://www.kaggle.com/datasets/josephvm/major-league-soccer-dataset | Matches/events through ~2021 — scores backbone only; stale for betting. |
| **5** | Kaggle — closing ML sample (9 sports incl. MLS) | https://www.kaggle.com/datasets/oliviersportsdata/us-sports-master-historical-closing-odds | Free **sample** rows; full MLS ~9k games is paid (Gumroad). Close **1X2 only**, no open, no totals. |

Suggested local paths after unpack:

- `data/raw/kaggle/football_odds_movements_2018_2026/` — filter MLS
- `data/raw/football_data/USA.csv` — from football-data.co.uk
- `data/raw/kaggle/mls_josephvm/` — optional scores/events

## Free / cheap (no Kaggle)

| Source | URL | Caveat |
|---|---|---|
| ASA games + xG (free API) | https://app.americansocceranalysis.com/api/v1/mls/games · https://app.americansocceranalysis.com/api/v1/mls/games/xgoals | No key. Client: https://github.com/American-Soccer-Analysis/itscalledsoccer — **metrics**, not book lines. |
| ASA interactive | https://www.americansocceranalysis.com/interactivegamebygame | Browser export / browse for spot checks. |
| The Odds Gap MLS sample | https://theoddsgap.com/data/mls · `curl -o mls.csv "https://theoddsgap.com/api/odds-history.csv?sport=soccer_usa_mls"` | Free sample week only; full history is sub; starts mid-2026 — **too short for Gate 1 alone**. |
| footballcsv MLS (results) | https://github.com/footballcsv/major-league-soccer | Scores/fixtures style dumps; verify license/freshness before use. |

## Flashscore dumps

Flashscore is a common **results** source (football-data.co.uk cites it). There is **no official bulk free odds archive**.

| Option | URL | Caveat |
|---|---|---|
| League archive scrapers (Apify etc.) | e.g. https://apify.com/incognito_mode/flashscore-league-archive-scraper | Results/tables; paid compute; ToS risk. |
| Odds+results actors | e.g. https://apify.com/sports-odds-lab/football-odds-results (league `USA: MLS`) | Open/close 1X2/OU/AH possible; **not free**, scrape-fragile — prefer Kaggle #1 if it covers your seasons. |

Prefer packaged Kaggle / football-data over DIY Flashscore scrapes for Gate 0.

## API-Football free tier — caveats

| | |
|---|---|
| Docs / pricing | https://www.api-football.com/documentation-v3 · https://www.api-football.com/pricing |
| Free plan | **100 req/day**, ~30/min; all endpoints listed, but **season depth is gated** — free often locked to older seasons (reports of “try 2021–2023” when asking for 2024/25). |
| MLS season param | Calendar year (e.g. `season=2025`); league id varies by catalog — confirm via `/leagues`. |
| Odds | Pre-match odds endpoints exist but are **not a multi-year open/close totals archive**; burning 100 calls on fixture+odds pagination is easy. |
| Lab stance | Fine for live fixture probes; **not** the Gate 0 CLV spine. Use Kaggle odds-movements + football-data close ML. |

## Status

| Source | Status |
|---|---|
| archangel0x01/football-odds-movements-2018-2026 | **Not downloaded** |
| football-data.co.uk `USA.csv` | **Not downloaded** |
| ASA xG / games | **Not pulled** |
| josephvm MLS dataset | **Not downloaded** (optional) |

**Metrics / holdout:** see `PossiblyMaybe.Cli/SportPacks/MlsPack.md`.  
**Do not implement an MLS importer yet.**
