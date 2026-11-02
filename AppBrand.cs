using System.Reflection;

namespace TVController;

internal static class AppBrand
{
    internal static Icon LoadIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CaaM.AppIcon") ?? throw new InvalidOperationException("App icon missing.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    internal static Image LoadImage()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CaaM.AppLogo") ?? throw new InvalidOperationException("App logo missing.");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    internal static string DataUrl
    {
        get
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CaaM.AppLogo") ?? throw new InvalidOperationException("App logo missing.");
            using var data = new MemoryStream(); stream.CopyTo(data);
            return $"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
        }
    }
}
