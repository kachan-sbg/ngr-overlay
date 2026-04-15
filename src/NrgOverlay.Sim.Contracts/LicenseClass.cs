namespace NrgOverlay.Sim.Contracts;

public enum LicenseClass
{
    Unknown, // No license data (LMU, non-iRacing sims)
    R,       // Rookie
    D,
    C,
    B,
    A,
    Pro,
    WC,
}

public static class LicenseClassExtensions
{
    /// <summary>
    /// Returns the RGBA color (0.0вЂ“1.0 per channel) for the license class cell background,
    /// matching the iRacing license color scheme defined in OVERLAYS.md.
    /// </summary>
    public static (float R, float G, float B, float A) GetColor(this LicenseClass licenseClass) =>
        licenseClass switch
        {
            LicenseClass.Unknown => (0.400f, 0.400f, 0.400f, 1f), // #666666 grey
            LicenseClass.R       => (1.000f, 0.267f, 0.267f, 1f), // #FF4444
            LicenseClass.D       => (1.000f, 0.533f, 0.000f, 1f), // #FF8800
            LicenseClass.C       => (1.000f, 1.000f, 0.000f, 1f), // #FFFF00
            LicenseClass.B       => (0.000f, 0.733f, 0.000f, 1f), // #00BB00
            LicenseClass.A       => (0.000f, 0.533f, 1.000f, 1f), // #0088FF
            LicenseClass.Pro     => (0.600f, 0.267f, 1.000f, 1f), // #9944FF
            LicenseClass.WC      => (1.000f, 0.267f, 1.000f, 1f), // #FF44FF
            _                    => (1.000f, 1.000f, 1.000f, 1f),
        };

    /// <summary>
    /// Returns true for license classes where text should be rendered in black
    /// (light background cells вЂ” currently only C/yellow).
    /// </summary>
    public static bool RequiresDarkText(this LicenseClass licenseClass) =>
        licenseClass == LicenseClass.C;
}

