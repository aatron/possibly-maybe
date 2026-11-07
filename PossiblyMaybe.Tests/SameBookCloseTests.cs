using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class SameBookCloseTests
{
    [Test]
    public async Task Venue_Maps_To_Close_Book()
    {
        await Assert.That(SameBookClose.BookForVenue(PaperVenues.DraftKings)).IsEqualTo(Book.DraftKings);
        await Assert.That(SameBookClose.BookForVenue(PaperVenues.Kalshi)).IsEqualTo(Book.Kalshi);
        await Assert.That(SameBookClose.BookForVenue(PaperVenues.Robinhood)).IsEqualTo(Book.Kalshi);
        await Assert.That(SameBookClose.BookForVenue(PaperVenues.CryptoCom)).IsEqualTo(Book.CryptoCom);
        await Assert.That(SameBookClose.TrackerVenueForBook(Book.DraftKings)).IsEqualTo("espn");
    }

    [Test]
    public async Task Robinhood_CloseAmerican_Applies_Fee()
    {
        var raw = SameBookClose.AdjustCloseAmerican(100, PaperVenues.Kalshi);
        var rh = SameBookClose.AdjustCloseAmerican(100, PaperVenues.Robinhood);
        await Assert.That(raw).IsEqualTo(100);
        await Assert.That(rh).IsNotEqualTo(100);
    }

    [Test]
    public async Task IsFresh_Fifteen_Minute_Window()
    {
        await Assert.That(SameBookClose.IsFresh(15)).IsTrue();
        await Assert.That(SameBookClose.IsFresh(15.1)).IsFalse();
        await Assert.That(SameBookClose.IsFresh(0)).IsFalse();
        await Assert.That(SameBookClose.IsFresh(null)).IsFalse();
    }
}
