using GoomeSharp;

namespace Launchpad.Ui;

public static class Backdrop
{
    public static Bitmap Blurred(Bitmap source)
    {
        const int width = 40;
        const int height = 14;
        var pixels = new uint[width * height];
        var cropWidth = Math.Min(source.Width, source.Height * width / height);
        var cropHeight = Math.Min(source.Height, source.Width * height / width);
        var left = (source.Width - cropWidth) / 2;
        var top = (source.Height - cropHeight) / 2;
        var data = source.Pixels;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = Average(data, source.Stride, left + x * cropWidth / width, top + y * cropHeight / height, Math.Max(1, cropWidth / width), Math.Max(1, cropHeight / height));
        }

        return Bitmap.Wrap(pixels, width, height);
    }

    public static Bitmap Cover(Bitmap source, double aspect)
    {
        var cropWidth = Math.Min(source.Width, (int)(source.Height * aspect));
        var cropHeight = Math.Min(source.Height, (int)(source.Width / aspect));
        var left = (source.Width - cropWidth) / 2;
        var top = (source.Height - cropHeight) / 2;
        var pixels = new uint[cropWidth * cropHeight];
        var data = source.Pixels;

        for (var y = 0; y < cropHeight; y++)
            data.Slice((top + y) * source.Stride + left, cropWidth).CopyTo(pixels.AsSpan(y * cropWidth, cropWidth));

        return Bitmap.Wrap(pixels, cropWidth, cropHeight);
    }

    private static uint Average(Span<uint> data, int stride, int x, int y, int w, int h)
    {
        long a = 0, r = 0, g = 0, b = 0;
        var count = 0;
        var stepX = Math.Max(1, w / 4);
        var stepY = Math.Max(1, h / 4);

        for (var yy = y; yy < y + h; yy += stepY)
        {
            for (var xx = x; xx < x + w; xx += stepX)
            {
                var p = data[yy * stride + xx];
                a += p >> 24;
                r += (p >> 16) & 255;
                g += (p >> 8) & 255;
                b += p & 255;
                count++;
            }
        }

        return (uint)(a / count << 24 | r / count << 16 | g / count << 8 | b / count);
    }
}
