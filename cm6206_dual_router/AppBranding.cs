using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace Cm6206DualRouter;

internal static class AppBranding
{
    public static string IconIcoPath => Path.Combine(AppContext.BaseDirectory, "icon.ico");
    public static string IconPngPath => Path.Combine(AppContext.BaseDirectory, "icon.png");

    public static Image? TryLoadAppImage()
    {
        try
        {
            var path = IconPngPath;
            if (!File.Exists(path))
                return null;

            using var fs = File.OpenRead(path);
            using var img = Image.FromStream(fs);
            return new Bitmap(img);
        }
        catch
        {
            return null;
        }
    }

    public static Icon? TryLoadAppIcon()
    {
        try
        {
            var icoPath = IconIcoPath;
            if (File.Exists(icoPath))
                return new Icon(icoPath);
        }
        catch
        {
            // ignore and fall back
        }

        IntPtr hIcon = IntPtr.Zero;

        try
        {
            var path = IconPngPath;
            if (!File.Exists(path))
                return null;

            using var bmp = new Bitmap(path);
            hIcon = bmp.GetHicon();
            if (hIcon == IntPtr.Zero)
                return null;

            using var icon = Icon.FromHandle(hIcon);
            return (Icon)icon.Clone();
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
                _ = DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
