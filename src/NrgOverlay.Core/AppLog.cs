namespace NrgOverlay.Core;

/// <summary>
/// Minimal file-backed logger. Keeps the file handle open for the process lifetime
/// so every write is a buffered (auto-flushed) append rather than an open/close cycle.
/// Log location: %APPDATA%\NrgOverlay\sim-overlay.log
/// Rotates at 5 MB (keeps one .bak). Call <see cref="Close"/> at shutdown or let
/// the <c>ProcessExit</c> handler do it automatically.
/// </summary>
public static class AppLog
{
    private static readonly string LogPath;
    private static readonly object WriteLock = new();
    private static StreamWriter?   _writer;

    private const long RotateSizeBytes = 5L * 1024 * 1024;

    static AppLog()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NrgOverlay");

        try { Directory.CreateDirectory(dir); } catch { /* best effort */ }

        LogPath = Path.Combine(dir, "sim-overlay.log");

        // Rotate any oversized file before opening the writer.
        RotateFile();
        OpenWriter();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Close();
        Info("=== NrgOverlay log opened ===");
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public static void Info(string message)  => Write("INFO ", message);
    public static void Warn(string message)  => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);

    /// <summary>Logs the exception type, message, and full stack trace.</summary>
    public static void Exception(string context, Exception ex)
        => Error($"{context}: [{ex.GetType().Name}] {ex.Message}{Environment.NewLine}{ex.StackTrace}");

    /// <summary>
    /// Flushes and closes the log file. Called automatically on process exit.
    /// Safe to call multiple times.
    /// </summary>
    public static void Close()
    {
        lock (WriteLock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
        lock (WriteLock)
        {
            try
            {
                // Rotate if the writer's stream position has passed the 5 MB threshold.
                if (_writer?.BaseStream.Position >= RotateSizeBytes)
                {
                    _writer.Dispose();
                    _writer = null;
                    RotateFile();
                    OpenWriter();
                }

                _writer?.Write(line);
            }
            catch { /* can't log the logger вЂ” give up silently */ }
        }
    }

    private static void OpenWriter()
    {
        try { _writer = new StreamWriter(LogPath, append: true) { AutoFlush = true }; }
        catch { /* best effort */ }
    }

    private static void RotateFile()
    {
        try
        {
            if (!File.Exists(LogPath)) return;
            if (new FileInfo(LogPath).Length < RotateSizeBytes) return;

            var bak = LogPath + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
            File.Move(LogPath, bak);
        }
        catch { /* rotation failure is non-fatal */ }
    }
}

