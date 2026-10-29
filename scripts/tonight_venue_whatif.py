#!/usr/bin/env python3
"""What-if: tonight NBA totals across DK / Kalshi(=Robinhood) / Crypto.com.

Matches paper gates: blended edge = (1-w)*(model−line) with w=0.5, |edge|≥6, EV≥4%.
Production picks the strike nearest the DK main line (not cherry-picked far strikes).
"""
from __future__ import annotations

import json
import math
import re
import urllib.parse
import urllib.request
from typing import Any

UA = {"User-Agent": "PossiblyMaybe/venue-whatif"}
KALSHI = "https://external-api.kalshi.com/trade-api/v2"
CRYPTO = "https://data-api.crypto.com/api/v1/predictions"
W = 0.5
MIN_EDGE = 6.0
MIN_EV = 0.04

GAMES = [
    # away, home, model, dk_line, actual_total
    ("MIA", "ATL", 238.9, 243.5, 246),
    ("WAS", "CLE", 239.6, 240.5, 232),
    ("CHA", "DET", 223.7, 212.5, 186),
    ("PHI", "IND", 229.1, 234.5, 223),
    ("ORL", "TOR", 227.1, 224.5, 228),
    ("NYK", "CHI", 229.4, 233.5, 260),
    ("HOU", "MIL", 235.3, 226.5, 232),
    ("GSW", "MIN", 231.3, 227.5, 233),
    ("DEN", "NOP", 244.7, 241.5, 195),
    ("SAS", "UTA", 238.9, 232.5, 228),
    ("OKC", "DAL", 230.6, 227.5, 216),
    ("LAL", "LAC", 229.3, 224.5, None),
]

ALIASES = {
    "ATL": ["Atlanta"], "CHA": ["Charlotte"], "CHI": ["Chicago"], "CLE": ["Cleveland"],
    "DAL": ["Dallas"], "DEN": ["Denver"], "DET": ["Detroit"], "GSW": ["Golden State"],
    "HOU": ["Houston"], "IND": ["Indiana"], "LAC": ["Los Angeles C", "Clippers"],
    "LAL": ["Los Angeles L", "Lakers"], "MIA": ["Miami"], "MIL": ["Milwaukee"],
    "MIN": ["Minnesota"], "NOP": ["New Orleans"], "NYK": ["New York"],
    "OKC": ["Oklahoma City"], "ORL": ["Orlando"], "PHI": ["Philadelphia"],
    "SAS": ["San Antonio"], "TOR": ["Toronto"], "UTA": ["Utah"], "WAS": ["Washington"],
}


def get_json(url: str) -> Any:
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=45) as r:
        return json.load(r)


def blended_edge(model: float, line: float) -> float:
    exp = W * line + (1 - W) * model
    return exp - line


def over_prob(exp: float, line: float) -> float:
    return 1 / (1 + math.exp(-(exp - line) / 12.0))


def american_from_buy(p: float) -> int:
    if p >= 0.5:
        return int(round(-100 * p / (1 - p)))
    return int(round(100 * (1 - p) / p))


def ev_from_american(p: float, american: int) -> float:
    dec = 1 + 100 / (-american) if american < 0 else 1 + american / 100
    return p * dec - 1


def decide(model: float, line: float, american: int) -> tuple[bool, str, float, float, float]:
    edge = blended_edge(model, line)
    side = "OVER" if edge > 0 else "UNDER"
    exp = W * line + (1 - W) * model
    p = over_prob(exp, line) if side == "OVER" else 1 - over_prob(exp, line)
    e = ev_from_american(p, american)
    go = abs(edge) >= MIN_EDGE and e >= MIN_EV
    return go, side, edge, p, e


def kalshi_all() -> list[dict]:
    out: list[dict] = []
    for status in ("open", "settled"):
        cursor = None
        while True:
            path = f"/markets?series_ticker=KXNBATOTAL&status={status}&limit=200"
            if cursor:
                path += f"&cursor={cursor}"
            d = get_json(KALSHI + path)
            batch = d.get("markets") or []
            out.extend(batch)
            cursor = d.get("cursor")
            if not cursor:
                break
            if status == "settled" and sum(1 for m in out if m.get("status") != "open") > 2000:
                break
    return out


def nearest_kalshi(markets: list[dict], away: str, home: str, line: float) -> dict | None:
    stamp = "26OCT28"
    combo = f"{away}{home}"
    scored = []
    for m in markets:
        et = (m.get("event_ticker") or "").upper()
        if stamp not in et:
            continue
        if combo not in et and f"{home}{away}" not in et:
            continue
        strike = m.get("floor_strike")
        if strike is None:
            continue
        scored.append((abs(float(strike) - line), float(strike), m))
    if not scored:
        return None
    scored.sort()
    _, strike, m = scored[0]
    buy = None
    for key in ("yes_ask_dollars", "last_price_dollars"):
        raw = m.get(key)
        try:
            v = float(raw) if raw not in (None, "") else None
        except ValueError:
            v = None
        if v is not None and 0 < v < 1:
            buy = v
            break
    return {
        "strike": strike,
        "buy": buy,
        "status": m.get("status"),
        "result": m.get("result"),
        "ticker": m.get("ticker"),
    }


def nearest_crypto_over(away: str, home: str, line: float) -> dict | None:
    event = None
    for q in ALIASES.get(home, [home]) + ALIASES.get(away, [away]):
        d = get_json(f"{CRYPTO}/events/search?q={urllib.parse.quote(q)}&limit=15")
        for e in d.get("data") or []:
            if e.get("kind") != "NBA" or e.get("type") != "match":
                continue
            title = e.get("title") or ""
            if not any(a in title for a in ALIASES.get(away, [away])):
                continue
            if not any(h in title for h in ALIASES.get(home, [home])):
                continue
            event = e
            break
        if event:
            break
    if not event:
        return None
    d = get_json(f"{CRYPTO}/events/{event['id']}/contracts?limit=500")
    scored = []
    for c in d.get("data") or []:
        mt = c.get("market_type") or {}
        if mt.get("name") != "total" or mt.get("period") != "full":
            continue
        title = c.get("title") or ""
        m = re.match(r"Over\s+(\d+(?:\.\d+)?)", title, re.I)
        if not m or c.get("status") in ("voided", "suspended"):
            continue
        try:
            yes = float(c["yes"]) if c.get("yes") not in (None, "") else None
        except (TypeError, ValueError):
            yes = None
        if yes is None or not (0 < yes < 1):
            continue
        strike = float(m.group(1))
        scored.append((abs(strike - line), strike, yes, c.get("status")))
    if not scored:
        return None
    scored.sort()
    _, strike, yes, status = scored[0]
    return {"strike": strike, "buy": yes, "status": status}


def outcome(side: str, line: float, actual: float | None) -> str:
    if actual is None:
        return " (no final yet)"
    won = (actual > line) if side == "OVER" else (actual < line)
    return f" actual={actual} → {'W' if won else 'L'}"


def main() -> None:
    print("######## Tonight multi-venue what-if (20261028) ########")
    print(f"Champ gates: w={W} |blended edge|≥{MIN_EDGE} EV≥{MIN_EV:.0%}  (RH uses Kalshi asks)\n")

    print("--- DraftKings (main total @-110) ---")
    dk_go = 0
    for away, home, model, line, actual in GAMES:
        go, side, edge, p, e = decide(model, line, -110)
        mark = "GO" if go else "NO-GO"
        if go:
            dk_go += 1
        print(
            f"  {mark} {away}@{home} model={model} line={line} blend_edge={edge:+.1f} "
            f"{side} EV={e:+.1%}{outcome(side, line, actual)}"
        )
    print(f"DK GOs: {dk_go}\n")

    print("--- Kalshi / Robinhood (nearest strike to DK line) ---")
    markets = kalshi_all()
    kr_go = 0
    for away, home, model, line, actual in GAMES:
        hit = nearest_kalshi(markets, away, home, line)
        if not hit:
            print(f"  {away}@{home}: no Kalshi event")
            continue
        if hit["buy"] is None:
            # Settled endgame — cannot recover pre-tip EV; still show blended edge at that strike
            go, side, edge, p, e = decide(model, hit["strike"], -110)
            print(
                f"  SKIP-PRICE {away}@{home} nearest={hit['strike']} blend_edge={edge:+.1f} "
                f"status={hit['status']} (no fillable ask; hyp EV@-110={e:+.1%})"
                f"{outcome(side, hit['strike'], actual)}"
            )
            continue
        amer = american_from_buy(hit["buy"])
        go, side, edge, p, e = decide(model, hit["strike"], amer)
        mark = "GO" if go else "NO-GO"
        if go:
            kr_go += 1
        print(
            f"  {mark} {away}@{home} strike={hit['strike']} ask={hit['buy']:.3f} (~{amer:+d}) "
            f"blend_edge={edge:+.1f} {side} EV={e:+.1%}{outcome(side, hit['strike'], actual)}"
        )
    print(f"Kalshi/RH fillable GOs: {kr_go}\n")

    print("--- Crypto.com (nearest Over to DK line; finished games usually missing) ---")
    c_go = 0
    for away, home, model, line, actual in GAMES:
        hit = nearest_crypto_over(away, home, line)
        if not hit:
            print(f"  {away}@{home}: no Crypto.com totals (rolled off or missing)")
            continue
        amer = american_from_buy(hit["buy"])
        go, side, edge, p, e = decide(model, hit["strike"], amer)
        # Only OVER contracts fetched; if model wants under, still report
        mark = "GO" if go and side == "OVER" else "NO-GO"
        if mark == "GO":
            c_go += 1
        print(
            f"  {mark} {away}@{home} strike={hit['strike']} ask={hit['buy']:.3f} (~{amer:+d}) "
            f"blend_edge={edge:+.1f} model_side={side} EV={e:+.1%}{outcome(side, hit['strike'], actual)}"
        )
    print(f"Crypto.com GOs: {c_go}")


if __name__ == "__main__":
    main()
