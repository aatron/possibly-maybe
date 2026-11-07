import json
import sqlite3
import sys
import unittest
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import line_tracker as lt  # noqa: E402

FX = Path(__file__).parent / "fixtures"
load = lambda n: json.loads((FX / n).read_text())


class ParseTests(unittest.TestCase):
    def setUp(self):
        self.games = lt.parse_espn(load("espn_nfl.json"), "NFL")

    def test_espn_skips_finished_and_canonicalises(self):
        self.assertEqual([g["key"] for g in self.games], ["20261108 DAL@IND", "20261108 LAR@WAS"])
        r = self.games[0]["rows"][0]
        self.assertEqual((r["line"], r["source"], r["over_american"], r["open_line"]), (51.5, "Draft Kings", -110, 52.5))
        self.assertAlmostEqual(r["over_ask"], 110 / 210)

    def test_kalshi_match_with_alias_and_rows(self):
        mk = load("kalshi_nfl.json")["markets"]
        ind, was = self.games
        hits = lt.kalshi_match(mk, ind)
        self.assertTrue(hits and all(m["event_ticker"].endswith("DALIND") for m in hits))
        self.assertTrue(all(m["event_ticker"].endswith("LARWAS") for m in lt.kalshi_match(mk, was)))  # WSH->WAS
        rows = lt.kalshi_rows(hits)
        self.assertIn("over_ask", rows[0])
        self.assertEqual(rows[0]["venue"], "kalshi")

    def test_kalshi_mlb_ticker_has_time_and_ignores_other_dates(self):
        g = {"start": datetime(2026, 10, 29, 0, 0, tzinfo=timezone.utc), "away": "MIL", "home": "TB"}  # 20:00 ET Oct 28
        mk = load("kalshi_nfl.json")["markets"]
        self.assertEqual(len(lt.kalshi_match(mk, g)), 1)
        g["start"] = datetime(2026, 10, 29, 1, 0, tzinfo=timezone.utc)  # 21:00 ET: doubleheader time mismatch
        self.assertEqual(lt.kalshi_match(mk, g), [])

    def test_cdc_match_and_rows(self):
        ev = lt.cdc_match(load("cdc_events.json")["data"], self.games[0])
        self.assertIn("DAL", ev["title"])
        rows = lt.cdc_rows(load("cdc_contracts.json")["data"], ev["id"])
        self.assertEqual(sorted(r["line"] for r in rows), [47.5, 48.5])  # full-game Over only (not Under, not 1H)
        self.assertEqual((rows[0]["over_ask"] is not None, rows[0]["under_ask"] is not None), (True, True))

    def test_implied_line_interpolates(self):
        rows = [{"line": 44.5, "over_ask": 0.62, "under_ask": 0.40}, {"line": 45.5, "over_ask": 0.47, "under_ask": 0.55}]
        self.assertAlmostEqual(lt.implied_line(rows), 45.23, places=2)


class StoreTests(unittest.TestCase):
    def test_idempotent_and_report(self):
        con = sqlite3.connect(":memory:")
        con.executescript(lt.SCHEMA)
        g = lt.parse_espn(load("espn_nfl.json"), "NFL")[:1]
        now = datetime(2026, 11, 7, 12, 0, tzinfo=timezone.utc)
        self.assertEqual(lt.store(con, g, now), 1)
        self.assertEqual(lt.store(con, g, now), 0)
        self.assertEqual(con.execute("SELECT minutes_to_start FROM line_snapshots").fetchone()[0], 1800.0)
        lt.report(con, now)


if __name__ == "__main__":
    unittest.main()
