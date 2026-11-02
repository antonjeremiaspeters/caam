using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

const int size = 1024;
var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("caam-logo-concept-v6.png");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);

using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
bitmap.SetResolution(144, 144);
using var g = Graphics.FromImage(bitmap);
g.SmoothingMode = SmoothingMode.AntiAlias;
g.PixelOffsetMode = PixelOffsetMode.HighQuality;
g.CompositingQuality = CompositingQuality.HighQuality;
g.Clear(Color.Transparent);

using var tile = RoundedRectangle(new RectangleF(18, 18, 988, 988), 112);
using var tileBrush = new SolidBrush(Color.FromArgb(255, 15, 19, 27));
g.FillPath(tileBrush, tile);

using var white = new Pen(Color.FromArgb(248, 250, 252), 23)
{
    StartCap = LineCap.Round,
    EndCap = LineCap.Round,
    LineJoin = LineJoin.Miter,
    MiterLimit = 5
};

// One closed contour. The outer shell is mirror-balanced. On the inner-right
// return, a standard up-left mouse pointer replaces part of the contour:
// its rear edge continues into the inner bridge and its lower stem flows into
// the right grip, so no separate symbol is layered over the controller.
using var controller = new GraphicsPath();
controller.StartFigure();
controller.AddBezier(512, 286, 580, 286, 586, 238, 660, 238);
controller.AddBezier(660, 238, 725, 238, 769, 283, 794, 374);
controller.AddBezier(794, 374, 819, 465, 842, 556, 858, 636);
controller.AddBezier(858, 636, 873, 711, 845, 774, 780, 774);
controller.AddBezier(780, 774, 733, 774, 687, 737, 647, 670);

// Cursor lower stem merges into the right grip.
LineTo(controller, 625, 626);
LineTo(controller, 596, 566);
LineTo(controller, 568, 592);
// Real cursor tip and leading edge.
LineTo(controller, 562, 464);
LineTo(controller, 672, 546);
LineTo(controller, 629, 554);

// Cursor rear edge becomes the controller's lower inner bridge.
controller.AddBezier(629, 554, 596, 571, 558, 584, 516, 584);
LineTo(controller, 474, 584);
controller.AddBezier(474, 584, 431, 584, 409, 618, 377, 670);
controller.AddBezier(377, 670, 337, 737, 291, 774, 244, 774);
controller.AddBezier(244, 774, 179, 774, 151, 711, 166, 636);
controller.AddBezier(166, 636, 182, 556, 205, 465, 230, 374);
controller.AddBezier(230, 374, 255, 283, 299, 238, 364, 238);
controller.AddBezier(364, 238, 438, 238, 444, 286, 512, 286);
controller.CloseFigure();
g.DrawPath(white, controller);

// Grid-built D-pad: exact right angles and identical arm lengths.
using var dpad = new GraphicsPath();
dpad.StartFigure();
dpad.AddPolygon(new[]
{
    new Point(356,315), new Point(398,315), new Point(398,374), new Point(457,374),
    new Point(457,416), new Point(398,416), new Point(398,475), new Point(356,475),
    new Point(356,416), new Point(297,416), new Point(297,374), new Point(356,374)
});
dpad.CloseFigure();
using var controlPen = new Pen(Color.FromArgb(248, 250, 252), 15) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
g.DrawPath(controlPen, dpad);

// Four identical hollow face buttons, placed symmetrically around their center.
// The cluster center (647, 395) mirrors the D-pad center (377, 395)
// exactly around the icon's x=512 centerline.
foreach (var p in new[] { new PointF(647, 343), new PointF(699, 395), new PointF(647, 447), new PointF(595, 395) })
    g.DrawEllipse(controlPen, p.X - 22, p.Y - 22, 44, 44);

bitmap.Save(output, ImageFormat.Png);

static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
{
    var path = new GraphicsPath();
    var d = radius * 2;
    path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
    path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
    path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

static void LineTo(GraphicsPath path, float x, float y)
{
    var p = path.GetLastPoint();
    path.AddLine(p.X, p.Y, x, y);
}

static void BezierTo(GraphicsPath path, float c1x, float c1y, float c2x, float c2y, float x, float y)
{
    var p = path.GetLastPoint();
    path.AddBezier(p.X, p.Y, c1x, c1y, c2x, c2y, x, y);
}
