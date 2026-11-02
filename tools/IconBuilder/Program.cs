using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

if (args.Length != 2) throw new ArgumentException("Usage: IconBuilder input.png output.ico");
var input = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var sizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
using var source = new Bitmap(input);
var frames = new List<byte[]>();

foreach (var size in sizes)
{
    using var image = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(image))
    {
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, size, size));
    }
    using var data = new MemoryStream();
    image.Save(data, ImageFormat.Png);
    frames.Add(data.ToArray());
}

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
using var file = File.Create(output);
using var writer = new BinaryWriter(file);
writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)frames.Count);
var offset = 6 + 16 * frames.Count;
for (var i = 0; i < frames.Count; i++)
{
    var size = sizes[i]; var frame = frames[i];
    writer.Write((byte)(size == 256 ? 0 : size));
    writer.Write((byte)(size == 256 ? 0 : size));
    writer.Write((byte)0); writer.Write((byte)0);
    writer.Write((ushort)1); writer.Write((ushort)32);
    writer.Write(frame.Length); writer.Write(offset);
    offset += frame.Length;
}
foreach (var frame in frames) writer.Write(frame);
