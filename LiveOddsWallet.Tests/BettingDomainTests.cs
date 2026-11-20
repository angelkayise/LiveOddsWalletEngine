using LiveOddsWallet.Api.Models;

namespace LiveOddsWallet.Tests;

public class BettingDomainTests
{
    [Theory]
    [InlineData(500.00, 1.85, 925.00)]
    [InlineData(250.00, 2.40, 600.00)]
    [InlineData(100.00, 18.00, 1800.00)]
    public void PotentialPayout_CalculatesAccurateReturn(decimal stake, decimal odds, decimal expectedPayout)
    {
        var payout = Math.Round(stake * odds, 2);

        Assert.Equal(expectedPayout, payout);
    }

    [Fact]
    public void OddsFloor_NeverDropsBelowMinimumThreshold()
    {
        decimal currentOdds = 1.07m;
        decimal negativeDelta = -0.05m;

        decimal clampedOdds = Math.Max(1.05m, currentOdds + negativeDelta);

        Assert.Equal(1.05m, clampedOdds);
    }

    [Fact]
    public void PlaceBetResponse_StoresLockedOddsAndRemainingBalance()
    {
        var response = new PlaceBetResponse("corr-123", "ACCEPTED", 1.85m, 925.00m, 9500.00m);

        Assert.Equal("ACCEPTED", response.Status);
        Assert.Equal(1.85m, response.LockedOdds);
        Assert.Equal(9500.00m, response.RemainingBalance);
    }
}
