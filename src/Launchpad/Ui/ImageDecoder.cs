using System.Runtime.InteropServices;
using GoomeSharp;
using SkiaSharp;

namespace Launchpad.Ui;

public static class ImageDecoder
{
    public static Bitmap? Decode(byte[] bytes)
    {
        using SKCodec? codec = SKCodec.Create(new SKMemoryStream(bytes));

        if (codec == null)
            return null;

        using var source = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));

        if (codec.GetPixels(source.Info, source.GetPixels()) != SKCodecResult.Success)
            return null;

        var pixels = new uint[source.Width * source.Height];
        Marshal.Copy(source.GetPixels(), (int[])(object)pixels, 0, pixels.Length);
        return Bitmap.FromArgb(pixels, source.Width, source.Height);
    }
}
