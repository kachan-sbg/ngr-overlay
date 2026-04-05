/// <summary>
/// Generates src/SimOverlay.App/Resources/simoverlay.ico with four sizes:
/// 16×16, 32×32, 48×48, and 256×256 (PNG-compressed inside ICO).
///
/// Run from the repo root:
///   dotnet run --project tools/GenerateIcon
///
/// The csproj pre-build target runs this automatically when the .ico is missing.
/// </summary>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// When invoked from MSBuild Exec, the output path is passed as the first argument.
// When run manually (dotnet run --project tools/GenerateIcon), default to a path
// relative to the current working directory (expected: repo root).
var outPath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(
          Directory.GetCurrentDirectory(),
          @"src\SimOverlay.App\Resources\simoverlay.ico"));

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

Console.WriteLine($"Generating icon → {outPath}");

int[] sizes = [256, 48, 32, 16];
var pngs = new List<byte[]>();

foreach (var sz in sizes)
    pngs.Add(RenderPng(sz));

WriteIco(outPath, sizes, pngs);
Console.WriteLine("Done.");

// ── Rendering ─────────────────────────────────────────────────────────────────

static byte[] RenderPng(int size)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g   = Graphics.FromImage(bmp);

    g.SmoothingMode     = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode   = PixelOffsetMode.HighQuality;

    g.Clear(Color.Transparent);

    float pad    = size * 0.04f;
    float corner = size * 0.22f;
    var   rect   = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);

    // Background: dark navy with rounded corners
    using var bgBrush = new SolidBrush(Color.FromArgb(255, 13, 17, 36));
    FillRoundedRect(g, bgBrush, rect, corner);

    // Accent stripe: thin horizontal line across middle-lower area
    float stripeH = size * 0.08f;
    float stripeY = size * 0.62f;
    using var stripeBrush = new LinearGradientBrush(
        new RectangleF(rect.Left, stripeY, rect.Width, stripeH),
        Color.FromArgb(200, 80, 160, 255),
        Color.FromArgb(0,   80, 160, 255),
        LinearGradientMode.Horizontal);
    FillRoundedRect(g, stripeBrush, new RectangleF(rect.Left, stripeY, rect.Width, stripeH), 0);

    // "S" letterform
    float fontSize = size * 0.54f;
    using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
    var sf = new StringFormat
    {
        Alignment     = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
    };
    using var textBrush = new SolidBrush(Color.FromArgb(255, 230, 240, 255));
    g.DrawString("S", font, textBrush, new RectangleF(0, 0, size, size * 0.92f), sf);

    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

static void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, float radius)
{
    if (radius <= 0)
    {
        g.FillRectangle(brush, rect);
        return;
    }
    using var path = new GraphicsPath();
    float d = radius * 2;
    path.AddArc(rect.Left,              rect.Top,             d, d, 180, 90);
    path.AddArc(rect.Right - d,         rect.Top,             d, d, 270, 90);
    path.AddArc(rect.Right - d,         rect.Bottom - d,      d, d,   0, 90);
    path.AddArc(rect.Left,              rect.Bottom - d,      d, d,  90, 90);
    path.CloseFigure();
    g.FillPath(brush, path);
}

// ── ICO writer ────────────────────────────────────────────────────────────────
// ICO format: ICONDIR + N×ICONDIRENTRY + N×image-data (PNG inside for ≥32px).

static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
{
    using var fs  = File.Create(path);
    using var bw  = new BinaryWriter(fs);

    int count = sizes.Length;

    // ICONDIR
    bw.Write((ushort)0);     // reserved
    bw.Write((ushort)1);     // type = ICO
    bw.Write((ushort)count); // image count

    // Calculate offsets: header(6) + entries(16*count) + image data
    int dataOffset = 6 + 16 * count;
    var offsets = new int[count];
    for (int i = 0; i < count; i++)
    {
        offsets[i] = dataOffset;
        dataOffset += pngs[i].Length;
    }

    // ICONDIRENTRY × count
    for (int i = 0; i < count; i++)
    {
        int sz = sizes[i];
        bw.Write((byte)(sz == 256 ? 0 : sz)); // width  (0 means 256)
        bw.Write((byte)(sz == 256 ? 0 : sz)); // height
        bw.Write((byte)0);                    // color count (0 = no palette)
        bw.Write((byte)0);                    // reserved
        bw.Write((ushort)1);                  // planes
        bw.Write((ushort)32);                 // bits per pixel
        bw.Write((uint)pngs[i].Length);       // bytes in image
        bw.Write((uint)offsets[i]);           // offset from file start
    }

    // Image data (PNG chunks)
    foreach (var png in pngs)
        bw.Write(png);
}
