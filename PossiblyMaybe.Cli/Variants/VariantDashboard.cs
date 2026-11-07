using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

public static class VariantDashboard
{
    public static async Task WriteAsync(
        string variantsDbPath,
        string uiDir,
        string? radarDbPath = null,
        CancellationToken ct = default)
    {
        var (db, _) = await VariantLedger.OpenAsync(variantsDbPath, ct);
        await using (db)
        {
            var rows = await db.ShadowEntries.AsNoTracking().ToListAsync(ct);
            var radarPath = radarDbPath
                ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(variantsDbPath)) ?? "data", "radar.db");
            var waveRows = await RadarStore.LoadCellsAsync(radarPath, ct);
            var payload = BuildPayload(rows, waveRows);
            Directory.CreateDirectory(uiDir);
            var path = Path.Combine(uiDir, "variants.html");
            await File.WriteAllTextAsync(path, Render(payload), ct);
            Console.WriteLine($"Wrote {path} ({rows.Count} shadow rows, {waveRows.Count} wave cells).");
        }
    }

    private static object BuildPayload(List<ShadowEntry> rows, IReadOnlyList<RadarCellRow> waveRows)
    {
        var byVar = rows.GroupBy(r => r.VariantId).ToDictionary(g => g.Key, g => g.ToList());
        var variants = VariantRegistry.All.Select(v =>
        {
            byVar.TryGetValue(v.Id, out var list);
            list ??= [];
            var picks = list.Where(x => x.SkipReason is null).ToList();
            var settled = picks.Where(x => x.Result is not null).ToList();
            var leagues = picks.Select(p => p.Sport).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Select(sport =>
                {
                    var sp = picks.Where(p => p.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase)).ToList();
                    var st = sp.Where(p => p.Result is not null).ToList();
                    var hits = st.Count(p => p.Result == "W");
                    var units = st.Sum(p => p.PnL ?? 0);
                    var clv = st.Where(p => p.ClvPts is not null).Select(p => p.ClvPts!.Value).ToList();
                    var skips = list.Where(x => x.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase)
                                                && x.SkipReason is not null)
                        .GroupBy(x => x.SkipReason!)
                        .ToDictionary(g => g.Key, g => g.Count());
                    var series = st
                        .OrderBy(p => p.SettledAt ?? p.EvaluatedAt)
                        .Select((p, i) => new
                        {
                            t = (p.SettledAt ?? p.EvaluatedAt).ToString("o"),
                            units = st.Take(i + 1).Sum(x => x.PnL ?? 0),
                            clv = st.Take(i + 1).Where(x => x.ClvPts is not null).Select(x => x.ClvPts!.Value)
                                .DefaultIfEmpty(0).Average(),
                        });
                    return new
                    {
                        sport,
                        n = st.Count,
                        hits,
                        hitRate = st.Count == 0 ? 0 : (double)hits / st.Count,
                        flatUnits = units,
                        meanClv = clv.Count == 0 ? (double?)null : clv.Average(),
                        open = sp.Count(p => p.Result is null),
                        skips,
                        series,
                    };
                }).ToList();

            var allSettled = settled;
            return new
            {
                id = v.Id,
                label = v.Label,
                n = allSettled.Count,
                flatUnits = allSettled.Sum(p => p.PnL ?? 0),
                hitRate = allSettled.Count == 0 ? 0
                    : allSettled.Count(p => p.Result == "W") / (double)allSettled.Count,
                meanClv = allSettled.Where(p => p.ClvPts is not null).Select(p => p.ClvPts!.Value).ToList() is { Count: > 0 } clvs
                    ? clvs.Average() : (double?)null,
                leagues,
                skips = list.Where(x => x.SkipReason is not null)
                    .GroupBy(x => x.SkipReason!).ToDictionary(g => g.Key, g => g.Count()),
            };
        }).ToList();

        var waves = waveRows.Select(w =>
        {
            var cp = string.IsNullOrEmpty(w.Checkpoint)
                ? Checkpoints.Evaluate(w.N, w.MeanClv, w.ClvSe)
                : null;
            return new
            {
                league = w.League,
                market = w.Market,
                variantId = w.VariantId,
                n = w.N,
                meanClv = w.MeanClv,
                clvSe = w.ClvSe,
                rollingClv = w.RollingClv,
                cusum = w.Cusum,
                pValue = w.PValue,
                bhSignificant = w.BhSignificant,
                wave = w.Wave,
                stage = w.Stage,
                checkpoint = cp?.Stage.ToString() ?? w.Checkpoint,
                checkpointVerdict = cp?.Verdict ?? w.CheckpointVerdict,
                allowsWideStake = cp?.AllowsWideStake ?? w.AllowsWideStake,
                weeksOn = w.WeeksOn,
                nTrials = w.NTrials,
                promoteEligible = w.PromoteEligible,
                demote = w.Demote,
            };
        }).ToList();
        var nTrials = waveRows.Count == 0 ? 0 : waveRows.Max(w => w.NTrials);

        return new
        {
            generatedAt = DateTimeOffset.UtcNow.ToString("o"),
            variants,
            v0Id = VariantRegistry.V0Id,
            waves,
            nTrials,
            bhQ = WaveRadar.BhQ,
        };
    }

    private static string Render(object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        });
        // Keep HTML/JS braces literal; inject JSON via placeholder.
        return Template.Replace("__DATA_JSON__", json, StringComparison.Ordinal);
    }

    private const string Template = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>PossiblyMaybe — Variants</title>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
<style>
  :root { --bg:#10151c; --panel:#1a222d; --ink:#e7ecf3; --muted:#8b9bb4; --line:#2a3544; --up:#3ecf8e; --down:#f07178; --accent:#6cb6ff; }
  * { box-sizing: border-box; }
  body { margin:0; font-family:"IBM Plex Sans","Segoe UI",sans-serif; background:radial-gradient(900px 500px at 80% -20%,#1a2838,var(--bg)); color:var(--ink); }
  header { padding:1.4rem 2rem 0.4rem; }
  header h1 { margin:0; font-size:1.35rem; }
  header p { margin:0.35rem 0 0; color:var(--muted); font-size:0.92rem; }
  main { padding:1rem 2rem 3rem; display:grid; gap:1.25rem; }
  table { width:100%; border-collapse:collapse; font-size:0.9rem; background:var(--panel); border:1px solid var(--line); border-radius:10px; overflow:hidden; }
  th,td { text-align:left; padding:0.55rem 0.7rem; border-bottom:1px solid var(--line); font-variant-numeric:tabular-nums; }
  th { color:var(--muted); font-weight:500; font-size:0.72rem; text-transform:uppercase; letter-spacing:0.06em; }
  .pos { color:var(--up); } .neg { color:var(--down); }
  .chart-wrap { background:var(--panel); border:1px solid var(--line); border-radius:10px; padding:1rem; }
  .muted { color:var(--muted); font-size:0.85rem; }
  select { background:var(--panel); color:var(--ink); border:1px solid var(--line); border-radius:6px; padding:0.35rem 0.6rem; font:inherit; }
</style>
</head>
<body>
<header>
  <h1>Shadow variants</h1>
  <p id="sub">Loading…</p>
</header>
<main>
  <div>
    <label class="muted">League filter </label>
    <select id="league"><option value="ALL">All</option></select>
  </div>
  <table>
    <thead>
      <tr>
        <th>Variant</th><th>n</th><th>Hit</th><th>Flat u</th><th>Δu vs V0</th>
        <th>Mean CLV</th><th>ΔCLV vs V0</th><th>Top skips</th>
      </tr>
    </thead>
    <tbody id="rows"></tbody>
  </table>
  <div class="chart-wrap">
    <canvas id="units" height="120"></canvas>
  </div>
  <div class="chart-wrap">
    <canvas id="clv" height="120"></canvas>
  </div>
  <section>
    <h2 style="margin:0 0 0.4rem;font-size:1.05rem;">Waves</h2>
    <p class="muted" id="waveSub">No radar scan yet.</p>
    <table>
      <thead>
        <tr>
          <th>League</th><th>Mkt</th><th>Variant</th><th>n</th><th>CLV ± SE</th>
          <th>Rolling</th><th>CUSUM</th><th>p</th><th>BH</th><th>Wave</th>
          <th>Ladder</th><th>Checkpoint</th><th>Verdict</th><th>Wks</th><th>N_trials</th>
        </tr>
      </thead>
      <tbody id="waveRows"></tbody>
    </table>
  </section>
</main>
<script>
const DATA = __DATA_JSON__;
const sub = document.getElementById('sub');
sub.textContent = `generated ${DATA.generatedAt} · ${DATA.variants.length} variants`;
const leagueSel = document.getElementById('league');
const leagues = new Set(['ALL']);
for (const v of DATA.variants) for (const L of v.leagues) leagues.add(L.sport);
[...leagues].sort().forEach(s => {
  if (s === 'ALL') return;
  const o = document.createElement('option'); o.value = s; o.textContent = s; leagueSel.appendChild(o);
});

function stats(v, league) {
  if (league === 'ALL') return { n:v.n, hitRate:v.hitRate, flatUnits:v.flatUnits, meanClv:v.meanClv, skips:v.skips, series:null };
  const L = v.leagues.find(x => x.sport === league);
  if (!L) return { n:0, hitRate:0, flatUnits:0, meanClv:null, skips:{}, series:[] };
  return { n:L.n, hitRate:L.hitRate, flatUnits:L.flatUnits, meanClv:L.meanClv, skips:L.skips, series:L.series };
}

function fmt(n, d=1) { return (n==null||Number.isNaN(n)) ? '—' : n.toFixed(d); }
function cls(n) { return n > 0 ? 'pos' : n < 0 ? 'neg' : ''; }

(function renderWaves() {
  const waves = DATA.waves || [];
  const waveSub = document.getElementById('waveSub');
  const nTrials = DATA.nTrials ?? 0;
  const bhQ = DATA.bhQ ?? 0.1;
  if (!waves.length) {
    waveSub.textContent = `No radar scan yet · N_trials=0 · BH q=${bhQ}`;
    return;
  }
  const on = waves.filter(w => w.wave === 'On').length;
  const bh = waves.filter(w => w.bhSignificant).length;
  const wide = waves.filter(w => w.allowsWideStake).length;
  waveSub.textContent = `ON=${on} · BH_sig=${bh} · wide_stake=${wide} · N_trials=${nTrials} · BH q=${bhQ} · ckpt 50/100–200/500/1500`;
  const tb = document.getElementById('waveRows');
  tb.innerHTML = '';
  for (const w of waves) {
    const tr = document.createElement('tr');
    tr.innerHTML = `<td>${w.league}</td><td>${w.market}</td><td><strong>${w.variantId}</strong></td>
      <td>${w.n}</td>
      <td class="${cls(w.meanClv)}">${fmt(w.meanClv,2)}±${fmt(w.clvSe,2)}</td>
      <td class="${cls(w.rollingClv)}">${fmt(w.rollingClv,2)}</td>
      <td>${fmt(w.cusum,1)}</td>
      <td>${fmt(w.pValue,4)}</td>
      <td>${w.bhSignificant ? 'Y' : '—'}</td>
      <td class="${w.wave==='On'?'pos':'muted'}">${w.wave}</td>
      <td>${w.stage}</td>
      <td>${w.checkpoint}</td>
      <td>${w.checkpointVerdict}</td>
      <td>${w.weeksOn}</td>
      <td>${w.nTrials}</td>`;
    tb.appendChild(tr);
  }
})();

let unitsChart, clvChart;
function render() {
  const league = leagueSel.value;
  const v0 = DATA.variants.find(v => v.id === DATA.v0Id);
  const s0 = stats(v0, league);
  const tbody = document.getElementById('rows');
  tbody.innerHTML = '';
  const unitSeries = [];
  const clvSeries = [];
  for (const v of DATA.variants) {
    const s = stats(v, league);
    const du = s.flatUnits - s0.flatUnits;
    const dc = (s.meanClv??0) - (s0.meanClv??0);
    const topSkips = Object.entries(s.skips||{}).sort((a,b)=>b[1]-a[1]).slice(0,3)
      .map(([k,c]) => `${k}:${c}`).join(' ') || '—';
    const tr = document.createElement('tr');
    tr.innerHTML = `<td><strong>${v.id}</strong> <span class="muted">${v.label}</span></td>
      <td>${s.n}</td><td>${fmt(s.hitRate*100,0)}%</td>
      <td class="${cls(s.flatUnits)}">${fmt(s.flatUnits)}</td>
      <td class="${cls(du)}">${v.id===DATA.v0Id?'—':fmt(du)}</td>
      <td class="${cls(s.meanClv||0)}">${fmt(s.meanClv,2)}</td>
      <td class="${cls(dc)}">${v.id===DATA.v0Id?'—':fmt(dc,2)}</td>
      <td class="muted">${topSkips}</td>`;
    tbody.appendChild(tr);
    const ser = league === 'ALL' ? null : s.series;
    if (ser && ser.length) {
      unitSeries.push({ label: v.id, data: ser.map(p => ({x:p.t, y:p.units})) });
      clvSeries.push({ label: v.id, data: ser.map(p => ({x:p.t, y:p.clv})) });
    }
  }
  try {
    if (unitSeries.length) {
      const flatU = unitSeries.map((s,i) => ({ label:s.label, data:s.data.map((p,j)=>({x:j,y:p.y})), borderColor:`hsl(${(i*47)%360} 70% 60%)`, tension:0.15, pointRadius:0 }));
      const flatC = clvSeries.map((s,i) => ({ label:s.label, data:s.data.map((p,j)=>({x:j,y:p.y})), borderColor:`hsl(${(i*47)%360} 70% 60%)`, tension:0.15, pointRadius:0 }));
      if (unitsChart) unitsChart.destroy();
      unitsChart = new Chart(document.getElementById('units'), { type:'line', data:{ datasets: flatU },
        options:{ plugins:{ title:{display:true,text:'Cumulative flat units (league filter)',color:'#8b9bb4'}, legend:{labels:{color:'#e7ecf3'}} },
          scales:{ x:{ ticks:{color:'#8b9bb4'} }, y:{ ticks:{color:'#8b9bb4'} } } } });
      if (clvChart) clvChart.destroy();
      clvChart = new Chart(document.getElementById('clv'), { type:'line', data:{ datasets: flatC },
        options:{ plugins:{ title:{display:true,text:'Rolling mean CLV pts',color:'#8b9bb4'}, legend:{labels:{color:'#e7ecf3'}} },
          scales:{ x:{ ticks:{color:'#8b9bb4'} }, y:{ ticks:{color:'#8b9bb4'} } } } });
    }
  } catch (e) { console.warn(e); }
}
leagueSel.onchange = render;
render();
</script>
</body>
</html>
""";
}
