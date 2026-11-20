using GoomeSharp;

namespace Launchpad.Ui;

public static class Backdrop
{
    public static Bitmap Blurred(Bitmap source)
    {
        const int w = 10, h = 4;
        var pixels = new uint[w * h];
        var cw = Math.Min(source.Width, source.Height * w / h);
        var ch = Math.Min(source.Height, source.Width * h / w);
        var left = (source.Width - cw) / 2;
        var top = (source.Height - ch) / 2;
        var data = source.Pixels;

        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                pixels[y * w + x] = Average(data, source.Stride, left + x * cw / w, top + y * ch / h, Math.Max(1, cw / w), Math.Max(1, ch / h));

        return Bitmap.Wrap(pixels, w, h);
    }

    public static Bitmap Cover(Bitmap source, double aspect)
    {
        var cw = Math.Min(source.Width, (int)(source.Height * aspect));
        var ch = Math.Min(source.Height, (int)(source.Width / aspect));
        var left = (source.Width - cw) / 2;
        var top = (source.Height - ch) / 2;
        var pixels = new uint[cw * ch];
        var data = source.Pixels;

        for (var y = 0; y < ch; y++)
            data.Slice((top + y) * source.Stride + left, cw).CopyTo(pixels.AsSpan(y * cw, cw));

        return Bitmap.Wrap(pixels, cw, ch);
    }

    private static uint Average(Span<uint> data, int stride, int x, int y, int w, int h)
    {
        long a = 0, r = 0, g = 0, b = 0;
        var count = 0;
        var stepX = Math.Max(1, w / 4);
        var stepY = Math.Max(1, h / 4);

        for (var yy = y; yy < y + h; yy += stepY)
            for (var xx = x; xx < x + w; xx += stepX)
            {
                var p = data[yy * stride + xx];
                a += p >> 24;
                r += (p >> 16) & 255;
                g += (p >> 8) & 255;
                b += p & 255;
                count++;
            }

        return (uint)(a / count << 24 | r / count << 16 | g / count << 8 | b / count);
    }
}
