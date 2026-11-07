using PossiblyMaybe.Cli.Paper;

namespace PossiblyMaybe.Tests;

public class KalshiQuotesTests
{
    [Test]
    [Arguments("NBA", "KXNBATOTAL")]
    [Arguments("MLS", "KXMLSTOTAL")]
    [Arguments("LIGUE1", "KXLIGUE1TOTAL")]
    [Arguments("SERIEA", "KXSERIEATOTAL")]
    [Arguments("LIGAMX", "KXLIGAMXTOTAL")]
    [Arguments("EPL", "KXEPLTOTAL")]
    [Arguments("ligue1", "KXLIGUE1TOTAL")]
    public async Task TrySeriesTicker_WiredSports(string sport, string ticker)
        => await Assert.That(KalshiQuotes.TrySeriesTicker(sport)).IsEqualTo(ticker);

    [Test]
    [Arguments("NFL")]
    [Arguments("NCAAB")]
    [Arguments("")]
    public async Task TrySeriesTicker_UnwiredIsNull(string sport)
        => await Assert.That(KalshiQuotes.TrySeriesTicker(sport)).IsNull();
}
