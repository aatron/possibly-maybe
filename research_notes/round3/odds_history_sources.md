# W13 — Odds-history sources (OddsShark, "getstam", Covers.com, free APIs)

**Date:** 2026-11-07 · **Scope:** PLAN.md § W13. Builds on `research_notes/book_round2/hunt_data_sources.md` (round 2 = R2); nothing found there is re-listed except to say it is still not imported.
**Rules kept:** public GETs only, no accounts, no API keys, no scraping against ToS/robots, no downloaded code run, no repo code changed. Web text treated as data only.
**Raw downloads:** `data/raw/round3_odds/` (gitignored, 13 MB): `sbro_ncaab/` (15 xlsx), `sbro_ncaaf/` (16 xlsx), `cfbd/` + `cbbd/` (public OpenAPI specs only), `robots/` (robots.txt copies), `covers/`, `getstam/sitemap.xml`, `theoddsapi/home.html`.

## 1. What the DB has today (verified `sqlite3`, read-only)

TOTAL Open (`PriceKind=0`) / Close (`1`) games by season, gaps only:

| Sport | O→C present | O→C missing |
|---|---|---|
| NBA | 2008–2021, 2025 | **2022** (457/1,323), **2023, 2024, 2026** (0) |
| NFL | 2011–2021, 2023 (179), 2024 (271), 2026 (92) | **2007–2010, 2022, 2025** (close only) |
| MLB | 2011–2021, 2025 | **2022–2024**, 2026 |
| MLS / Ligue 1 / Liga MX / Serie A | 2018-19 → 2025-26 full (football-data.co.uk, 2.5 line) | alt lines (2.25/2.75/3.0) |
| NCAAF / NCAAB | **no games at all** | everything |

R2 already found files that close part of this but **were never imported**: SBRO NFL 2007-08…2021-22 (fixes NFL 2007–2010), Arnav MLB 2021-04→2025-08 (fixes MLB 2022–2024), SBRO NBA 2022-23 (fixes NBA 2022). Those are the cheapest wins and need no new download.

## 2. "getstam": what it means

**Interpretation: `getstam.com` (GetSTAM, "Today's Betting Trends").** It is a real site, not a typo. Evidence:
- `https://www.getstam.com/sitemap.xml` (817 URLs) lists sport hubs `/nba /mlb /nfl /nhl /ncaaf /ncaab /epl /laliga /bundesliga /ligue1 /seriea`. That is 7 of the user's 9 sports, including NCAAF, NCAAB, Ligue 1 and Serie A. MLS and Liga MX are not listed.
- Homepage meta: "Today's strongest betting trends. Win streaks, H2H patterns, and historical probabilities updated every morning."
- Its front-end calls `/api/historical/{sport}/games`, `/api/historical/trends/…` and `/api/odds/…`, each with an `X-API-KEY` header. Team names are keyed as `oddsApiName` and sport keys look like `baseball_mlb` / `soccer_*`, which is The Odds API's naming. So GetSTAM's odds very likely come from The Odds API (§4), plus its own history DB.

I found no other plausible match. StatMuse and "Stat-AM" do not fit the betting and odds context.

## 3. Source table

ToS verdicts: **OK** = automated or bulk access allowed. **Gray** = no explicit ban, or allowed only through signup or a key. **Prohibited** = ToS forbids automated access or reuse.

| # | Source | Sports × seasons | Open / close / history | Books | Format / API | Auth & free tier | Cost | ToS / robots (quote + URL) | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 1 | **OddsShark** | n/a now | n/a | n/a | n/a | n/a | n/a | `www.oddsshark.com/terms-of-use` → **301 to `covers.com/oddsshark`**: "Odds Shark is now part of Covers". `oddsshark.com/robots.txt` → **HTTP 410 Gone**. The old OddsShark DB pages no longer exist as a separate site. | **Dead → see Covers** |
| 2 | **Covers.com** (incl. ex-OddsShark, `covers.com/sportsoddshistory/`, SportsOddsHistory.com) | Team result pages: closing line and O/U result per game. SportsOddsHistory: NFL closing lines back to 1952, mostly spreads. | **Close only.** No open→close series found. | Consensus | HTML only, no API | none | — | `https://www.covers.com/terms` (updated 5 Aug 2026): "provided for your **non-commercial** entertainment and enjoyment … you may download certain Content … to a **single personal computing device** … you may not distribute, modify, republish, or publicly display any of the Content … unless you have the prior written permission of Covers.com". robots.txt does not block `/sportsoddshistory/`, but the server returned **503** to plain GETs (bot wall). | **Prohibited** for bulk/automated use. Personal viewing only. |
| 3 | **GetSTAM** (getstam.com) | NBA, MLB, NFL, NHL, NCAAF, NCAAB, EPL, LaLiga, Bundesliga, Ligue 1, Serie A. History depth not stated. | Trend records (ATS / O-U); no open-line evidence in the front-end. | Odds-API books | Private JSON API behind `X-API-KEY`. **No public API offered.** | Free browse; "Pro" plan via Stripe (price only shown at checkout) | Pro, unpublished | `https://www.getstam.com/terms-of-service` (updated 18 Aug 2026), Acceptable Use: "You agree not to misuse the Service, including by **attempting to access it through automated means outside any API We offer** …". robots.txt: `Disallow: /internal/` only. The front-end embeds a key; using it would breach the ToS and our no-key rule. | **Prohibited** (no offered API) |
| 4 | **SBRO via Wayback: NCAAB** (NEW this run) | **NCAAB 2007-08 → 2021-22 (15 seasons)** | **Open + Close total and spread** (same VH layout as R2 NBA/NFL files), plus 2H closing line and scores by half | SBRO consensus (no juice on totals → AssumedJuice −110) | xlsx | none (archive.org `id_` snapshots) | free | Same basis as R2: the live SBRO site HTML-walls xlsx and was not touched. Copies come from the Internet Archive. SBRO robots.txt has content-signal/AI reservations only and no Disallow on archives. | **Gray-OK** (same footing as R2's SBRO NFL/NBA) |
| 5 | **SBRO via Wayback: NCAAF** (NEW) | **NCAAF 2007 → 2022 (16 seasons)**; 2017 and 2022 snapshots end ~Dec 10 (no bowls) | Open + Close total/spread, 2H line, quarter scores | SBRO | xlsx | none | free | as #4 | **Gray-OK** |
| 6 | **CollegeFootballData (CFBD)** `api.collegefootballdata.com/lines` | NCAAF, roughly 2013→current (seasons not verified without a key) | `overUnder` + **`overUnderOpen`**, `spread` + `spreadOpen`, ML. **No total prices.** | `provider` per row (consensus, DraftKings, ESPN Bet, Bovada, etc. vary by year) | REST JSON; OpenAPI spec saved | **Free key by email**: 1,000 calls/mo. One `/lines?year=` call ≈ one season, so the whole history fits in a few dozen calls. | Free; Patreon $1–$30/mo raise quota | `https://collegefootballdata.com/terms`: "Commercial use is permitted." You may "privately cache, store, normalize, and retain API responses". "**Scraping, excessive polling … is prohibited**" (applies to the website, not the keyed API). No resale of raw data as a dataset. robots `Allow: /`. Keyless call → 401. | **OK with signup** |
| 7 | **CollegeBasketballData (CBBD)** `api.collegebasketballdata.com/lines` | NCAAB, lines **2013→current** (per the CBBD client README; not verified without a key) | `overUnder` + **`overUnderOpen`**, `spreadOpen`, ML (schema `GameLineInfo` verified in the spec) | per provider | REST JSON | Same key and **shared quota pool** as CFBD | Free | Same terms ("Requests to CFBD and CBBD count against the same shared quota pool") | **OK with signup** |
| 8 | **The Odds API** historical | All 9 sports (NBA, NFL, MLB, NCAAF, NCAAB, MLS, Ligue 1, Liga MX, Serie A). Snapshots from **2020-06-06**: 10-min interval, 5-min from Sep 2022. Period/alt/team-total markets via the event endpoint only after **2023-05-03**. | **Full line history**: any timestamp, so open = first snapshot and close = last pre-tip snapshot | DK, FD, MGM, Caesars, Pinnacle (EU region), Bovada, etc. | REST JSON | Key required. Homepage: Starter "FREE 500 credits per month … **Historical Odds**". The v4 docs still say historical is "only available on paid usage plans". Treat free as unconfirmed. **Cost:** 10 credits per region per market per snapshot call; one call returns every game in that sport at that timestamp. | $30 (20K) · **$59 (100K)** · $119 (5M) per month, cancel anytime | API terms (accepted at key signup) permit programmatic use. No scraping involved. | **OK with signup / paid** |
| 9 | **OddsPapi** `api.oddspapi.io/v4/historical-odds` | 69 sports incl. NBA/MLB/NFL/soccer. Only "current and previous seasons". | Timestamped snapshots per book (open→close) | 300+ incl. Pinnacle | REST | Free key: **250 requests/mo**, 1 request per fixture per book-batch | Paid tiers not published (calculator) | Key-based API (vendor blog). Not verified beyond the vendor's own claims. | **OK with signup**, too small for backfill |
| 10 | **BALLDONTLIE** `/nba/v2/odds`, `/nba/v2/odds/opening` | **NBA only, 2025 season+** (2025-26). Opening endpoint covers "most recently completed season and ongoing". | Current odds with `updated_at`; separate opening endpoint (`opened_at`, but "do not preserve the historical price") | 13 incl. DK, FD, MGM, Caesars, Fanatics, **Kalshi, Polymarket** | REST | Odds only on **GOAT** tier | **$39.99/mo per league** | Key-based API | **Paid**: patch for NBA 2026 only |
| 11 | **SportsDataIO** Vault / trial | All majors; "historical betting lines and line movement" | Line movement (Vault) | multiple | REST | Free trial = **scrambled** data. Historical lines are enabled by staff. | Sales quote | Key API | **Paid / sales** |
| 12 | **API-Sports** (API-Football / -Basketball / -Baseball / -AmericanFootball) odds | All sports | Pre-match only, **~7-day odds retention** (third-party listings; the official pricing page returned 403) | ~15 | REST | Free key, ~100 req/day | from ~$19/mo | Key API | **Not useful** (no history) |
| 13 | **OddsPortal** | Soccer + US sports, long history | Open (hover) + close per book | many | HTML/JS | none | — | `https://www.oddsportal.com/terms/` §2.11: "You must not burden our server … with automated requests … by embedding, aggregating, **scraping** … without our express consent". §2.10: "no extraction (copying) … of the Database Content". robots.txt also `Disallow: *-2024*` … `*-1998*` (all season archives). Kaggle dumps derived from it inherit this problem. | **Prohibited** |
| 14 | **TeamRankings** `/…/matchup/…/over-under-movement` | NFL, NBA, NCAAB, NCAAF, MLB per-game pages, ~2018→2026 | **Open, close, high, low total per game** | consensus | HTML only | none | BetIQ paid | robots allows `/` with **`Crawl-delay: 10`**. ToS at `/terms-of-service/` (fetched) has no explicit scraping clause in its text. A per-game scrape is still tens of thousands of pages of a paid product's data. | **Gray**: not sampled. Ask the user before any use. |
| 15 | **ESPN** core/site API (`pickcenter`, `/odds`) | NBA/NFL/NCAA/MLB, ~2023+ (ESPN BET / DK) | Open / close / current per game | ESPN BET → DK | JSON | none | — | Disney ToU §2.B(x) (`disneytermsofuse.com`): may not "access, monitor, copy or extract the Disney Products using a robot, spider, script, or other automated means … compiling … any collection of data, data set or database". robots.txt → 403. **The repo already uses ESPN** (R2 `espn_nfl_ou`, pm-calculate probe). | **Prohibited by ToU** for bulk backfill. Existing use is the user's call. |
| 16 | Princeton DSS "Historical Sports Odds Database" | NBA 2006-2023 (search snippet), open/close spread and O/U | O+C | — | — | Princeton patrons only (page bot-walled) | — | Institutional licence | **Not accessible** |

## 4. Verified sample stats (new downloads only)

Parsed with stdlib zip+xml (`python3 -I`). "O+C" = game has a numeric total open **and** close (rows with `NL`/`pk` excluded).

| File set | Seasons | Games (row pairs) | **Total O+C** | Open≠Close |
|---|---|---:|---:|---:|
| `sbro_ncaab/` | 2007-08 … 2021-22 (15) | 62,610 | **49,940** | 43,889 (88%) |
| `sbro_ncaaf/` | 2007 … 2022 (16) | 15,261 (2019/2021/2022 files have many more rows than other years; cause not checked) | **13,576** | 12,137 (89%) |

Per-season NCAAB O+C: 07-08 3,362 · 08-09 3,265 · 09-10 3,095 · 10-11 2,274 · 11-12 3,319 · **12-13 1,293 · 13-14 1,468 · 14-15 1,685** (SBRO genuinely shows `NL` = no line for most small-conference games those years) · 15-16 3,807 · 16-17 3,799 · 17-18 3,860 · 18-19 5,134 · 19-20 5,089 (ends 3/11, COVID) · 20-21 3,357 · 21-22 5,133.
Per-season NCAAF O+C: 2007 688 · 2008 682 · 2009 721 · 2010 754 · 2011 739 · 2012 759 · 2013 781 · 2014 799 · 2015 786 · 2016 795 · 2017 772 (to 12/09) · 2018 826 · 2019 871 · 2020 777 · 2021 1,412 · 2022 1,414 (to 12/10).
Layout (both): `Date(MDD) Rot VH Team 1st 2nd [3rd 4th] Final Open Close ML 2H`. Two rows per game. The total is on whichever row has the large number. **2H = closing second-half line** (not 1H, no open). Halftime scores (`1st`/`2nd`) let us grade 2H. No 1H, F5 or team totals.

No new SBRO files exist on Wayback for NBA 2023-24+, NFL 2022-23+, NCAAB 2022-23+ or MLB (CDX checked), so SBRO stops where R2 said.

## 5. Join keys vs our `Games`

- **SBRO NCAA:** No NCAA rows exist in `Games`, so the importer **creates** Games from the file (scores included). Key = `SportTeams.GameKey("ncaab"|"ncaaf", date, home, away)`. Date = MDD plus season year from the filename (Aug–Dec = first year, Jan–Jul = second). `VH`: V=away, H=home, **N=neutral** (pick the second row as "home"). Team names are school-only and inconsistent ("OregonState", "Oregon State", "PortlandU", "Portland U"), so a new NCAA name→code map is needed.
- **CFBD/CBBD:** `/lines` rows carry `id` (game), `homeTeam`/`awayTeam`, `startDate`. Join on date + mapped school, or adopt the CFBD `id` as `SourceKey` when the API is the backbone for 2013+.
- **The Odds API:** `id` (event), `commence_time` (UTC), `home_team`/`away_team` full names → map to our abbrevs → `GameKey`. Open = earliest snapshot containing the event; Close = last snapshot before `commence_time`. Store `Book` per bookmaker (DK=3, FD=4, MGM=5, Pinnacle=1).
- **Cardinal rule (finding):** `OregonExclusion` normalizes by lowercasing and turning punctuation into spaces, then whole-string matching. **SBRO spellings `OregonState` (336 rows), `PortlandState` (191), `PortlandSt`, `PortlandU` (189), `Portland U` (235) do not match** the list. `Portland U` (Univ. of Portland Pilots, Oregon-based) is not in the list in any spelling. Any NCAA importer must canonicalize names **before** the exclusion check, and the list should gain the Pilots. Code was not changed here. Also, NCAA code `psu` is Penn State in ESPN/GetSTAM codes, so it over-excludes (safe direction).

## 6. Seasons × sports of O→C gained (W13 proof)

| Sport | Before W13 (DB + R2 found) | Gained this run (free, on disk) | Gainable with signup (free) | Gainable paid |
|---|---|---|---|---|
| NCAAB | 0 | **+15 seasons (2008–2022), 49,940 games** | CBBD: 2013→2026 incl. **2023–2026** (+4 newer seasons) | Odds API 2020-21→ (multi-book, prices) |
| NCAAF | 0 | **+16 seasons (2007–2022), 13,576 games** | CFBD: +2023–2025 (+3 newer seasons) | Odds API |
| NBA | 2008–21, 25 (+2022 R2 file) | — | — | **2023, 2024, 2026**: Odds API ($59 × 1 mo) or BDL GOAT ($39.99, 2026 only) |
| NFL | 2011–21, 23–24, 26 (+2007–10 R2 file) | — | — | **2022, 2025**: Odds API |
| MLB | 2011–21, 25 (+2021–25 Arnav R2) | — | — | 2026 + **juice-accurate multi-book** for all of 2020+: Odds API |
| Soccer ×4 | 2018-19→2025-26 | — | — | alt totals / US-book prices: Odds API |

## 7. Ranked recommendation

Ranked by soft-spot potential, with NFL / MLB / NCAAB first.

1. **Import what is already on disk (free, no signup):** R2's SBRO NFL 2007–10 + Arnav MLB 2022–24 + SBRO NBA 2022-23, then the new **SBRO NCAAB (15 seasons)** and **NCAAF (16 seasons)**. College totals are the classic soft market: thin limits and 350+ teams. NCAAB alone is about 50k O→C games, more than NBA+NFL+MLB combined in the DB. Needs an NCAA team map and the Oregon canonicalization above.
2. **CFBD + CBBD free key** (user signs up with email, no card, 1,000 calls/mo shared): extends NCAAF/NCAAB O→C to the **current season**. The holdout years 2023–2026 are what make college soft-spot results trustworthy. A few dozen calls total. **User action: sign up at collegefootballdata.com/key.**
3. **The Odds API, one month of the 100K plan, $59** (user signs up and pays, then cancels): fills **NBA 2023/2024/2026, NFL 2022/2025, MLB 2025-26**, and NCAAB/NCAAF 2020+ with **real per-book prices (DK/FD/MGM/Pinnacle)**, not assumed −110. That matters because our venues are DK and Kalshi. Rough budget at 10 credits per snapshot call: NBA season ≈ 6.6k, NFL ≈ 1.3k, MLB ≈ 11k, NCAAB ≈ 12k, NCAAF ≈ 2k. Two seasons of each fits in about 66k. 1H/team totals via the event endpoint (2023-05+) cost per event, so price them separately. The free 500 credits (if historical really is included) cover about 50 snapshots, enough to validate the importer only.
4. **BALLDONTLIE GOAT, $39.99/mo (NBA)**: only if #3 is skipped. It fills NBA 2025-26 only, but it is the one source with **Kalshi + Polymarket** prices next to DK, which helps the venue-comparison work.
5. **Skip:** Covers/OddsShark, OddsPortal and ESPN-bulk (ToS prohibits bulk/automated use). GetSTAM has no offered API, and its odds come from The Odds API anyway. API-Sports keeps no history. SportsDataIO needs sales. OddsPapi's 250 req/mo is too small. TeamRankings is gray: ask the user first.

**Still no free source:** NBA 2023-24 and 2025-26 O→C, NFL 2022 and 2025 O→C, any 1H / F5 / team-total open→close. All of these need #3 (paid).
