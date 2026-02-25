using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cm6206AssetGenerator;

internal sealed class PixelCanvas : IDisposable
{
    public Image<Rgba32> Image { get; }
    public int Width => Image.Width;
    public int Height => Image.Height;

    public PixelCanvas(int width, int height, Rgba32 clear)
    {
        Image = new Image<Rgba32>(width, height);
        Clear(clear);
    }

    public void Clear(Rgba32 c)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            Image[x, y] = c;
    }

    public void SetPixel(int x, int y, Rgba32 c)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
        Image[x, y] = c;
    }

    public void FillRect(int x, int y, int w, int h, Rgba32 c)
    {
        var x0 = Math.Max(0, x);
        var y0 = Math.Max(0, y);
        var x1 = Math.Min(Width, x + w);
        var y1 = Math.Min(Height, y + h);
        for (var yy = y0; yy < y1; yy++)
        for (var xx = x0; xx < x1; xx++)
            Image[xx, yy] = c;
    }

    public void SavePng(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        Image.SaveAsPng(path);
    }

    public void Dispose() => Image.Dispose();
}
