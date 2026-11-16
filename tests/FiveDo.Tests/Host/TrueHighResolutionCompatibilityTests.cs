using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class TrueHighResolutionCompatibilityTests
{
    [Theory]
    [InlineData("18036597b8d903cc68c5a73b16760a47")]
    [InlineData("18036597")]
    [InlineData("55f8ccf1c26d3301bce9fc90967311e2")]
    [InlineData("55F8CCF1")]
    public void ValidatedTitles_AreAccepted(string gameId)
    {
        Assert.True(TrueHighResolutionCompatibility.IsValidated(gameId));
    }

    [Theory]
    [InlineData("d76725503d3ae78eddfa76704ef69926")]
    [InlineData("d7672550")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    public void UnvalidatedTitles_AreRejected(string? gameId)
    {
        Assert.False(TrueHighResolutionCompatibility.IsValidated(gameId));
    }
}
