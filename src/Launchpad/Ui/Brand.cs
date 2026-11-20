using GoomeSharp;

namespace Launchpad.Ui;

public static class Brand
{
    public static Bitmap Logo { get; } = Bitmap.Load(ReadLogo());

    private static byte[] ReadLogo()
    {
        using Stream stream = typeof(Brand).Assembly.GetManifestResourceStream("logo.png")!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
