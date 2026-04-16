using NrgOverlay.Sim.Contracts;

namespace NrgOverlay.Sim.iRacing.Tests;

public class CountryCodeResolverTests
{
    [Fact]
    public void ResolveCountryCode_PrefersOverride_ThenCache_ThenFlair()
    {
        var overrides = new Dictionary<int, string> { [42] = "DE" };
        var cache = new Dictionary<int, string> { [42] = "US", [7] = "FR" };
        var byFlairIso2 = new Dictionary<int, string> { [99] = "IT" };
        var byFlairIso3 = new Dictionary<int, string> { [100] = "WLS" };

        Assert.Equal("DE", CountryCodeResolver.ResolveCountryCode(42, 0, overrides, cache, byFlairIso2, byFlairIso3));
        Assert.Equal("FR", CountryCodeResolver.ResolveCountryCode(7, 0, overrides, cache, byFlairIso2, byFlairIso3));
        Assert.Equal("IT", CountryCodeResolver.ResolveCountryCode(99, 99, overrides, cache, byFlairIso2, byFlairIso3));
        Assert.Equal("GB", CountryCodeResolver.ResolveCountryCode(100, 100, overrides, cache, byFlairIso2, byFlairIso3));
        Assert.Equal(string.Empty, CountryCodeResolver.ResolveCountryCode(101, 0, overrides, cache, byFlairIso2, byFlairIso3));
    }

    [Fact]
    public void ToFlagOrFallback_UsesFlagWhenIsoIsValid()
    {
        Assert.Equal("\uD83C\uDDE9\uD83C\uDDEA", CountryCodeResolver.ToFlagOrFallback("de", ""));
    }

    [Fact]
    public void ToFlagOrFallback_UsesIso3_WhenNoEmojiAvailable()
    {
        Assert.Equal("WLS", CountryCodeResolver.ToFlagOrFallback("WLS", ""));
        Assert.Equal("??", CountryCodeResolver.ToFlagOrFallback("", ""));
    }
}

