namespace SimOverlay.Core;

/// <summary>
/// Minimal file-backed logger. Writes synchronously so every line is on disk
/// before the next one, making crash-time log tails complete and reliable.
/// Log location: %APPDATA%\SimOverlay\sim-overlay.log
/// Rotates at 5 MB (keeps one .bak).
/// </summary>
public static class AppLog
{
    private static readonly string LogPath;
    private static readonly object WriteLock = new();

    static AppLog()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimOverlay");

        try { Directory.CreateDirectory(dir); } catch { /* best effort */ }

        LogPath = Path.Combine(dir, "sim-overlay.log");
        RotateIfNeeded();
        Info("=== SimOverlay log opened ===");
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public static void Info(string message)      => Write("INFO ", message);
    public static void Warn(string message)      => Write("WARN ", message);
    public static void Error(string message)     => Write("ERROR", message);

    /// <summary>Logs the exception type, message, and full stack trace.</summary>
    public static void Exception(string context, Exception ex)
        => Error($"{context}: [{ex.GetType().Name}] {ex.Message}{Environment.NewLine}{ex.StackTrace}");

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
        lock (WriteLock)
        {
            try   { File.AppendAllText(LogPath, line); }
            catch { /* can't log the logger — give up silently */ }
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(LogPath)) return;
            if (new FileInfo(LogPath).Length < 5 * 1024 * 1024) return;

            var bak = LogPath + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
            File.Move(LogPath, bak);
        }
        catch { /* rotation failure is non-fatal */ }
    }
}
