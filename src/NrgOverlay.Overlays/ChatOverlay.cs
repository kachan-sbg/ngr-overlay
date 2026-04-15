using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using NrgOverlay.Sim.Contracts;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Timer = System.Windows.Forms.Timer;

namespace NrgOverlay.Overlays;

/// <summary>
/// Prototype browser-chat overlay.
/// Renders a 1 Hz snapshot from a hidden WebView2 instance.
/// </summary>
public sealed class ChatOverlay : BaseOverlay
{
    public const string OverlayId = "Chat";
    public const string WindowTitle = "NrgOverlay - Chat";

    private const string DefaultChatUrl =
        "https://streamelements.com/overlay/69b91c573025400ee1a47d8c/hMkBCbiHKpD0iqD6-BfLIExLv1H9rw7oz1yhU9-E9jE9Yp9b";
    private const string EnvChatUrl = "NRGOVERLAY_CHAT_URL";
    private const int SnapshotIntervalMs = 1000;
    private const int StaleThresholdSec = 5;

    public static OverlayConfig DefaultConfig => new()
    {
        Id = OverlayId,
        Enabled = false,
        X = 1560,
        Y = 110,
        Width = 340,
        Height = 860,
        Opacity = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.65f },
        FontSize = 13f,
    };

    private readonly BrowserSnapshotSource _snapshotSource;
    private readonly object _frameLock = new();

    private byte[]? _latestPixels;
    private int _latestWidth;
    private int _latestHeight;
    private int _latestVersion;
    private DateTime _latestFrameUtc;
    private string? _lastBrowserError;

    private ID2D1Bitmap? _cachedBitmap;
    private int _cachedVersion;
    private int _cachedBitmapWidth;
    private int _cachedBitmapHeight;

    public ChatOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        var chatUrl = Environment.GetEnvironmentVariable(EnvChatUrl);
        if (string.IsNullOrWhiteSpace(chatUrl))
            chatUrl = DefaultChatUrl;

        _snapshotSource = new BrowserSnapshotSource(chatUrl!, SnapshotIntervalMs, config.Width, config.Height);
        _snapshotSource.FrameReady += OnFrameReady;
        _snapshotSource.Error += OnBrowserError;
        _snapshotSource.Start();
    }

    protected override void OnRender(ID2D1RenderTarget context, OverlayConfig config)
    {
        if (!IsLocked)
        {
            RenderMock(context, config);
            return;
        }

        var (pixels, width, height, version, frameUtc, error) = GetFrameState();
        if (pixels is not null && width > 0 && height > 0)
        {
            EnsureBitmap(context, pixels, width, height, version);
        }

        if (_cachedBitmap is not null)
        {
            context.DrawBitmap(_cachedBitmap, config.Opacity, BitmapInterpolationMode.Linear);
        }
        else
        {
            DrawStatusText(context, config, "Chat snapshot warming upвЂ¦");
        }

        if (error is not null)
            DrawStatusText(context, config, $"Chat source error: {error}");
        else if (frameUtc != DateTime.MinValue && (DateTime.UtcNow - frameUtc).TotalSeconds > StaleThresholdSec)
            DrawStatusText(context, config, "Chat snapshot stale вЂ” waiting for refreshвЂ¦");
    }

    protected override void OnDeviceRecreated()
    {
        base.OnDeviceRecreated();
        _cachedBitmap?.Dispose();
        _cachedBitmap = null;
        _cachedVersion = 0;
        _cachedBitmapWidth = 0;
        _cachedBitmapHeight = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _snapshotSource.FrameReady -= OnFrameReady;
            _snapshotSource.Error -= OnBrowserError;
            _snapshotSource.Dispose();

            _cachedBitmap?.Dispose();
            _cachedBitmap = null;
        }
        base.Dispose(disposing);
    }

    private void OnFrameReady(byte[] pixels, int width, int height)
    {
        lock (_frameLock)
        {
            _latestPixels = pixels;
            _latestWidth = width;
            _latestHeight = height;
            _latestVersion++;
            _latestFrameUtc = DateTime.UtcNow;
            _lastBrowserError = null;
        }
    }

    private void OnBrowserError(string message)
    {
        lock (_frameLock)
            _lastBrowserError = message;
        AppLog.Warn($"ChatOverlay browser: {message}");
    }

    private (byte[]? pixels, int width, int height, int version, DateTime frameUtc, string? error) GetFrameState()
    {
        lock (_frameLock)
            return (_latestPixels, _latestWidth, _latestHeight, _latestVersion, _latestFrameUtc, _lastBrowserError);
    }

    private void EnsureBitmap(ID2D1RenderTarget context, byte[] pixels, int width, int height, int version)
    {
        if (_cachedBitmap is not null
            && _cachedVersion == version
            && _cachedBitmapWidth == width
            && _cachedBitmapHeight == height)
        {
            return;
        }

        _cachedBitmap?.Dispose();
        _cachedBitmap = null;

        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            _cachedBitmap = context.CreateBitmap(
                new Vortice.Mathematics.SizeI(width, height),
                handle.AddrOfPinnedObject(),
                width * 4,
                new BitmapProperties(
                    new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied)));
            _cachedVersion = version;
            _cachedBitmapWidth = width;
            _cachedBitmapHeight = height;
        }
        finally
        {
            handle.Free();
        }
    }

    private void DrawStatusText(ID2D1RenderTarget context, OverlayConfig config, string text)
    {
        var fmt = Resources.GetTextFormat("Segoe UI", MathF.Max(config.FontSize - 1f, 11f));
        var brush = Resources.GetBrush(config.TextColor.R, config.TextColor.G, config.TextColor.B, 0.86f);
        using var layout = Resources.WriteFactory.CreateTextLayout(text, fmt, config.Width - 16f, 80f);
        layout.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading;
        layout.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Near;
        context.DrawTextLayout(new System.Numerics.Vector2(8f, config.Height - 52f), layout, brush);
    }

    private void RenderMock(ID2D1RenderTarget context, OverlayConfig config)
    {
        var dw = Resources.WriteFactory;
        var fmt = Resources.GetTextFormat("Oswald", config.FontSize);
        var text = Resources.GetBrush(config.TextColor);
        var dim = Resources.GetBrush(config.TextColor.R, config.TextColor.G, config.TextColor.B, 0.6f);
        float y = 8f;
        float rowH = config.FontSize + 6f;

        DrawLine(context, dw, fmt, dim, "STREAM CHAT (Mock)", 8f, y, config.Width - 16f, rowH);
        y += rowH + 4f;
        DrawLine(context, dw, fmt, text, "[mod] Nice lap pace!", 8f, y, config.Width - 16f, rowH);
        y += rowH;
        DrawLine(context, dw, fmt, text, "[viewer] Fuel to finish?", 8f, y, config.Width - 16f, rowH);
        y += rowH;
        DrawLine(context, dw, fmt, text, "[viewer] Lets goooo", 8f, y, config.Width - 16f, rowH);
        y += rowH;
        DrawLine(context, dw, fmt, dim, "Edit mode: live browser paused", 8f, y, config.Width - 16f, rowH);
    }

    private static void DrawLine(
        ID2D1RenderTarget ctx,
        Vortice.DirectWrite.IDWriteFactory dw,
        Vortice.DirectWrite.IDWriteTextFormat fmt,
        ID2D1Brush brush,
        string text,
        float x,
        float y,
        float w,
        float h)
    {
        using var layout = dw.CreateTextLayout(text, fmt, w, h);
        layout.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading;
        ctx.DrawTextLayout(new System.Numerics.Vector2(x, y), layout, brush, DrawTextOptions.Clip);
    }

    private sealed class BrowserSnapshotSource : IDisposable
    {
        private readonly string _url;
        private readonly int _intervalMs;
        private readonly int _width;
        private readonly int _height;
        private readonly ManualResetEventSlim _started = new(false);
        private readonly CancellationTokenSource _cts = new();
        private Thread? _uiThread;
        private ChatBrowserForm? _form;

        public event Action<byte[], int, int>? FrameReady;
        public event Action<string>? Error;

        public BrowserSnapshotSource(string url, int intervalMs, int width, int height)
        {
            _url = url;
            _intervalMs = intervalMs;
            _width = Math.Max(64, width);
            _height = Math.Max(64, height);
        }

        public void Start()
        {
            _uiThread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "ChatOverlayWebView2",
            };
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            _started.Wait(TimeSpan.FromSeconds(8));
        }

        public void Dispose()
        {
            _cts.Cancel();
            if (_form is { IsHandleCreated: true })
            {
                try
                {
                    _form.BeginInvoke(new Action(() => _form.Close()));
                }
                catch
                {
                    // best effort
                }
            }
            _uiThread?.Join(TimeSpan.FromSeconds(2));
            _started.Dispose();
            _cts.Dispose();
        }

        private void ThreadMain()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            using var form = new ChatBrowserForm(_url, _intervalMs, _width, _height, _cts.Token);
            _form = form;
            form.FrameReady += (pixels, w, h) => FrameReady?.Invoke(pixels, w, h);
            form.Error += msg => Error?.Invoke(msg);
            _started.Set();
            Application.Run(form);
        }
    }

    private sealed class ChatBrowserForm : Form
    {
        private readonly string _url;
        private readonly int _intervalMs;
        private readonly CancellationToken _cancellationToken;
        private readonly WebView2 _webView;
        private readonly Timer _timer;
        private bool _ready;
        private bool _capturing;

        public event Action<byte[], int, int>? FrameReady;
        public event Action<string>? Error;

        public ChatBrowserForm(string url, int intervalMs, int width, int height, CancellationToken cancellationToken)
        {
            _url = url;
            _intervalMs = intervalMs;
            _cancellationToken = cancellationToken;

            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-20000, -20000);
            Size = new Size(width, height);
            Opacity = 0;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
            };
            Controls.Add(_webView);

            _timer = new Timer { Interval = _intervalMs };
            _timer.Tick += TimerTick;

            Shown += async (_, _) => await InitAsync();
            FormClosed += (_, _) =>
            {
                _timer.Stop();
                _timer.Dispose();
                _webView.Dispose();
            };
        }

        private async Task InitAsync()
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                var core = _webView.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultScriptDialogsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.IsBuiltInErrorPageEnabled = true;
                core.IsMuted = true;
                core.Navigate(_url);
                _ready = true;
                _timer.Start();
            }
            catch (Exception ex)
            {
                Error?.Invoke($"Init failed: {ex.Message}");
            }
        }

        private async void TimerTick(object? sender, EventArgs e)
        {
            if (_cancellationToken.IsCancellationRequested) return;
            if (!_ready || _capturing) return;
            if (_webView.CoreWebView2 is null) return;

            _capturing = true;
            try
            {
                using var ms = new MemoryStream();
                await _webView.CoreWebView2.CapturePreviewAsync(
                    CoreWebView2CapturePreviewImageFormat.Png, ms);
                ms.Position = 0;
                using var bitmap = new Bitmap(ms);
                var (pixels, width, height) = ToBgra32(bitmap);
                FrameReady?.Invoke(pixels, width, height);
            }
            catch (Exception ex)
            {
                Error?.Invoke($"Capture failed: {ex.Message}");
            }
            finally
            {
                _capturing = false;
            }
        }

        private static (byte[] Pixels, int Width, int Height) ToBgra32(Bitmap source)
        {
            using var clone = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(clone))
                g.DrawImage(source, 0, 0, source.Width, source.Height);

            var rect = new Rectangle(0, 0, clone.Width, clone.Height);
            var data = clone.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            try
            {
                var byteCount = data.Stride * data.Height;
                var pixels = new byte[byteCount];
                Marshal.Copy(data.Scan0, pixels, 0, byteCount);
                return (pixels, clone.Width, clone.Height);
            }
            finally
            {
                clone.UnlockBits(data);
            }
        }
    }
}



