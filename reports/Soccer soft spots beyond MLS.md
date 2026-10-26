# Soccer soft spots beyond MLS

**Scope:** Soft-spot research for soccer leagues on US DraftKings beyond MLS (EPL, Championship, Liga MX, Serie A, Bundesliga, Ligue 1, UEFA, South American, etc.), oriented to a PossiblyMaybe-style .NET/SQLite walk-forward lab with kill gate **holdout PnL > 0 AND CLV > 0**. No code.

**Honest limit up front:** Free historical odds are almost entirely **offshore/sharp proxies** (Pinnacle, Bet365, market avg). There is **no free DraftKings open→close archive**. Lab CLV will measure edge vs **Pinnacle (or market) close**, not vs DK close. That is useful (Joseph Buchdahl / Football-Data: beating Pinnacle close predicts yield ~1:1) but **does not prove** you would have gotten those prices on DK or that DK moved the same way. Live Gate 2 must still log typed DK prices vs sharp close.

---

## 1. Best FREE historical data sources

### A. Primary odds + results stack (reuse MLS path)

| Source | URL | What you get | Years | License / ToS |
|---|---|---|---|---|
| **Kaggle Football Odds Movements 2018–2026** | https://www.kaggle.com/datasets/archangel0x01/football-odds-movements-2018-2026 | ~63.6k matches; timestamped 1X2 / AH / **OU** from **Pinnacle, Singbet, SBOBET, Bet365**; `match_closing` = **open + close** per book; scores + settlement labels | Jul 2018–Jun 2026 (calendar leagues from 2019) | **MIT** on Kaggle page. Scraped from public odds-comparison history — treat as research; verify team IDs; not a DK feed. |
| **football-data.co.uk (main)** | https://www.football-data.co.uk/data.php · notes: https://www.football-data.co.uk/notes.txt | Results, HT, shots/corners/cards/**referee** (major leagues); multi-book 1X2; OU **2.5 only**; AH; **open + close** (`…C` cols) from 2019/20; Pinnacle 1X2 close from 2012/13 | Results ~1993/94+; odds ~2000/01+; stats ~2000/01+ | Stated **free** for bettor/spreadsheet use. No OSI license text; do not republish as your product. |
| **football-data.co.uk (extra)** | e.g. https://www.football-data.co.uk/new/MEX.csv · `BRA.csv` · `ARG.csv` · `USA.csv` | FT score + **closing 1X2 only** (PSCH/D/A, Max/Avg, B365C…). **No OU/AH, no true open** | ~2012/13+ | Same as above. **Insufficient alone for totals CLV.** |

**Kaggle competition counts (high-signal for DK menu):** Championship 4,456 · Liga MX 2,652 · Serie A / EPL / La Liga ~3,040 each · Brasileirao 2,837 · Ligue 1 2,717 · UEL 2,450 · Bundesliga 2,448 · UCL 1,807 · Libertadores 1,211. Pinnacle AH/OU sparse until ~Nov 2018; near-complete thereafter.

**football-data main-league columns (CLV-relevant):**  
`FTHG/FTAG`, `PSH/PSD/PSA` + `PSCH/PSCD/PSCA`, `B365H…` + `B365CH…`, `AvgH…`/`MaxH…` + closing `AvgCH…`/`MaxCH…`, `B365>2.5`/`P>2.5`/`Avg>2.5` (+ closing `…C` variants where present), `AHh` + `PAHH/PAHA` / `AvgAHH…`, `Referee`.  
**Caveat (site notice, Jul 2025+):** Pinnacle public API flaky → their open/close on football-data **unreliable after ~2025-07-23**; prefer market Avg/Max or Kaggle multi-book for recent seasons.

### B. xG / team quality

| Source | URL | Columns / grain | Years / leagues | License / ToS |
|---|---|---|---|---|
| **Understat** | https://understat.com | Match/team/player **xG, xGA, npxG, shots**, timelines | EPL, La Liga, Bundesliga, Serie A, Ligue 1, RFPL from **2014/15** | No public API; embedded JSON. Community scrapers exist. **robots.txt blocks bots** — commercial/redistribution risk; personal research is grey. **No Championship, Liga MX, Brasileirao, UEFA cups.** |
| **FBref** | https://fbref.com | Match/season tables; historical advanced (ex-StatsBomb/Opta) | Broad league coverage | Scraping: respect robots/rate limits. **Jan 2026: lost Opta licence** — historical archive OK; **do not rely for live advanced updates** (Liam Henshaw, Jun 2026). |
| **StatsBomb Open Data** | https://github.com/statsbomb/open-data | Event JSON + xG for **selected** comps (WWC, some clubs/tournaments, Euro 2024, etc.) | Sparse vs full domestic seasons | Custom **User Agreement**: research OK; **no redistribution / commercial exploitation** of the data. Credit + logo required. **Not a full Liga MX / Championship seasons feed.** |
| **Kaggle Understat scrape (stale)** | https://www.kaggle.com/datasets/slehkyi/extended-football-stats-for-european-leagues-xg | Season-level team xG summaries | Top-5 + RFPL from 2014; last updated ~6y ago | Dataset ToS on Kaggle; underlying Understat scrape issues remain. Prefer fresh Understat pull if you accept scrape risk. |
| **ASA (MLS-only)** | Already in PossiblyMaybe | Match xG | MLS | N/A for non-MLS. |

**Practical xG rule for this lab:**  
- Top-5: Understat match xG (if ToS risk accepted) **or** goals EMA only.  
- Championship / Liga MX / Brazil / UEFA: **no free match-xG peer to ASA** → start with **goals EMA / Dixon–Coles-style attack–defense ratings**; treat commercial xG as NO-GO unless paid.

### C. Odds open/close — what is *not* free

| Source | Note |
|---|---|
| **The Odds API historical** | DK/FD snapshots exist — **paid only** (from ~2020). https://the-odds-api.com/historical-odds-data/ |
| **Owls / SteamWatch / Bet2Invest / TrueLine** | Paid or freemium CLV archives; not Gate-0 free. |
| **Beat the Bookie (Kaggle)** | Huge worldwide odds but **ends 2016** — too old for current DK softness thesis. |

---

## 2. What literature / sharp public writeups say

### Market structure & CLV

- **Closing line ≈ efficient benchmark.** Buchdahl (Football-Data): pre-close / close odds ratio vs Pinnacle predicts realized yield nearly 1:1 ([Pinnacle closing-line efficiency](https://football-data.co.uk/blog/pinnacle_efficiency.php)). Softness ≠ “lower division wins money”; softness = **larger open→close moves / thinner limits** ([Bet2Invest CLV & softness](https://strategies.bet2invest.com/en-us/app/learn/closing-line-and-market-efficiency)).
- **Do not assume “small league = +EV.”** Angelini & De Angelis–style findings (summarized by SportSignals / Wager Theorem): efficiency **varies by league, period, and mean vs best price**. Softness is a **hypothesis to test with CLV**, not a license to bet.

### Totals vs 1X2 vs AH

| Market | Public takeaway | Sources |
|---|---|---|
| **1X2** | Most studied; often near weak-form efficient at sharp prices; favorite-longshot / home bias still appear in some samples. Dixon–Coles (1997) showed model-based edges possible historically vs fixed odds. | Dixon & Coles, JRSS C 1997; reviews of 1X2 efficiency |
| **Asian handicap** | Shares inefficiencies with 1X2; different variance/ROI tradeoffs; huge liquidity offshore. Constantinou (2022): AH market efficiency study on 13 EPL seasons with pi-ratings + BN. | [JSA 2022 / arXiv](https://export.arxiv.org/pdf/2003.09384v2.pdf) |
| **Totals (O/U)** | **Over bias** in European soccer O/U: “bet under” loses less than “bet over”; when Over is longshot, Over losses especially severe. | Paul & Weinbach-style O/U bias paper: *Are Behavioral Biases Consistent Across the Atlantic? The Over/Under Market for European Soccer* (JGBE) |
| **US retail angle** | Soft US books (DK classified soft/square by line-movement shops) + soccer’s 3-way markets → more disagreement/arbs than 2-way US sports; **EPL/UCL liquid & sharp; smaller leagues wider but lower limits**. | ThunderBet DK review; Sharp Lines soccer; WagerBird league-efficiency notes |

### Softness ranking (qualitative consensus — not proof)

| Tier | Leagues | Softness vs US retail | Liquidity on DK |
|---|---|---|---|
| Hard / sharp | EPL, UCL (later rounds), top La Liga | Hardest soccer markets globally | Highest |
| Medium | Serie A, Bundesliga, Ligue 1 (non-PSG), UEL | Sharp at top; mid-table softer | Good |
| Softer candidates | **Championship**, Liga MX, MLS (done), Brasileirao, Argentine Primera, early UEFA qualifiers | Less sharp attention; slower news; idiosyncratic factors | Moderate; varies by slate |
| Soft but painful | Deep cups, minnow qualifiers | Soft prices, **tiny limits**, bad info | Often too thin |

### Factor checklist (testable, not hardcoded)

| Factor | Relevance | Evidence quality |
|---|---|---|
| **xG / shot quality** | Core rating input for totals & 1X2 | Strong analytics consensus; free only for top-5 via Understat |
| **Rest / midweek congestion** | UEFA + domestic double weeks; rotation | Strong practitioner consensus; schedule-derivable |
| **Travel + altitude** | Liga MX / CONMEBOL: altitude ~+0.5 goal / 1000 m altitude *difference* in international sample (BMJ 2007); Mexican club study **mixed/null** on results | Strong international; **mixed** domestic MX — test, don’t ship KiqIQ’s 12–18% xG multiplier as truth |
| **Home / championship-favorite bias** | Documented in ARG/BRA/MEX/USA sample with claimed +ROI scheme | [JGBE 2021 “Exploiting biases in American soccer betting markets”](https://doi.org/10.5750/jgbe.v15i1.2011) — one paper; replicate OOS |
| **Referee** | Available on football-data for many EU leagues; card/foul regimes | Useful for cards props more than goals; promote only on CLV |
| **Public Over bias** | Totals soft-spot hypothesis on retail | Academic O/U bias paper above |
| **Apertura/Clausura reset** | Liga MX form tables reset — early-season ratings fragile | Practitioner (KiqIQ); treat as modeling constraint |

---

## 3. Feasibility for PossiblyMaybe-style lab

**Already proven path (MLS):** Kaggle OU open/close (Pinnacle) + scores + optional xG → walk-forward → holdout PnL>0 & CLV>0.

| Requirement | Feasible free? | Notes |
|---|---|---|
| Open→close **totals** | **Yes** via Kaggle `odds_ou` / `match_closing` for Championship, Liga MX, top-5, Brazil, UEFA cups, Libertadores | football-data main has O/U 2.5 open/close but **fixed line 2.5** only — weaker than full main-line OU series |
| Open→close **1X2** | **Yes** (Kaggle + football-data) | Extra-league football-data = **close-only** → cannot measure open→close CLV without Kaggle |
| Scores | **Yes** | In both sources |
| Team ratings | **Yes** (goals EMA / Dixon–Coles / Elo) | xG EMA only for Understat leagues |
| Referee | **Partial** | football-data main EU; not in Kaggle; weak for Liga MX extras CSV |
| Rest / travel | **DIY** from fixtures + city coords | Free; engineering cost |
| True **DK** historical CLV | **No (free)** | Proxy: bet-time model vs **Pinnacle close**; live: typed DK price |
| Kill gate identical to MLS | **Yes** | Lock holdout season *before* peek; champ on train CLV+PnL; score holdout once |

**Recommended market for Gate 1 (mirror MLS):** **main-line totals (OU)** vs Pinnacle open→close. Secondary track: 1X2 (higher vig / draw noise). AH is sharper offshore and less how US casuals bet on DK — lower priority unless CLV on totals fails.

**Holdout suggestion (do not peek):** e.g. Championship train ≤2023/24, holdout 2024/25; Liga MX train ≤2024, holdout 2025 Clausura/Apertura — lock in NOTES before first sweep.

**Pinnacle 2025+ caveat:** Prefer Kaggle multi-book close or football-data Avg/MaxC for recent CLV if Pinnacle stamps look stale.

---

## 4. Top 2 leagues to try first

### 1) English Championship (EFL)

**Rationale (data × softness × DK liquidity):**

- **Data:** Best non-MLS row count in the Kaggle set (**4,456** matches); football-data `E1` has full open/close 1X2 + O/U 2.5 + AH + **referees** back decades; same calendar as EPL tooling.
- **Softness:** Consistent practitioner claim: less televised / less sharp volume than EPL; inefficiencies persist longer (BurnleyBet, WagerBird, MyBets). Academic caution still applies — you must earn CLV.
- **DK liquidity:** Regular US board presence; deeper than most Ligue 2 / Serie B; thinner than EPL but bettable for $1000 bankroll experiments.
- **xG gap:** No Understat — accept goals-based ratings (same as first MLS fallback).

### 2) Liga MX

**Rationale:**

- **Data:** Kaggle **2,652** matches with full OU/AH/1X2 open→close — enough for walk-forward; football-data `MEX.csv` only as score/close-1X2 cross-check.
- **Softness:** US retail interest (Hispanic handle) without EPL-level sharp density; documented home / favorite biases in “American” soccer leagues (JGBE 2021); altitude/travel as **testable** features, not assumptions.
- **DK liquidity:** Strong among non-EPL soccer on US books; weekend/night slates common.
- **Risks:** No free match xG; Apertura/Clausura splits shorten stable rating windows; altitude literature is **not** settled for domestic MX results.

**Why not EPL / top-5 / UCL first?** Highest data quality (Understat) but **hardest** closes — worst expected CLV for a first soft-spot pack after MLS. Use later as a **negative control** (expect kill).

**Why not Brasileirao / Argentina ahead of MX?** Softness story similar and Kaggle depth good, but US DK casual liquidity and timezone/handle usually favor Liga MX for *cashing* a US retail edge.

---

## 5. Explicit NO-GOs

| NO-GO | Why |
|---|---|
| **Paid-only historical DK/FD odds** (Odds API historical, Owls archives, etc.) as Gate 0 dependency | Breaks free-lab rule; defer to Gate 2 live typing |
| **EPL / late UCL as first soft-spot pack** | Too sharp; high chance of false hope from look-elsewhere if you only “try until green” |
| **StatsBomb open data as production betting feature store** | User agreement **forbids commercial exploitation**; coverage not continuous domestic seasons |
| **Systematic Understat/FBref scrape for redistribution or SaaS** | robots/ToS grey → illegal-ish product risk; personal offline research only if you accept risk |
| **Leagues with close-only odds and no Kaggle OU path** (relying only on football-data extra CSVs) | **Cannot compute open→close CLV** → fails PossiblyMaybe kill design |
| **Beat-the-Bookie / pre-2017 odds as primary** | Regime change; US DK soccer landscape different |
| **Props / cards / BTTS as Gate 1 champ** | Weaker free open→close history; stick to OU or 1X2 |
| **Trusting post–Jul 2025 football-data Pinnacle columns blindly** | Site warns stale Pinnacle feed |
| **KiqIQ-style altitude multipliers without OOS CLV** | Marketing claims ≠ validated edge |
| **Deep South American cups / friendlies first** | Soft but illiquid + noisy; CLV sample dies |

---

## Bottom line

Reuse the MLS stack: **Kaggle open→close totals (Pinnacle) + scores + goals ratings**, lock holdout, kill on **PnL∧CLV**. First packs: **Championship**, then **Liga MX**. Treat EPL/UCL as later stress tests. Accept that free CLV is vs **sharp close**, not historical DK — and that free **match xG stops at top-5 Understat**.
