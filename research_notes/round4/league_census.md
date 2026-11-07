# A4 — League / venue census (feeds C6)

**Date:** 2026-11-07 · **Agent:** Cursor Auto/Composer · **Scope:** research notes only (PLAN A4).  
**Rules:** public GETs + existing notes; no venue auth, no bets, no production code, no PLAN/PROGRESS edits.  
**Sources reused:** `round3/odds_history_sources.md`, `book_round2/{hunt_data_sources,venue_placement,line_tracker}.md`, `data/raw/NOTES_*.md`, `OregonExclusion.cs`, `scripts/{kalshi_compare,line_tracker}.py`, `KalshiQuotes.cs` / `CryptoComQuotes.cs`.  
**Live probes (unauth):** Kalshi `GET /series/{ticker}` + open markets; Crypto.com `GET /predictions/events?kind=…&type=match`; football-data.co.uk CSV heads.

**Proof:** ranked C6 candidate list below. Existing paper/shadow leagues (NBA, MLS, Ligue 1, Liga MX, Serie A) and Wave-C packs (C1–C5) are listed for completeness but are **not** C6.

---

## 1. Ranked C6 candidates

Score = **data** (free results + historical O→C for Tier A) × **venues** (DK + Kalshi + Crypto/RH coverage for live shadow) × **Oregon** (lower risk = better). Failures demote to shadow-only.

| Rank | League | C6 id | Flag | Why |
|---:|---|---|---|---|
| **1** | **EPL** | `C6-epl` | **ready for C6-epl** | football-data `E0` free results + open/close OU 2.5; Kalshi `KXEPLTOTAL` + spreads live; Crypto.com `kind=EPL` live; DK posts EPL; **no Oregon clubs** |
| **2** | **Bundesliga** | `C6-bundesliga` | **ready for C6-bundesliga** | football-data `D1` same pattern; Kalshi `KXBUNDESLIGATOTAL` live; DK posts; Crypto.com kind not aliased (use search / later kind map) — still Tier-A-able on FD; **no Oregon** |
| **3** | **NHL** | `C6-nhl` | **ready for C6-nhl** (Tier A gated) | Kalshi `KXNHLTOTAL` dense (189 open mkts probed); Crypto.com `kind=NHL` live; DK game lines; ESPN free scores. **Historical free O→C thin** → start **shadow + line_tracker**, promote Tier A only after O→C fill (Odds API / tracker age). Oregon: none in NHL (Winterhawks already excluded as names, not NHL) |
| **4** | **UCL** | `C6-ucl` | **shadow-only / thin history** | Kalshi `KXUCLTOTAL` exists (0 open at probe — schedule-dependent); Crypto.com `kind=UCL` live; DK posts UCL. No football-data-style multi-season totals archive like E0/D1 → **shadow + live tracker**, not Tier A first |
| **5** | **NCAAW** | `C6-ncaaw` | **shadow-only until O→C** | Kalshi `KXNCAAWBTOTAL` very dense (200+ open); Crypto.com `kind=CBB` mixes M/W (matching fragile); DK posts WBB. Free multi-season women O→C **not** in SBRO W13 haul (men only). Oregon: **Oregon / Oregon St / Portland St / Portland Pilots** — codes already under NCAA |
| **6** | **NWSL** | `C6-nwsl` | **shadow-only / high Oregon** | Kalshi `KXNWSLTOTAL` (6 open at probe); DK posts NWSL; Crypto.com `kind=NWSL` empty off-window. Free O→C sparse. **Portland Thorns** every season → many skipped tickets |
| **7** | **WNBA** | `C6-wnba` | **shadow-only / seasonal + Oregon** | Kalshi `KXWNBATOTAL` (0 open off-season); DK posts; Crypto.com empty off-season. **Portland Fire** (`por`) excluded. Wait for 2027 season + tracker history before Tier A |

**C6 priority order (run these):** `epl` → `bundesliga` → `nhl` → `ucl` → `ncaaw` → `nwsl` → `wnba`.

**Not C6** (already planned elsewhere): NCAAB=C1, NCAAF=C2, NFL=C3, MLB=C4, La Liga+EFL=C5. Paper stack: NBA + MLS + Ligue 1 + Liga MX + Serie A.

---

## 2. Per-league sheet

Legend — **Venues:** Totals / Spreads presence. **RH** = Robinhood sports event contracts ≈ Kalshi asks (separate bankroll; no RH order API). **O→C** = historical open→close totals usable for CLV. **Oregon** = names to add/keep in `OregonExclusion` (normalize before match).

### 2.1 Already in paper / Wave C (context)

| League | DK | Kalshi totals (+ related) | Crypto.com `kind` | RH | Free results | O→C source | Oregon exclude | Flag |
|---|---|---|---|---|---|---|---|---|
| **NBA** | Totals + spreads | `KXNBATOTAL`, `KXNBASPREAD`, `KXNBATEAMTOTAL` | `NBA` (wired) | via Kalshi | ESPN scoreboard/summary (repo) | SBRO/Kaggle/minmaxer in DB; gaps 2023/2024/2026 | Trail Blazers / Portland / `por` | live paper (not C6) |
| **MLS** | Totals + spreads | `KXMLSTOTAL`, `KXMLSSPREAD` | `MLS` (wired) | via Kalshi | ESPN + ASA xG | football-data USA / Kaggle movements | Timbers / Portland / `por` | live paper |
| **Ligue 1** | Totals | `KXLIGUE1TOTAL`, `KXLIGUE1SPREAD` | no stable kind found | via Kalshi | ESPN; football-data `F1` | FD + warehouse 2018→ | none | live paper |
| **Serie A** | Totals | `KXSERIEATOTAL`, `KXSERIEASPREAD` | no stable kind | via Kalshi | ESPN; FD `I1` | FD + warehouse | none | live paper |
| **Liga MX** | Totals | `KXLIGAMXTOTAL` | no stable kind | via Kalshi | ESPN | FD-style / warehouse | none | live paper |
| **NCAAB** | Totals + spreads | `KXNCAAMBTOTAL`, `KXNCAAMBSPREAD` | `CBB` (untested match) | via Kalshi | ESPN | SBRO Wayback 2007–22; CBBD keyed | Oregon, Oregon St, Portland St, Portland Pilots (+ codes `ore`/`orst`/`port`/`up`) | **C1** |
| **NCAAF** | Totals + spreads | `KXNCAAFTOTAL`, `KXNCAAFSPREAD`, `KXNCAAF1HTOTAL`, team totals | `CFB` | via Kalshi | ESPN | SBRO 2007–22; CFBD keyed | Oregon, Oregon St (+ Portland St / Pilots if listed) | **C2** |
| **NFL** | Totals + spreads + 1H | `KXNFLTOTAL`, `KXNFLSPREAD`, `KXNFL1HTOTAL`, Q totals, `KXNFLTEAMTOTAL` | `NFL` | via Kalshi | nflverse + ESPN | SBRO/flancast + ESPN O→C (gaps 2022/2025) | none (Vikings ≠ Portland St) | **C3** |
| **MLB** | Totals + spreads + F5 | `KXMLBTOTAL`, `KXMLBF5TOTAL`, `KXMLBTEAMTOTAL` | `MLB` (off-season empty) | via Kalshi | ESPN | flancast/Arnav + minmaxer | Hillsboro Hops / Eugene Emeralds / Portland Pickles (not MLB clubs) | **C4** |
| **La Liga** | Totals | `KXLALIGATOTAL`, `KXLALIGASPREAD` | no kind; try later | via Kalshi | ESPN; FD `SP1` | FD (in warehouse history) | none | **C5** |
| **EFL Championship** | Totals | `KXEFLCHAMPIONSHIPTOTAL` (0 open at probe) | no kind | via Kalshi | ESPN; FD `E1` | FD | none | **C5** |

### 2.2 C6 candidates (detail)

#### EPL — **ready for C6-epl**
| | |
|---|---|
| **Venues** | **DK:** ML / spread / totals. **Kalshi:** `KXEPLTOTAL`, `KXEPLSPREAD`, `KXEPLGAME`, BTTS/goal series (machina list). **Crypto.com:** `kind=EPL` match events live. **RH:** Kalshi-routed when listed. |
| **Free results** | football-data.co.uk `mmz4281/{yy}{yy+1}/E0.csv`; ESPN soccer scoreboard |
| **O→C** | FD open + closing (`…C` columns) OU 2.5 / AH / 1X2 since ~2000s; same importer pattern as Serie A / Ligue 1 |
| **Oregon** | none |
| **Notes** | Soft-spot thesis same as other FD soccer: US retail / prediction-market juice vs offshore close |

#### Bundesliga — **ready for C6-bundesliga**
| | |
|---|---|
| **Venues** | **DK:** yes. **Kalshi:** `KXBUNDESLIGATOTAL`, `KXBUNDESLIGASPREAD`. **Crypto.com:** no `Bundesliga`/`BUNDESLIGA` kind hit — discover via event search or wait for kind alias; still list on help-center soccer set. **RH:** via Kalshi. |
| **Free results** | FD `D1.csv`; ESPN |
| **O→C** | FD same as EPL |
| **Oregon** | none |

#### NHL — **ready for C6-nhl** (Tier A after O→C)
| | |
|---|---|
| **Venues** | **DK:** ML / spread / totals. **Kalshi:** `KXNHLTOTAL`, `KXNHLSPREAD` (dense). **Crypto.com:** `kind=NHL` live. **RH:** via Kalshi. |
| **Free results** | ESPN `hockey/nhl` scoreboard (same pattern as `line_tracker`) |
| **O→C** | No free multi-season SBRO-class dump found in r2/r3. **Path:** A2 `line_tracker` + ESPN open_line → grow O→C; optional Odds API paid for backfill |
| **Oregon** | No NHL franchise. Keep `portland winterhawks` / `winterhawks` (WHL) so alias bugs never map wrong |

#### UCL — **shadow-only / no Tier-A archive**
| | |
|---|---|
| **Venues** | **DK:** yes. **Kalshi:** `KXUCLTOTAL`, `KXUCLSPREAD` (0 open at probe). **Crypto.com:** `kind=UCL` live. Also Kalshi `KXUELTOTAL`, `KXUECLTOTAL`, `KXFACUPTOTAL` for satellite cups. |
| **Free results** | ESPN UCL scoreboard |
| **O→C** | No FD division file; sparse free history → **shadow-only** until tracker/Odds API |
| **Oregon** | none |

#### NCAAW — **shadow-only / no data for Tier A**
| | |
|---|---|
| **Venues** | **DK:** College Basketball (W). **Kalshi:** `KXNCAAWBTOTAL`. **Crypto.com:** `CBB` (gender mixed — do not trust without titles). |
| **Free results** | ESPN women's scoreboard |
| **O→C** | W13 SBRO haul was **men only**. CBBD may not cover women. Flag **no data → shadow-only** |
| **Oregon** | University of Oregon, Oregon State, Portland State, University of Portland Pilots — same name/code set as NCAAB |

#### NWSL — **shadow-only / high Oregon**
| | |
|---|---|
| **Venues** | **DK:** USA–NWSL totals. **Kalshi:** `KXNWSLTOTAL`, `KXNWSLSPREAD`. **Crypto.com:** empty at probe. |
| **Free results** | ESPN NWSL; ASA has NWSL endpoints historically (verify before importer) |
| **O→C** | No FD-style archive found → shadow |
| **Oregon** | **Portland Thorns**, Thorns FC, `por` (sport-scoped) |

#### WNBA — **shadow-only / seasonal + Oregon**
| | |
|---|---|
| **Venues** | **DK:** totals/spreads in-season. **Kalshi:** `KXWNBATOTAL`, `KXWNBASPREAD`. **Crypto.com:** empty off-season. |
| **Free results** | ESPN WNBA |
| **O→C** | No free multi-season dump in r2/r3 → shadow until tracker accumulates |
| **Oregon** | **Portland Fire**, `por` |

### 2.3 Other Kalshi/Crypto markets seen (not ranking for C6 yet)

| Series / kind | Role |
|---|---|
| `KXFACUPTOTAL`, `KXUELTOTAL`, `KXUECLTOTAL` | Cup soccer totals — shadow if wanted later |
| Crypto.com `CFB` / `CBB` | College live kinds (NCAAF/NCAAB path for A2, not new C6) |
| Player props (CDC help center) | Out of totals scope |

---

## 3. Venue coverage matrix (totals)

| League | DraftKings | Kalshi series | Crypto.com kind (probe) | Robinhood |
|---|---|---|---|---|
| NBA | Y | `KXNBATOTAL` | `NBA` | ≈Kalshi |
| NFL | Y | `KXNFLTOTAL` | `NFL` | ≈Kalshi |
| MLB | Y | `KXMLBTOTAL` | `MLB` (off-season 0) | ≈Kalshi |
| NHL | Y | `KXNHLTOTAL` | `NHL` | ≈Kalshi |
| WNBA | Y (seasonal) | `KXWNBATOTAL` | `WNBA` 0 | ≈Kalshi |
| NCAAF | Y | `KXNCAAFTOTAL` | `CFB` | ≈Kalshi |
| NCAAB | Y | `KXNCAAMBTOTAL` | `CBB` | ≈Kalshi |
| NCAAW | Y | `KXNCAAWBTOTAL` | via `CBB`? | ≈Kalshi |
| MLS | Y | `KXMLSTOTAL` | `MLS` | ≈Kalshi |
| NWSL | Y | `KXNWSLTOTAL` | `NWSL` 0 | ≈Kalshi |
| EPL | Y | `KXEPLTOTAL` | `EPL` | ≈Kalshi |
| La Liga | Y | `KXLALIGATOTAL` | none found | ≈Kalshi |
| Bundesliga | Y | `KXBUNDESLIGATOTAL` | none found | ≈Kalshi |
| Ligue 1 | Y | `KXLIGUE1TOTAL` | none found | ≈Kalshi |
| Serie A | Y | `KXSERIEATOTAL` | none found | ≈Kalshi |
| Liga MX | Y | `KXLIGAMXTOTAL` | none found | ≈Kalshi |
| EFL Champ | Y | `KXEFLCHAMPIONSHIPTOTAL` | none found | ≈Kalshi |
| UCL | Y | `KXUCLTOTAL` | `UCL` | ≈Kalshi |

DK rows = sportsbook posts those leagues (Oregon DK is the state operator; availability varies by product, but majors + soccer listed above are standard). Live juice for paper still comes from ESPN pickcenter / assumed −110 unless tracker stores DK.

---

## 4. ToS / scrape constraints (brief)

| Source | Use for | Constraint | Verdict |
|---|---|---|---|
| **Kalshi Trade API** public market GETs | Live series / strikes | Public unauth market data; **no trading** from agents | **OK** (quotes only) |
| **Crypto.com** `data-api.crypto.com` predictions | Live totals | Public market data for personal use (repo notes); trading needs app/KYC | **OK** (quotes only) |
| **Robinhood** | Prices | No public sports order API; RH ≈ Kalshi asks in paper | **Manual only** for real tickets |
| **DraftKings / ESPN pickcenter** | Live lines + scores | Disney ToU forbids bulk automated extraction; repo already uses ESPN for slate/results — **do not bulk-scrape DK HTML**; prefer ESPN scoreboard already in pipeline + line_tracker | **Gray / existing use only** |
| **football-data.co.uk** | Soccer results + O→C | Free CSV download for analysis; attribute; don't hammer | **OK** |
| **ASA** (MLS/NWSL APIs) | xG / games | Public JSON used for MLS; stay rate-limit friendly | **OK** with restraint |
| **SBRO via Wayback** | NCAA/NBA/NFL O→C | Live SBRO HTML-walled; IA copies used historically | **Gray-OK** (archives only) |
| **CFBD / CBBD** | College O→C refresh | Free key; commercial OK; **no site scraping** | **OK with signup** |
| **The Odds API** | Multi-book history | Paid historical; keyed API | **OK paid** |
| **Covers / OddsPortal / GetSTAM** | — | ToS bans automated bulk | **Prohibited** |
| **nflverse** | NFL scores/close | CC BY 4.0 | **OK** |

---

## 5. Oregon checklist by league (cardinal rule)

| League | Exclude (names / codes) |
|---|---|
| NBA / WNBA | Portland Trail Blazers, Blazers, Portland Fire, `por` |
| MLS / NWSL | Portland Timbers, Timbers, Portland Thorns (+ FC), `por` |
| NCAAB / NCAAF / NCAAW | Oregon Ducks, Oregon State Beavers, Portland State Vikings, Portland Pilots / Portland U; codes `ore` `orst` `port` `up`/`portlandu` — **never `psu`** (Penn State) |
| NHL / EPL / Bundesliga / UCL / La Liga / Serie A / Ligue 1 / Liga MX / EFL / NFL / MLB | No Oregon major franchise; keep minor-league name list so aliases cannot collide (`winterhawks`, hops, emeralds, pickles) |

Every new C6 pack must call `OregonExclusion` and extend tests for that league's name variants.

---

## 6. C6 gate summary

| Id | Ready? |
|---|---|
| `C6-epl` | **ready for C6-epl** — Tier A on football-data; live Kalshi+Crypto+DK |
| `C6-bundesliga` | **ready for C6-bundesliga** — Tier A on FD; live Kalshi+DK (Crypto kind TBD) |
| `C6-nhl` | **ready for C6-nhl** — live venues strong; Tier A only after O→C fill → until then shadow |
| `C6-ucl` | **shadow-only / no data** for multi-season CLV |
| `C6-ncaaw` | **shadow-only / no data** (no women SBRO) |
| `C6-nwsl` | **shadow-only** + high Oregon skip rate |
| `C6-wnba` | **shadow-only** until season + O→C age |

**Feeds C6:** implement packs in the priority order in §1 after C0 template lands.
