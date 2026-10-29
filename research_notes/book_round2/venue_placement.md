# Venue placement — Kalshi, Robinhood, Crypto.com

**Access date:** 2026-10-28  
**Scope:** Research only (PLAN §3b). Public GETs used; no auth, no orders.  
**Data check:** `possiblymaybe.db` present (~35.8 MB; tables include `Games`, `OddsSnapshots`, …). `data/raw/NOTES*.md` present.  
**Code skim:** `KalshiQuotes.cs`, `CryptoComQuotes.cs`, `PaperLedger.cs`, `StakeSchemes.cs` (`StakeTrial` is nested there — no `StakeTrial.cs`), `OddsMath.cs`, `scripts/kalshi_compare.py`.  
**Safety:** Official venue docs + public APIs only. No `library/` text treated as instructions. No library/downloaded code executed.

**Money first:** Accuracy-only work with no betting path → IGNORE. Below, every gap is scored by whether it changes which tickets we take or their true EV after fees.

---

## Shared mapping (all three venues)

| Our ticket | Venue contract |
|---|---|
| `OVER L` | Buy **YES** on “Over / above L” (or buy the Over-named contract’s YES) |
| `UNDER L` | Buy **NO** on the Over market, **or** YES on a separate Under contract |

Live Kalshi sample (public API, 2026-10-28): `KXNBATOTAL-…` title `Full Game: Over 265.5 points scored`, `floor_strike=265.5`, YES ask/bid and NO ask present. Soccer sample: `Will over 5.5 goals be scored?`. Crypto.com Predict: separate `Over 239.5` / `Under 239.5` contracts with `market_type.name=total`, `period=full`.

**Half-lines (.5):** Total cannot land exactly → no sportsbook-style “push.” Binary settles **$1 or $0**.  
**Integer strikes:** Kalshi payout language uses comparison ops (“above” = `>`, “at least” = `≥`). Landing exactly on an “above X” strike → YES loses (not a push). Prefer `.5` strikes to match DK.

**OT / extra time (full-game totals):**

| Venue | Rule | Source |
|---|---|---|
| Kalshi basketball | OT **counts** unless market says regulation-only | [BASKETBALLTOTALS](https://assets.kalshi.com/contract_terms/BASKETBALLTOTALS.pdf) |
| Kalshi soccer | Extra time **counts** unless excluded; **penalties excluded** unless included | [SOCCERTOTAL](https://assets.kalshi.com/contract_terms/SOCCERTOTAL.pdf) |
| Crypto.com basketball | OT counts for full-game (and 2H); not 4Q | [Sports Event Trading](https://help.crypto.com/en/articles/10208780-sports-event-trading) |
| Robinhood | Settlement follows the **exchange** listing the contract (often KalshiEX); read that market’s terms | [Event contracts overview](https://robinhood.com/us/en/support/articles/event-contracts-overview/) |

**Postpone / abandon (typical):** delay ≤48h → markets stay open; >48h or abandon without resume → last fair price / Event Cancelled (venue-specific). Kalshi basketball also: cancel before tip → last fair price. Crypto.com: reschedule to **new calendar day** before tip → Event Cancelled; same-day delay → settle normally.

---

## 1. Kalshi

### Totals ↔ OVER/UNDER

- Series we use in code: `KXNBATOTAL`, `KXMLSTOTAL` (`KalshiQuotes.cs:13–17`, `kalshi_compare.py:22`).
- Also listed on exchange (public `GET /series/{ticker}`, 2026-10-28): `KXLIGUE1TOTAL`, `KXSERIEATOTAL`, `KXLIGAMXTOTAL` — **not wired in our code**.
- Matching: event ticker contains Kalshi date `yyMMMdd` + team codes; pick market with `floor_strike` nearest to our `line`; OVER → `yes_ask_dollars`; UNDER → `no_ask_dollars` else `1 - yes_bid_dollars`.

### Fees → true break-even

Official formula ([fee schedule PDF](https://kalshi.com/docs/kalshi-fee-schedule.pdf), effective 2026-07-07; [fee-schedule page](https://kalshi.com/fee-schedule)):

```text
taker_fee = round_up(M × 0.07 × C × P × (1 − P))
```

- `P` = fill price in dollars; `C` = contracts; `M` = 1 for standard markets.  
- Charged on **immediate** matches only. Maker fee `0.0175 × C × P × (1−P)` only for series listed as maker-fee markets. **`KXNBATOTAL` / `KXMLSTOTAL` are not on the non-standard maker list** → treat as **taker-only** when crossing the book; resting maker = $0 unless schedule changes.  
- No settlement fee. No membership fee.

**Large-C approximation** (ignore centicent round-up): fee/contract ≈ `0.07·P·(1−P)`.  
**All-in cost** to buy and hold to settle: `P + fee`. Win pays `$1`.  
**Break-even win rate** = all-in cost (table below).

| Ask P | Fee/ctr ≈ | All-in | BE win% | vs fee-blind BE=P |
|---:|---:|---:|---:|---|
| 0.20 | 0.0112 | 0.2112 | **21.12%** | +1.12 pp |
| 0.30 | 0.0147 | 0.3147 | **31.47%** | +1.47 |
| 0.40 | 0.0168 | 0.4168 | **41.68%** | +1.68 |
| 0.50 | 0.0175 | 0.5175 | **51.75%** | +1.75 |
| 0.60 | 0.0168 | 0.6168 | **61.68%** | +1.68 |
| 0.70 | 0.0147 | 0.7147 | **71.47%** | +1.47 |
| 0.80 | 0.0112 | 0.8112 | **81.12%** | +1.12 |

Official 100-contract fee table peaks at **$1.75 / 100** at 50¢ (= 1.75¢/ctr).

### Order mechanics

- Central limit order book. Public: `GET …/markets/{ticker}/orderbook` returns YES/NO **bids** only; YES ask ≈ `1 −` best NO bid (and vice versa) ([orderbook docs](https://docs.kalshi.com/api-reference/market/get-market-orderbook)).  
- Orders: authenticated `POST` limit; `side=bid` buys YES; price dollars e.g. `"0.4500"`; `count` = contracts; TIF GTC / IOC / etc. ([create order](https://docs.kalshi.com/getting_started/quick_start_create_order)).  
- Tick $0.01 (contract terms). Position accountability $25k/strike/member (terms PDF).  
- **Count from $ stake S at ask P (taker):**  
  `C = floor(S / (P + 0.07·P·(1−P)))` (then recompute exact fee with round-up).  
  Max cost ≈ `C·P + fee(C,P)`.

### API vs manual / KYC

- Public market data: no key (`external-api.kalshi.com/trade-api/v2`). Orders need API keys + RSA signing.  
- Manual UI also fine. Age 18+; KYC/ID when requested ([signup](https://help.kalshi.com/en/articles/13823778-signing-up-as-an-individual)). Kalshi markets itself as US-wide for sports ([news](https://news.kalshi.com/p/game-on-kalshi-sports-trading-is-now-100-legal-in-all-50-states-2)); confirm your state in-app — third-party “restricted state” lists disagree with Kalshi marketing.

### How our code prices it / wrongness

- `KalshiQuotes.TryGetTotalAsync` → ask → `OddsMath.AmericanFromBuyPrice` → `PaperLedger.PlaceFromBinaryQuoteAsync` gates `EV ≥ 4%` with **no fee** (`PaperLedger.cs:102–108`).  
- `kalshi_compare.py` same fee-blind EV.  
- **Wrong for money:** EV and Kelly use payout `1/P` but true payout after fee is `1 / (P+fee)` → **overstated EV**, worst near 50¢ (~1.75 pp).  
- Nearest-strike (any distance) can attach a GO to the wrong total.  
- Soccer leagues we model (Ligue 1 / Serie A / Liga MX) exist on Kalshi but return `null` → always SKIP.

---

## 2. Robinhood (event contracts)

### Totals ↔ OVER/UNDER

- Sports event contracts via Robinhood Derivatives + partner DCMs (KalshiEX, ForecastEx, Rothera, Nadex) ([overview](https://robinhood.com/us/en/support/articles/event-contracts-overview/)).  
- Same binary YES/NO $1/$0; sports totals are Over/Under thresholds.  
- **Our paper:** RH uses **identical Kalshi public asks** (`PaperLedger.cs:62–70`, `PaperVenues` comment). Separate $1000 bankroll only.

### Fees → true break-even

As of **2026-06-01** ([overview](https://robinhood.com/us/en/support/articles/event-contracts-overview/)):

```text
RH_commission = round_up_to_cent( k × P × (1 − P) × C )
  k = 0.10 (no Gold) or 0.05 (Gold)
  then capped at $0.01 per contract
+ exchange fee (varies; “up to $0.01 per contract”; KalshiEX route often cited as ~$0.01)
```

Fees on **open and** early close; hold-to-settlement → no second RH leg beyond open. Instant deposits **not** usable for event contracts.

**Assumed KalshiEX exch = $0.01/ctr** (confirm on order ticket — not always published as a single number):

| Ask P | No-Gold all-in≈ | BE% | Gold all-in≈ | BE% |
|---:|---:|---:|---:|---:|
| 0.20 | 0.20+0.01+0.01=0.22 | **22.0%** | 0.22 | 22.0% |
| 0.40 | 0.42 | **42.0%** | 0.42 | 42.0% |
| 0.50 | 0.52 | **52.0%** | 0.52 | 52.0% |
| 0.60 | 0.62 | **62.0%** | 0.62 | 62.0% |
| 0.80 | 0.82 | **82.0%** | 0.82 | 82.0% |
| 0.90 | ~0.919–0.920 | **~92%** | ~0.9145–0.920 | Gold helps at extremes |

Mid-book RH all-in (~**+2.0¢**) is **worse** than Kalshi direct taker (~**+1.75¢**). Paper treating RH = Kalshi ask **overstates RH EV**.

### Order mechanics

- **Mobile only** to trade (web view-only) ([trading article](https://robinhood.com/us/en/support/articles/trading-event-contracts/)).  
- Limit orders: **IOC** (contracts or dollars) or **GTD** (contracts; expires next calendar day 3 AM ET). Dollar orders always IOC; qty from `$ / price` after fees. Opening = whole contracts.  
- Count from stake: `C = floor(S / (P + rh_fee_per + exch_per))`.

### API vs manual / KYC

- **Manual app only** for trading (no public retail order API for us).  
- Robinhood Derivatives approval; US resident; individual brokerage. **Sports event contracts unavailable:** Maryland, Nevada; Michigan no **new** sports opens as of 2026-09-09 ([overview](https://robinhood.com/us/en/support/articles/event-contracts-overview/)). Check app for current list.

### How our code prices it / wrongness

- Same quote object as Kalshi → same American odds → same EV gate.  
- **Wrong:** ignores RH commission + exchange fee; ignores state/eligibility; ignores that RH mid liquidity/routing can differ from the public Kalshi book we scrape.

---

## 3. Crypto.com Predict (CDNA)

### Totals ↔ OVER/UNDER

- Separate Over/Under contracts (`title` like `Over 239.5` / `Under 239.5`); YES buy price on the matching side (`CryptoComQuotes.cs:67–93`).  
- Public data: `https://data-api.crypto.com/api/v1/predictions/…` (no key for personal use) ([data.crypto.com](https://data.crypto.com/)).  
- Code sports: NBA + MLS only (`CryptoComQuotes.cs:51–57`). Help center lists Winner / Spread / Totals / Player; availability varies by event.

### Fees → true break-even

Sports singles ([Sports Event Trading — fees](https://help.crypto.com/en/articles/10208780-sports-event-trading)):

```text
taker = 0.07 × C × P × (1 − P)
maker = 0.0175 × C × P × (1 − P)
ITM settlement fee: Waived
OTM settlement: (table row present; open fee already paid)
```

Same **functional** open taker curve as Kalshi → **same BE table as Kalshi §1** for buy-and-hold.  
Predict Pro generic page also states fee at settlement/close ([hours/fees](https://help.crypto.com/en/articles/17213456-trading-hours-fees-and-transaction-history)); for binaries settled at $0/$1, `P(1−P)=0` at settlement prices, and sports FAQ explicitly **waives ITM settle fees** — use sports table for sports.

### Order mechanics

- Market (MO with protection, IOC) and Limit; default slippage tolerance **$0.05**/ctr adjustable ([sports FAQ](https://help.crypto.com/en/articles/10208780-sports-event-trading)).  
- Estimated total = `(price + fees) × qty`; hold includes slippage buffer.  
- Count: same as Kalshi `C = floor(S / (P + 0.07·P·(1−P)))`.

### API vs manual / KYC

- Quotes: public Market Data API (what we use). Trading: Crypto.com App/Web → USD account → enable Derivatives North America; **US only** for sports event trading.  
- CFTC-regulated CDNA. No order placement via our paper path.

### How our code prices it / wrongness

- Parses `yes` (and under fallback) → American → fee-blind 4% EV.  
- Event match is **title substring / NameHints** — fragile (wrong game risk).  
- `limit=500` on contracts; thin events OK, dense boards may need pagination if API adds it.  
- Some match events expose moneylines only (no `total` contracts) → SKIP (observed 2026-10-28 on one NBA card).

---

## Break-even comparison (buy ask, hold to settle)

Approximate large-C; RH assumes +$0.01 exchange.

| Ask P | Kalshi / Crypto BE% | RH (no Gold) BE% | Fee-blind code BE% |
|---:|---:|---:|---:|
| 0.30 | 31.47 | 32.0 | 30.0 |
| 0.40 | 41.68 | 42.0 | 40.0 |
| 0.50 | 51.75 | 52.0 | 50.0 |
| 0.60 | 61.68 | 62.0 | 60.0 |
| 0.70 | 71.47 | 72.0 | 70.0 |

**Rule of thumb:** our paper `EV = p_model/P − 1` needs roughly **+1.5–2.0 pp** more model edge at mid prices before a venue is truly +EV after fees. Prefer **Kalshi or Crypto** over RH at the same displayed ask; prefer the venue with the **lower all-in ask** after fee.

---

## Exact order-ticket recipe (step-by-step)

Worked example: model wants **OVER 229.5**, stake budget **$20**, Kalshi YES ask **P = 0.45**, depth OK at that ask.

### A. Kalshi (preferred when ask ≤ other venues after fee)

1. Confirm market: series `KXNBATOTAL` (or soccer series), title Over **229.5**, ticker e.g. `KXNBATOTAL-…-230`.  
2. Read book: `GET /markets/{ticker}/orderbook` — size at prices ≤ your limit.  
3. Fee/ctr ≈ `0.07×0.45×0.55 = 0.017325` → all-in ≈ `0.467325`.  
4. `C = floor(20 / 0.467325) = 42`.  
5. Exact fee ≈ `0.07×42×0.45×0.55` then round-up per schedule; max cost ≈ `42×0.45 + fee` (≤ ~$19.67 + fee).  
6. Place **limit buy YES** at `0.4500` (or 1¢ inside if maker and series has no maker fee), `count=42`, IOC if you need fill-or-nothing else GTC.  
7. Hold to settlement (no settle fee). Do **not** authenticate/place from agents.

### B. Robinhood

1. App → Prediction markets → game → Over 229.5 → **Yes**.  
2. Mode **Contracts** → enter `C` from `floor(S / (P + min(0.01, k·P·(1−P)) + exch))`.  
3. Prefer **GTD limit** at your max price if not chasing; IOC/dollar if you need immediate.  
4. Review commission + exchange lines on confirm screen; if all-in > Kalshi, **skip RH**.

### C. Crypto.com

1. Predict → Sports → event → Over 229.5 → Yes.  
2. Contracts = same formula as Kalshi. Set slippage ≤ your edge (default $0.05 can wipe a thin edge).  
3. Confirm Estimated Total includes fee; hold to settle (ITM settle fee waived).

### D. Paper → real check

1. Run slate / `paper-go` note for venue ask.  
2. Recompute `EV_true = p_model / (P + fee_per) - 1`; require `≥ 4%` (or tighten gate).  
3. Require `|strike − model_line| ≤ 0.5` (else no trade).  
4. Pick venue with best `EV_true` among DK (American juice) / Kalshi / Crypto / RH.

---

## Code gaps (file:line) — ≤3 minimal money fixes

| # | Gap | Where | Minimal fix | Money path |
|---|---|---|---|---|
| **1** | EV/Kelly ignore venue fees → false GOs near even money | `PaperLedger.cs:102–108`; `OddsMath.cs:34–59`; sizing `StakeSchemes.cs:88–90` | Add `EffectiveBuyPrice(P, venue)` = `P + fee(P)` (Kalshi/Crypto: `0.07·P·(1−P)`; RH: `P + min(0.01,k·P·(1−P)) + 0.01`) and pass that into `AmericanFromBuyPrice` / EV | Yes — stops fee-negative tickets |
| **2** | Nearest strike with no max Δ | `KalshiQuotes.cs:57–63`; `CryptoComQuotes.cs:96–100` | Reject unless `abs(strike−line) ≤ 0.5` (NBA) / `≤ 0.25` (soccer) | Yes — avoids wrong-line “GOs” |
| **3** | Soccer Kalshi series unwired; RH fee = Kalshi | `KalshiQuotes.cs:13–17`; RH path `PaperLedger.cs:69–70` | Map `Ligue1/SerieA/LigaMX` → `KXLIGUE1TOTAL` / `KXSERIEATOTAL` / `KXLIGAMXTOTAL`; apply RH `EffectiveBuyPrice` separately so RH can SKIP when Kalshi GO | Yes — more soccer fills + fewer RH losers |

**IGNORE (no money / out of scope here):** prettier UI; auth trading bots; accuracy metrics without a ticket path; DK −110 default (PLAN known issue — separate).

---

## Smart-betting checklist

1. **Strike** equals our line (±0.5 NBA / ±0.25 soccer); else skip.  
2. **Side:** OVER → YES(over); UNDER → NO(over) or YES(under).  
3. **All-in price** = ask + venue fee; `EV_true ≥ 4%` and `|blend edge| ≥ champ minEdge`.  
4. **Compare venues** on all-in, not raw ask; usually Kalshi ≤ Crypto ≈ formula twins; RH worse at same P.  
5. **Depth:** size ≤ visible size at your limit (Kalshi orderbook); else partial/slippage.  
6. **Timing:** pre-tip; avoid live thin books; Crypto slippage ≤ edge.  
7. **Settlement:** OT/ET rules match our model (full-game OT on); no push on `.5`.  
8. **Eligibility:** RH state list; Crypto US; Kalshi KYC.  
9. **Bankroll:** one venue’s OUT lane does not block others (`AGENTS.md`).  
10. **Never** auto-place from agents; user confirms real tickets.

---

## Verdicts (BRIEF vocabulary)

| Topic | Verdict |
|---|---|
| Kalshi public ask → American | **USED** — `KalshiQuotes.cs`, `kalshi_compare.py` |
| RH = Kalshi ask, separate book | **USED** — `PaperLedger.cs:62–70` (fee-wrong) |
| Crypto.com data-api totals | **USED** — `CryptoComQuotes.cs` |
| Fee-adjusted EV / BE | **CANDIDATE** — fix #1; high money impact, 1 formula param set |
| Exact strike gate | **CANDIDATE** — fix #2 |
| Wire EU/MX Kalshi totals series | **CANDIDATE** — fix #3 |
| Build RH/Crypto private order bots | **IGNORE** — no auth/orders per safety; manual ticket recipe enough |
| Pure calibration without fee/venue path | **IGNORE** — no betting path in this doc’s scope |

---

## Sources (URLs + access 2026-10-28)

- https://kalshi.com/docs/kalshi-fee-schedule.pdf  
- https://kalshi.com/fee-schedule  
- https://assets.kalshi.com/contract_terms/BASKETBALLTOTALS.pdf  
- https://assets.kalshi.com/contract_terms/SOCCERTOTAL.pdf  
- https://docs.kalshi.com/api-reference/market/get-market-orderbook  
- https://docs.kalshi.com/getting_started/quick_start_create_order  
- https://help.kalshi.com/en/articles/13823778-signing-up-as-an-individual  
- https://external-api.kalshi.com/trade-api/v2 (public GETs)  
- https://robinhood.com/us/en/support/articles/event-contracts-overview/  
- https://robinhood.com/us/en/support/articles/trading-event-contracts/  
- https://help.crypto.com/en/articles/10208780-sports-event-trading  
- https://help.crypto.com/en/articles/17213456-trading-hours-fees-and-transaction-history  
- https://data.crypto.com/ / `https://data-api.crypto.com/api/v1/predictions/…`  
- Repo: `PossiblyMaybe.Cli/Paper/{KalshiQuotes,CryptoComQuotes,PaperLedger}.cs`, `PossiblyMaybe.Domain/{StakeSchemes,OddsMath}.cs`, `scripts/kalshi_compare.py`, `AGENTS.md`
