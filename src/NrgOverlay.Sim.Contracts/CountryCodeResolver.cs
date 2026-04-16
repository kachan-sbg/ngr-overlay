namespace NrgOverlay.Sim.Contracts;

/// <summary>
/// Resolves and formats driver country codes for overlays.
/// Resolution order: manual override в†’ external cache в†’ Flair mapping в†’ unknown.
/// </summary>
public static class CountryCodeResolver
{
    private static readonly IReadOnlyDictionary<string, string> Iso2ByIso3 = BuildIso2ByIso3();

    /// <summary>
    /// Resolves an ISO alpha-2 country code for a driver.
    /// </summary>
    public static string ResolveCountryCode(
        int userId,
        int flairId,
        IReadOnlyDictionary<int, string>? overridesByUserId,
        IReadOnlyDictionary<int, string>? cachedByUserId,
        IReadOnlyDictionary<int, string>? byFlairIso2,
        IReadOnlyDictionary<int, string>? byFlairIso3)
    {
        if (TryGetAnyCode(overridesByUserId, userId, out var overrideCode))
            return PreferIso2Code(overrideCode);

        if (TryGetAnyCode(cachedByUserId, userId, out var cachedCode))
            return PreferIso2Code(cachedCode);

        if (TryGetIso2Code(byFlairIso2, flairId, out var iso2))
            return iso2;

        if (TryGetIso3Code(byFlairIso3, flairId, out var iso3))
            return PreferIso2Code(iso3);

        return string.Empty;
    }

    /// <summary>
    /// Converts a country code to a flag emoji when ISO2 is available.
    /// Falls back to ISO3 text when present.
    /// </summary>
    public static string ToFlagOrFallback(string? countryCode, string? fallbackIso3)
    {
        var iso2 = NormalizeIso2Code(countryCode);
        if (iso2.Length == 2)
        {
            return char.ConvertFromUtf32(0x1F1E6 + (iso2[0] - 'A'))
                 + char.ConvertFromUtf32(0x1F1E6 + (iso2[1] - 'A'));
        }

        var iso3 = NormalizeIso3Code(countryCode);
        if (iso3.Length == 3) return iso3;

        iso3 = NormalizeIso3Code(fallbackIso3);
        if (iso3.Length == 3) return iso3;

        return "??";
    }

    /// <summary>
    /// Normalizes a potential ISO alpha-2 country code to uppercase or empty.
    /// </summary>
    public static string NormalizeIso2Code(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var trimmed = code.Trim().ToUpperInvariant();
        return trimmed.Length == 2 && char.IsAsciiLetter(trimmed[0]) && char.IsAsciiLetter(trimmed[1])
            ? trimmed
            : string.Empty;
    }

    /// <summary>
    /// Normalizes a potential ISO alpha-3 country/region code to uppercase or empty.
    /// </summary>
    public static string NormalizeIso3Code(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var trimmed = code.Trim().ToUpperInvariant();
        return trimmed.Length == 3
            && char.IsAsciiLetter(trimmed[0])
            && char.IsAsciiLetter(trimmed[1])
            && char.IsAsciiLetter(trimmed[2])
            ? trimmed
            : string.Empty;
    }

    public static string NormalizeIsoCode(string? code)
    {
        var iso2 = NormalizeIso2Code(code);
        if (iso2.Length == 2) return iso2;
        return NormalizeIso3Code(code);
    }

    private static bool TryGetIso2Code(
        IReadOnlyDictionary<int, string>? map,
        int key,
        out string code)
    {
        code = string.Empty;
        if (map is null || key <= 0) return false;
        if (!map.TryGetValue(key, out var raw)) return false;

        code = NormalizeIso2Code(raw);
        return code.Length == 2;
    }

    private static bool TryGetIso3Code(
        IReadOnlyDictionary<int, string>? map,
        int key,
        out string code)
    {
        code = string.Empty;
        if (map is null || key <= 0) return false;
        if (!map.TryGetValue(key, out var raw)) return false;

        code = NormalizeIso3Code(raw);
        return code.Length == 3;
    }

    private static bool TryGetAnyCode(
        IReadOnlyDictionary<int, string>? map,
        int key,
        out string code)
    {
        code = string.Empty;
        if (map is null || key <= 0) return false;
        if (!map.TryGetValue(key, out var raw)) return false;

        code = NormalizeIsoCode(raw);
        return code.Length is 2 or 3;
    }

    private static string PreferIso2Code(string code)
    {
        var iso2 = NormalizeIso2Code(code);
        if (iso2.Length == 2)
            return iso2;

        var iso3 = NormalizeIso3Code(code);
        if (iso3.Length != 3)
            return string.Empty;

        return Iso2ByIso3.TryGetValue(iso3, out var mappedIso2)
            ? mappedIso2
            : iso3;
    }

    private static IReadOnlyDictionary<string, string> BuildIso2ByIso3()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // UK home nations (iRacing flair IDs use these pseudo ISO3 values).
            ["ENG"] = "GB",
            ["SCT"] = "GB",
            ["WLS"] = "GB",
            ["NIR"] = "GB",
        };

        foreach (var kv in DriverCountryDefaults.Iso3ByFlairId)
        {
            var iso3 = NormalizeIso3Code(kv.Value);
            if (iso3.Length != 3)
                continue;

            if (!DriverCountryDefaults.Iso2ByFlairId.TryGetValue(kv.Key, out var rawIso2))
                continue;

            var iso2 = NormalizeIso2Code(rawIso2);
            if (iso2.Length == 2)
                map[iso3] = iso2;
        }

        return map;
    }
}

