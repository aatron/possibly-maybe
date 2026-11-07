using System.Text.Json;
using System.Text.Json.Serialization;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Durable A5/T5 seasonality BH table beside variants.db.
/// Radar loads BH-significant (sport × phase) keys as a CUSUM prior only.
/// </summary>
public static class SeasonalityCalendarStore
{
    public static string DefaultPath(string repoRoot)
        => Path.Combine(repoRoot, "data", "seasonality_bh.json");

    public static string PathBesideVariants(string variantsDbPath)
        => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(variantsDbPath))!, "seasonality_bh.json");

    public static async Task SaveAsync(
        string path, IReadOnlyList<SeasonalityCell> cells, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var nTrials = cells.Count == 0 ? 0 : cells[0].NTrials;
        var dto = new SeasonalityCalendarDto
        {
            ScannedAt = DateTimeOffset.UtcNow,
            BhQ = SeasonalityScan.BhQ,
            NTrials = nTrials,
            SignalVariantId = SeasonalityScan.SignalVariantId,
            Cells = cells.Select(c => new SeasonalityCellDto
            {
                Sport = c.Sport,
                PhaseId = c.PhaseId,
                N = c.N,
                MeanClv = c.MeanClv,
                PValue = c.PValue,
                BhAdjustedP = c.BhAdjustedP,
                BhSignificant = c.BhSignificant,
            }).ToList(),
            SignificantKeys = cells
                .Where(c => c.BhSignificant)
                .Select(c => WaveRadar.SeasonPhaseKey(c.Sport, c.PhaseId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
        var json = JsonSerializer.Serialize(dto, JsonOpts);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public static async Task<SeasonalityCalendarDto?> LoadAsync(
        string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            return null;
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SeasonalityCalendarDto>(fs, JsonOpts, ct);
    }

    /// <summary>BH-significant phase keys for <see cref="WaveRadar.Scan"/>; empty if unscanned / none.</summary>
    public static async Task<IReadOnlySet<string>> LoadSignificantKeysAsync(
        string path, CancellationToken ct = default)
    {
        var dto = await LoadAsync(path, ct);
        if (dto is null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (dto.SignificantKeys is { Count: > 0 })
            return new HashSet<string>(dto.SignificantKeys, StringComparer.OrdinalIgnoreCase);
        var fromCells = (dto.Cells ?? [])
            .Where(c => c.BhSignificant)
            .Select(c => WaveRadar.SeasonPhaseKey(c.Sport, c.PhaseId));
        return new HashSet<string>(fromCells, StringComparer.OrdinalIgnoreCase);
    }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class SeasonalityCalendarDto
{
    public DateTimeOffset ScannedAt { get; set; }
    public double BhQ { get; set; }
    public int NTrials { get; set; }
    public string SignalVariantId { get; set; } = "";
    public List<SeasonalityCellDto> Cells { get; set; } = [];
    public List<string> SignificantKeys { get; set; } = [];
}

public sealed class SeasonalityCellDto
{
    public string Sport { get; set; } = "";
    public string PhaseId { get; set; } = "";
    public int N { get; set; }
    public double MeanClv { get; set; }
    public double PValue { get; set; }
    public double BhAdjustedP { get; set; }
    public bool BhSignificant { get; set; }
}
