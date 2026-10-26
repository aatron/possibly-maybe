# MLS Gate 0 raw data

**US retail books (DraftKings, FanDuel, and peers) post MLS.**  
All large files stay under `data/raw/` (**gitignored** — never commit the 500MB+ zip or parquets).

## Holdout lock (BEFORE metric peek — 2026-10-25)

| Slice | Rule |
|---|---|
| **Train** | `season` ≤ **2024** (calendar MLS year from Kaggle `season`) |
| **Holdout** | `season` == **2025** only |
| **2026** | Excluded from train/hold scoring (incomplete / post-lock) |

Do **not** retune on holdout. Champ = max train PnL among configs with train CLV>0, train PnL>0, bets in band; then score holdout once.

## Status (2026-10-25)

| Source | Status | Path |
|---|---|---|
| Kaggle football-odds-movements | **In** (parquet only) | `kaggle/football_odds_movements_2018_2026/` |
| MLS slice | **3507** matches | `mls/mls_matches.parquet` |
| Export for CLI | `scripts/export_mls_totals.py` → | `mls/mls_totals_open_close.csv` |
| football-data USA.csv | **In** | `mls/USA.csv` |
| ASA games + xG | **In** | `mls/asa_*.json` |

Zip source: `~/Downloads/archive(8).zip` (~517MB). Uncompressed CSVs are multi‑GB — we keep **parquet** (~128MB extracted).

## Soft vs NBA / NFL / MLB (retail)

MLS is **softer for retail bettors** than NBA/NFL/MLB on average: thinner US handle and liquidity, fewer dedicated soccer sharps in DK/FD pools, and public bias toward big-brand favorites / overs. Offshore/Pinnacle soccer markets are sharper — edge thesis is **US-book MLS totals** after NFL EPA work.

## Gate 1 path

- Import: `import-mls` — Sport=`MLS`, TOTAL open/close (Pinnacle preferred), optional ASA xG on game
- Model: team **xG for/against EMA** when ASA joinable; else **goals EMA**
- Sweep: `sweep-mls` — walk-forward; holdout locked above
- CLI: do not wipe NBA rows

## Required sources (URLs)

| Priority | Source | URL |
|---|---|---|
| **1** | Kaggle odds movements | https://www.kaggle.com/datasets/archangel0x01/football-odds-movements-2018-2026 |
| **2** | football-data USA.csv | https://www.football-data.co.uk/new/USA.csv |
| **3** | ASA games + xG | https://app.americansocceranalysis.com/api/v1/mls/games · `/xgoals` |

## Metrics / pack

See `PossiblyMaybe.Cli/SportPacks/MlsPack.md`.

## Sweep result (after holdout lock)

| | |
|---|---|
| Games imported | **3506** (xg on 3498) |
| Champ | **w=0.40**, `|exp−line|≥0.25`, EV≥4%, xG EMA (else goals) |
| Holdout 2025 | **n=93**, CLV **+0.18** goals, PnL **+12.2u** |
| Kill | **HOLD PASS** |
