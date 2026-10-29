#!/usr/bin/env bash
# Daily PossiblyMaybe paper ritual: at-risk book, scores probe, soft-spot slate, dashboard.
# Usage: scripts/pm-calculate.sh [yyyymmdd]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

DAY="${1:-$(date +%Y%m%d)}"
TOMORROW="$(date -d "${DAY:0:4}-${DAY:4:2}-${DAY:6:2} +1 day" +%Y%m%d 2>/dev/null || python3 -c "
from datetime import datetime, timedelta
d=datetime.strptime('$DAY','%Y%m%d')+timedelta(days=1)
print(d.strftime('%Y%m%d'))
")"

CLI=(dotnet run --project PossiblyMaybe.Cli -c Release --no-build --)
BUILD=(dotnet build PossiblyMaybe.Cli -c Release -v q --nologo)

echo "========================================"
echo " PossiblyMaybe /pm-calculate"
echo " day=$DAY  tomorrow=$TOMORROW"
echo " root=$ROOT"
echo "========================================"

echo
echo ">>> build"
"${BUILD[@]}"

echo
echo ">>> paper status (equity / at-risk / available; lanes at ~\$0 are OUT)"
"${CLI[@]}" paper || true

echo
echo ">>> open tickets + ESPN finals probe (at risk)"
python3 - "$ROOT" "$DAY" <<'PY'
import json, re, sqlite3, sys, urllib.request
from collections import defaultdict

root, today = sys.argv[1], sys.argv[2]
db = f"{root}/paper.db"
con = sqlite3.connect(db)
con.row_factory = sqlite3.Row
rows = con.execute(
    "SELECT GameKey, Side, Line, AmericanOdds, StakeDollars, Scheme, Sport, Result "
    "FROM LedgerEntries WHERE Result IS NULL ORDER BY GameKey, Scheme"
).fetchall()
if not rows:
    print("No open tickets.")
    raise SystemExit(0)

by_game = defaultdict(list)
for r in rows:
    by_game[r["GameKey"]].append(r)

print(f"Open game keys: {len(by_game)}  (tickets={len(rows)})")
for key, tickets in by_game.items():
    sample = tickets[0]
    stakes = " ".join(f"{t['Scheme']}=${t['StakeDollars']:.2f}" for t in tickets)
    total = sum(t["StakeDollars"] for t in tickets)
    print(f"  {key}  {sample['Side']} {sample['Line']} @{sample['AmericanOdds']}  risk=${total:.2f}")
    print(f"    {stakes}")

dates = sorted({m.group(1) for k in by_game if (m := re.match(r"^(\d{8})\b", k))})
# also probe today even if nothing open dated today
if today not in dates:
    dates.append(today)
dates = sorted(set(dates))

UA = {"User-Agent": "PossiblyMaybe/pm-calculate"}

def fetch(url):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=25) as r:
        return json.load(r)

def nba_board(yyyymmdd):
    url = f"https://site.api.espn.com/apis/site/v2/sports/basketball/nba/scoreboard?dates={yyyymmdd}"
    try:
        data = fetch(url)
    except Exception as e:
        print(f"  NBA scoreboard {yyyymmdd}: FAIL {e}")
        return []
    out = []
    for ev in data.get("events") or []:
        comp = (ev.get("competitions") or [{}])[0]
        status = ((comp.get("status") or {}).get("type") or {}).get("name") or ""
        state = ((comp.get("status") or {}).get("type") or {}).get("state") or ""
        teams = {}
        scores = {}
        for c in comp.get("competitors") or []:
            abbr = ((c.get("team") or {}).get("abbreviation") or "").upper()
            ha = c.get("homeAway")
            teams[ha] = abbr
            scores[ha] = c.get("score")
        out.append({
            "sport": "NBA",
            "away": teams.get("away"),
            "home": teams.get("home"),
            "away_score": scores.get("away"),
            "home_score": scores.get("home"),
            "final": state == "post" or "FINAL" in status.upper(),
            "status": status or state,
        })
    return out

def mls_board(yyyymmdd):
    url = f"https://site.api.espn.com/apis/site/v2/sports/soccer/usa.1/scoreboard?dates={yyyymmdd}"
    try:
        data = fetch(url)
    except Exception as e:
        print(f"  MLS scoreboard {yyyymmdd}: FAIL {e}")
        return []
    out = []
    for ev in data.get("events") or []:
        comp = (ev.get("competitions") or [{}])[0]
        status = ((comp.get("status") or {}).get("type") or {}).get("name") or ""
        state = ((comp.get("status") or {}).get("type") or {}).get("state") or ""
        teams = {}
        scores = {}
        for c in comp.get("competitors") or []:
            abbr = ((c.get("team") or {}).get("abbreviation") or "").upper()
            ha = c.get("homeAway")
            teams[ha] = abbr
            scores[ha] = c.get("score")
        out.append({
            "sport": "MLS",
            "away": teams.get("away"),
            "home": teams.get("home"),
            "away_score": scores.get("away"),
            "home_score": scores.get("home"),
            "final": state == "post" or "FINAL" in status.upper(),
            "status": status or state,
        })
    return out

# Map common paper keys like CIN@MIA onto ESPN abbrs when possible (best-effort).
print()
print("--- ESPN probe (finals → settle-go when total known) ---")
for d in dates:
    games = nba_board(d) + mls_board(d)
    if not games:
        print(f"  {d}: no games / fetch empty")
        continue
    print(f"  {d}:")
    for g in games:
        mark = "FINAL" if g["final"] else g["status"]
        print(f"    [{g['sport']}] {g['away']}@{g['home']}  {g['away_score']}-{g['home_score']}  {mark}")

    # Match open tickets on this date by substring team codes when present
    for key, tickets in by_game.items():
        if not key.startswith(d):
            continue
        m = re.search(r"(\w+)@(\w+)$", key)
        if not m:
            continue
        away, home = m.group(1).upper(), m.group(2).upper()
        hit = next((g for g in games if g["away"] == away and g["home"] == home), None)
        if not hit:
            # loose contains
            hit = next((g for g in games if away in (g["away"] or "") and home in (g["home"] or "")), None)
        sample = tickets[0]
        if not hit:
            print(f"    !! open {key} — no ESPN match yet")
            continue
        if hit["final"]:
            try:
                total = float(hit["away_score"]) + float(hit["home_score"])
            except (TypeError, ValueError):
                total = None
            side, line = sample["Side"], float(sample["Line"])
            if total is None:
                print(f"    !! open {key} FINAL but scores missing")
                continue
            if side == "OVER":
                result = "W" if total > line else ("P" if total == line else "L")
            elif side == "UNDER":
                result = "W" if total < line else ("P" if total == line else "L")
            else:
                result = "?"
            print(f"    => SETTLE {key}  total={total:.1f} vs {side} {line} → {result}")
            print(f"       dotnet run --project PossiblyMaybe.Cli -c Release -- settle-go \"{key}\" {result} {total}")
        else:
            print(f"    .. open {key} still {hit['status']} ({hit['away_score']}-{hit['home_score']})")
PY

echo
echo ">>> Kalshi vs DraftKings (open paper tickets)"
python3 "$ROOT/scripts/kalshi_compare.py" --paper-db "$ROOT/paper.db" || echo "(Kalshi compare skipped/failed — continuing)"

echo
echo ">>> import-box (NBA pace warehouse; ok if nothing new)"
"${CLI[@]}" import-box || echo "(import-box skipped/failed — continuing)"

SLATE_LOG="$(mktemp)"
trap 'rm -f "$SLATE_LOG"' EXIT

SOCCER_PASS=(MLS LIGUE1 LIGAMX SERIEA)

echo
echo ">>> slate NBA $DAY"
("${CLI[@]}" slate-nba "$DAY" 1000 || true) | tee -a "$SLATE_LOG"

for sp in "${SOCCER_PASS[@]}"; do
  echo
  echo ">>> slate $sp $DAY"
  ("${CLI[@]}" slate-soccer "$sp" "$DAY" 1000 || true) | tee -a "$SLATE_LOG"
done

echo
echo ">>> slate NBA $TOMORROW (lookahead)"
("${CLI[@]}" slate-nba "$TOMORROW" 1000 || true) | tee -a "$SLATE_LOG"

for sp in "${SOCCER_PASS[@]}"; do
  echo
  echo ">>> slate $sp $TOMORROW (lookahead)"
  ("${CLI[@]}" slate-soccer "$sp" "$TOMORROW" 1000 || true) | tee -a "$SLATE_LOG"
done

echo
echo ">>> Kalshi vs DraftKings (today/tomorrow GOs from slate)"
# paper-go "KEY" SIDE LINE ODDS P ABSEDGE SPORT
mapfile -t GO_ARGS < <(python3 - "$SLATE_LOG" <<'PY'
import re, sys
text = open(sys.argv[1], encoding="utf-8", errors="replace").read()
# paper-go "20261028 CIN@MIA" OVER 3.5 -110 0.712 0.76 MLS
pat = re.compile(
    r'paper-go\s+"(\d{8})\s+(\w+)@(\w+)"\s+(OVER|UNDER)\s+([0-9.]+)\s+(-?\d+)\s+([0-9.]+)\s+([0-9.]+)\s+(\w+)',
    re.I,
)
seen = set()
for m in pat.finditer(text):
    day, away, home, side, line, odds, p, _edge, sport = m.groups()
    key = (sport.upper(), day, away.upper(), home.upper(), side.upper(), line)
    if key in seen:
        continue
    seen.add(key)
    print(f"{sport.upper()}|{day}|{away.upper()}|{home.upper()}|{side.upper()}|{line}|{odds}|{p}")
PY
)
if ((${#GO_ARGS[@]})); then
  KALSHI_ARGS=()
  for g in "${GO_ARGS[@]}"; do
    KALSHI_ARGS+=(--slate "$g")
  done
  python3 "$ROOT/scripts/kalshi_compare.py" --sports NBA,MLS "${KALSHI_ARGS[@]}" || true
else
  echo "No GO paper-go lines in slate output — nothing to compare."
fi

echo
echo ">>> dashboard → ui/index.html"
"${CLI[@]}" dashboard || true

echo
echo "========================================"
echo " Next actions"
echo "========================================"
echo "1. For each GO line printed above, run the suggested paper-go (fans out all 6 lanes)."
echo "2. Prefer the venue with better EV in Kalshi vs DK (Kalshi = prediction-market YES/NO price)."
echo "3. For each FINAL settle hint, run settle-go (or confirm total first)."
echo "4. Open ui/index.html (or http://127.0.0.1:8765/ if hosted) for equity + calendar."
echo "5. Lane with available ~\$0 cannot bet — that scheme is OUT (no reload)."
echo "Done."
