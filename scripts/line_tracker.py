#!/usr/bin/env python3
"""Snapshot logger for all tracked-league TOTALS lines (ESPN, Kalshi, Crypto.com) -> data/line_snapshots.db.

Append-only table `line_snapshots`; open = first snapshot, close = last pre-start snapshot.
game_key = `yyyymmdd AWAY@HOME HHMM` (ET date + kickoff; HHMM keeps doubleheaders distinct).
Public unauthenticated GETs only. Run every 15-30 min; `--report` prints movement + best price.
Add a sport by one SPORTS entry (or merge via data/line_tracker_sports.json). Never touches paper.db.
shortcut: totals only (spread / team-total / 1H series known on Kalshi for NFL/MLB/NCAA); add when parse path stays small.
"""
from __future__ import annotations

import argparse
import json
import re
import sqlite3
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path
from zoneinfo import ZoneInfo

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kalshi_compare as kc  # noqa: E402  (get_json, all_open_markets, ffloat, implied_from_american)

ET = ZoneInfo("America/New_York")
UA = {"User-Agent": "PossiblyMaybe/line-tracker"}
ESPN = "https://site.api.espn.com/apis/site/v2/sports/{path}/scoreboard?dates={d}"
CDC = "https://data-api.crypto.com/api/v1/predictions"
ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DB = ROOT / "data" / "line_snapshots.db"
EXTRA_SPORTS = ROOT / "data" / "line_tracker_sports.json"
# Rate-limit: pause between ESPN day calls and between leagues; back off on HTTP errors.
DAY_SLEEP_S = 0.35
LEAGUE_SLEEP_S = 0.75
FAIL_BACKOFF_S = 2.0

# espn path required; kalshi / cdc optional (None → skip that venue with a log, never crash).
SPORTS_BASE: dict[str, dict] = {
    "NBA": {"espn": "basketball/nba", "kalshi": "KXNBATOTAL", "cdc": "NBA"},
    "NCAAB": {"espn": "basketball/mens-college-basketball", "kalshi": "KXNCAAMBTOTAL", "cdc": None},
    "NCAAF": {"espn": "football/college-football", "kalshi": "KXNCAAFTOTAL", "cdc": None},
    "NFL": {"espn": "football/nfl", "kalshi": "KXNFLTOTAL", "cdc": "NFL"},
    "MLB": {"espn": "baseball/mlb", "kalshi": "KXMLBTOTAL", "cdc": "MLB"},
    "MLS": {"espn": "soccer/usa.1", "kalshi": "KXMLSTOTAL", "cdc": "MLS"},
    "LIGUE1": {"espn": "soccer/fra.1", "kalshi": "KXLIGUE1TOTAL", "cdc": None},
    "LIGAMX": {"espn": "soccer/mex.1", "kalshi": "KXLIGAMXTOTAL", "cdc": None},
    "SERIEA": {"espn": "soccer/ita.1", "kalshi": "KXSERIEATOTAL", "cdc": None},
    "LALIGA": {"espn": "soccer/esp.1", "kalshi": None, "cdc": None},  # no public Kalshi totals series known
    "EFL": {"espn": "soccer/eng.2", "kalshi": None, "cdc": None},  # Championship; no public series known
    "BUNDESLIGA": {"espn": "soccer/ger.1", "kalshi": "KXBUNDESLIGATOTAL", "cdc": None},  # Crypto kind TBD
}
# ESPN abbr -> repo GameKey code (SportTeams.cs / TeamCodes conventions)
CANON = {
    "NFL": {"WSH": "WAS"},
    "MLB": {"CHW": "CWS", "ATH": "OAK"},
    "NBA": {"GS": "GSW", "NO": "NOP", "NY": "NYK", "SA": "SAS", "UTAH": "UTA", "WSH": "WAS"},
}
# Kalshi tickers use their own codes; treat these as the same team when matching
ALIASES = [{"WSH", "WAS"}, {"JAX", "JAC"}, {"LAR", "LA"}, {"ARI", "AZ"}, {"ATH", "OAK"},
           {"CWS", "CHW"}, {"KC", "KCR"}, {"SD", "SDP"}, {"SF", "SFG"}, {"TB", "TBR"},
           {"GS", "GSW"}, {"NO", "NOP"}, {"NY", "NYK"}, {"SA", "SAS"}, {"UTAH", "UTA"},
           {"PHO", "PHX"}, {"BK", "BKN"}, {"BRO", "BKN"}]

SCHEMA = """
CREATE TABLE IF NOT EXISTS line_snapshots(
  captured_at TEXT NOT NULL, sport TEXT NOT NULL, game_key TEXT NOT NULL, start_utc TEXT NOT NULL,
  minutes_to_start REAL, venue TEXT NOT NULL, source TEXT, line REAL NOT NULL,
  over_bid REAL, over_ask REAL, under_bid REAL, under_ask REAL,  -- dollars per $1 payout
  over_american INTEGER, under_american INTEGER, open_line REAL, ticker TEXT,
  UNIQUE(captured_at, game_key, venue, source, line));
"""
COLS = ["captured_at", "sport", "game_key", "start_utc", "minutes_to_start", "venue", "source", "line",
        "over_bid", "over_ask", "under_bid", "under_ask", "over_american", "under_american", "open_line", "ticker"]


def log(msg: str) -> None:
    print(msg, flush=True)


def load_sports() -> dict[str, dict]:
    """Table-driven SPORTS + optional JSON merge for census leagues (A4 → C6)."""
    sports = {k: dict(v) for k, v in SPORTS_BASE.items()}
    if EXTRA_SPORTS.exists():
        try:
            extra = json.loads(EXTRA_SPORTS.read_text())
            for k, v in (extra or {}).items():
                sports[k.upper()] = {**sports.get(k.upper(), {}), **v}
            log(f"merged {len(extra)} sport(s) from {EXTRA_SPORTS.name}")
        except Exception as e:  # noqa: BLE001
            log(f"EXTRA SPORTS FAIL ({EXTRA_SPORTS}): {e}")
    return sports


def fetch(url: str):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=20) as r:
        return json.load(r)


def fetch_retry(url: str, label: str, attempts: int = 3):
    """GET with simple backoff; returns None on exhaustion (caller logs / skips)."""
    delay = FAIL_BACKOFF_S
    for i in range(attempts):
        try:
            return fetch(url)
        except (urllib.error.HTTPError, urllib.error.URLError, TimeoutError, json.JSONDecodeError) as e:
            log(f"{label} FAIL ({i + 1}/{attempts}): {e}")
            if i + 1 < attempts:
                time.sleep(delay)
                delay *= 2
    return None


def num(x):
    try:
        return float(re.sub(r"^[ou+]", "", str(x))) if x not in (None, "") else None
    except ValueError:
        return None


def american_price(a) -> float | None:
    """American odds string -> implied cost per $1 payout (vig not removed)."""
    try:
        return kc.implied_from_american(int(str(a).replace("+", "")))
    except (TypeError, ValueError):
        return None


# ---- parsing (pure; unit-tested) ----

def game_key(start: datetime, away: str, home: str) -> str:
    """Doubleheader-safe: ET date + teams + ET kickoff HHMM (game_key alone was not enough)."""
    et = start.astimezone(ET)
    return f"{et:%Y%m%d} {away}@{home} {et:%H%M}"


def _team_token(team: dict, sport: str) -> str:
    """ESPN abbreviation, CANON-mapped; unmapped keeps own abbr (never another club's alias)."""
    abbr = (team.get("abbreviation") or team.get("shortDisplayName") or team.get("name") or "?").strip()
    # collapse spaces so game_key stays \\w+-friendly for the C# KeyRe
    abbr = re.sub(r"\s+", "", abbr)
    return CANON.get(sport, {}).get(abbr, abbr)


def parse_espn(sb: dict, sport: str) -> list[dict]:
    """Pre-game events with a total -> game dicts incl. one espn row."""
    out = []
    for ev in sb.get("events") or []:
        if (ev.get("status") or {}).get("type", {}).get("state") != "pre":
            continue
        comp = ev["competitions"][0]
        t = {c["homeAway"]: c["team"] for c in comp["competitors"]}
        away, home = (_team_token(t[k], sport) for k in ("away", "home"))
        start = datetime.fromisoformat(ev["date"].replace("Z", "+00:00"))
        nick = (t["away"].get("name") or away, t["home"].get("name") or home)
        g = {"sport": sport, "start": start, "away": away, "home": home, "nick": nick,
             "key": game_key(start, away, home), "rows": []}
        odds = (comp.get("odds") or [None])[0]
        line = num((odds or {}).get("overUnder"))
        if line is not None:
            tot = odds.get("total") or {}
            ov, un = (tot.get(s) or {} for s in ("over", "under"))
            g["rows"].append({
                "venue": "espn", "source": odds["provider"]["name"], "line": line,
                "over_ask": american_price(ov.get("close", {}).get("odds")),
                "under_ask": american_price(un.get("close", {}).get("odds")),
                "over_american": _int(ov.get("close", {}).get("odds")),
                "under_american": _int(un.get("close", {}).get("odds")),
                "open_line": num(ov.get("open", {}).get("line"))})
        out.append(g)
    return out


def _int(x):
    try:
        return int(str(x).replace("+", ""))
    except (TypeError, ValueError):
        return None


def _same(a: str, b: str) -> bool:
    return a == b or any(a in s and b in s for s in ALIASES)


def kalshi_match(markets: list[dict], g: dict) -> list[dict]:
    """Markets of the event whose ticker is -<yyMONdd>[HHMM]<AWAY><HOME> for this game (home/away either order)."""
    stamp = f"{g['start'].astimezone(ET):%y%b%d}".upper()
    hhmm = f"{g['start'].astimezone(ET):%H%M}"
    hits = []
    for m in markets:
        mm = re.search(r"-(\d{2}[A-Z]{3}\d{2})(\d{4})?([A-Z]+)$", m.get("event_ticker", ""))
        if not mm or mm.group(1) != stamp or (mm.group(2) and mm.group(2) != hhmm):  # HHMM splits doubleheaders
            continue
        tail = mm.group(3)
        for a, h in ((g["away"], g["home"]), (g["home"], g["away"])):
            if any(tail[:i] and _same(tail[:i], a) and _same(tail[i:], h) for i in range(1, len(tail))):
                hits.append(m)
                break
    return hits


def kalshi_rows(markets: list[dict]) -> list[dict]:
    rows = []
    for m in markets:
        k = kc.ffloat(m.get("floor_strike"))
        if k is None:
            continue
        rows.append({"venue": "kalshi", "source": m["event_ticker"].split("-")[0], "line": k, "ticker": m.get("ticker"),
                     "over_bid": kc.ffloat(m.get("yes_bid_dollars")), "over_ask": kc.ffloat(m.get("yes_ask_dollars")),
                     "under_bid": kc.ffloat(m.get("no_bid_dollars")), "under_ask": kc.ffloat(m.get("no_ask_dollars"))})
    return rows


def cdc_match(events: list[dict], g: dict) -> dict | None:
    """Crypto.com match event: same kickoff (+-30 min), 'AWAY nick @ HOME nick' title containing both nicknames."""
    for e in events:
        if e.get("type") != "match" or " @ " not in e.get("title", ""):
            continue
        a, h = e["title"].split(" @ ", 1)
        when = datetime.fromisoformat(e["event_date"].replace("Z", "+00:00"))
        if g["nick"][0] in a and g["nick"][1] in h and abs(when - g["start"]) <= timedelta(minutes=30):
            return e
    return None


def cdc_rows(contracts: list[dict], event_id: str) -> list[dict]:
    """Full-game 'Over X' contracts: yes = buy over, no = buy under (asks only; no bids published)."""
    rows = []
    for c in contracts:
        mt = c.get("market_type") or {}
        m = re.match(r"^Over\s+(\d+(?:\.\d+)?)", c.get("title") or "", re.I)
        if mt.get("name") != "total" or mt.get("period") != "full" or not m or c.get("status") in ("voided", "suspended"):
            continue
        rows.append({"venue": "cryptocom", "source": "crypto.com", "line": float(m.group(1)), "ticker": c.get("symbol"),
                     "over_ask": num(c.get("yes")), "under_ask": num(c.get("no"))})
    return rows


# ---- collection ----

def collect(sports: list[str], days: int, now: datetime, sports_cfg: dict[str, dict] | None = None) -> list[dict]:
    cfg_all = sports_cfg or load_sports()
    snaps = []
    today = now.astimezone(ET)
    for si, sport in enumerate(sports):
        if si:
            time.sleep(LEAGUE_SLEEP_S)
        cfg = cfg_all[sport]
        games: list[dict] = []
        try:
            # one call per day: ESPN rejects date ranges (400)
            for i in range(days + 1):
                if i:
                    time.sleep(DAY_SLEEP_S)
                sb = fetch_retry(ESPN.format(path=cfg["espn"], d=f"{today + timedelta(days=i):%Y%m%d}"),
                                 f"{sport} ESPN")
                if not sb:
                    continue
                games += [g for g in parse_espn(sb, sport) if g["start"] > now and g["key"] not in {x["key"] for x in games}]
        except Exception as e:  # noqa: BLE001
            log(f"{sport} ESPN FAIL: {e}")
        log(f"{sport}: {len(games)} upcoming games (ESPN)")
        if not games:
            continue  # off-season / empty slate: nothing to match

        kmk: list[dict] = []
        series = cfg.get("kalshi")
        if not series:
            log(f"{sport} Kalshi SKIP: no series ticker configured")
        else:
            try:
                kmk = kc.all_open_markets(series)
            except Exception as e:  # noqa: BLE001
                log(f"{sport} Kalshi FAIL: {e}")
                time.sleep(FAIL_BACKOFF_S)

        cev: list[dict] = []
        kind = cfg.get("cdc")
        if not kind:
            log(f"{sport} Crypto.com SKIP: no kind configured")
        else:
            body = fetch_retry(f"{CDC}/events?kind={kind}&type=match&limit=100", f"{sport} Crypto.com events")
            cev = (body or {}).get("data") or []

        for g in games:
            if kmk:
                g["rows"] += kalshi_rows(kalshi_match(kmk, g))
            if cev:
                ev = cdc_match(cev, g)
                if ev:
                    body = fetch_retry(f"{CDC}/events/{ev['id']}/contracts?limit=1000",
                                       f"{g['key']} Crypto.com contracts")
                    if body:
                        g["rows"] += cdc_rows(body.get("data") or [], ev["id"])
                    time.sleep(DAY_SLEEP_S)
            venues = sorted({r["venue"] for r in g["rows"]})
            log(f"  {g['key']}: {len(g['rows'])} rows {venues}")
            snaps.append(g)
    return snaps


def store(con: sqlite3.Connection, games: list[dict], now: datetime) -> int:
    ts = now.replace(microsecond=0).isoformat()
    n = 0
    for g in games:
        mins = round((g["start"] - now).total_seconds() / 60, 1)
        for r in g["rows"]:
            row = {"captured_at": ts, "sport": g["sport"], "game_key": g["key"], "start_utc": g["start"].isoformat(),
                   "minutes_to_start": mins, **r}
            n += con.execute(f"INSERT OR IGNORE INTO line_snapshots({','.join(COLS)}) VALUES({','.join('?' * len(COLS))})",
                             [row.get(c) for c in COLS]).rowcount
    con.commit()
    return n


# ---- report ----

def implied_line(rows: list[dict]) -> float | None:
    """Strike where mid P(over)=0.5 (interpolated) from tight two-sided quotes; single-line venues return their line."""
    if len(rows) == 1:
        return rows[0]["line"]
    pts = sorted((r["line"], (r["over_ask"] + 1 - r["under_ask"]) / 2) for r in rows
                 if r["over_ask"] is not None and r["under_ask"] is not None and r["over_ask"] + r["under_ask"] <= 1.10)
    for (k0, p0), (k1, p1) in zip(pts, pts[1:]):
        if p0 >= 0.5 >= p1 and p0 != p1:
            return round(k0 + (k1 - k0) * (p0 - 0.5) / (p0 - p1), 2)
    return None


def report(con: sqlite3.Connection, now: datetime) -> None:
    con.row_factory = sqlite3.Row
    games = con.execute("SELECT DISTINCT sport, game_key, start_utc FROM line_snapshots "
                        "WHERE start_utc > ? ORDER BY start_utc, game_key", (now.isoformat(),)).fetchall()
    if not games:
        log("No upcoming games in the snapshot DB.")
    for g in games:
        log(f"\n{g['sport']} {g['game_key']}  start {g['start_utc'][:16]}Z")
        main = None
        for venue, source in con.execute("SELECT DISTINCT venue, source FROM line_snapshots WHERE game_key=? ORDER BY venue", (g["game_key"],)).fetchall():
            snaps = [r[0] for r in con.execute("SELECT DISTINCT captured_at FROM line_snapshots WHERE game_key=? AND venue=? AND source=? ORDER BY 1",
                                               (g["game_key"], venue, source))]
            lines = []
            for ts in (snaps[0], snaps[-1]):
                rs = con.execute("SELECT * FROM line_snapshots WHERE game_key=? AND venue=? AND source=? AND captured_at=?",
                                 (g["game_key"], venue, source, ts)).fetchall()
                lines.append(implied_line(rs))
            f, l = lines
            move = f"{l - f:+.2f}" if f is not None and l is not None else "n/a"
            extra = ""
            if venue == "espn":
                main = l
                ol = con.execute("SELECT open_line FROM line_snapshots WHERE game_key=? AND venue='espn' ORDER BY captured_at DESC LIMIT 1", (g["game_key"],)).fetchone()[0]
                extra = f"  (ESPN-reported open {ol})" if ol is not None else ""
            log(f"  {venue:<9} first {f} @ {snaps[0][5:16]}  latest {l} @ {snaps[-1][5:16]}  move {move}  [{len(snaps)} snaps, {source}]{extra}")
        if main is None:
            continue
        best = {"over": [], "under": []}
        for venue, ov, un in con.execute(
                "SELECT venue, over_ask, under_ask FROM line_snapshots s WHERE game_key=? AND line=? AND captured_at="
                "(SELECT MAX(captured_at) FROM line_snapshots WHERE game_key=s.game_key AND venue=s.venue AND source=s.source)",
                (g["game_key"], main)):
            for side, p in (("over", ov), ("under", un)):
                if p:
                    best[side].append((p, venue))
        for side, v in best.items():
            v.sort()
            log(f"  best {side} @ {main}: " + "  ".join(f"{n} {p:.3f}" for p, n in v) if v else f"  best {side} @ {main}: none")


def main() -> int:
    sports_cfg = load_sports()
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--sports", default="ALL",
                    help="Comma sports, or ALL for every configured league")
    ap.add_argument("--days", type=int, default=5, help="look-ahead window (days)")
    ap.add_argument("--db", default=str(DEFAULT_DB))
    ap.add_argument("--report", action="store_true", help="print movement/best price from stored snapshots; no fetch")
    a = ap.parse_args()
    Path(a.db).parent.mkdir(parents=True, exist_ok=True)
    con = sqlite3.connect(a.db)
    con.executescript(SCHEMA)
    now = datetime.now(timezone.utc)
    if a.report:
        report(con, now)
        return 0
    if a.sports.strip().upper() == "ALL":
        sports = list(sports_cfg)
    else:
        sports = [s.strip().upper() for s in a.sports.split(",") if s.strip()]
    bad = [s for s in sports if s not in sports_cfg]
    if bad:
        log(f"unknown sport(s) {bad}; known: {sorted(sports_cfg)}")
        return 2
    # module-level SPORTS kept for tests / callers that read it
    global SPORTS  # noqa: PLW0603
    SPORTS = sports_cfg
    n = store(con, collect(sports, a.days, now, sports_cfg), now)
    log(f"stored {n} rows -> {a.db}")
    return 0


SPORTS = load_sports()

if __name__ == "__main__":
    sys.exit(main())
