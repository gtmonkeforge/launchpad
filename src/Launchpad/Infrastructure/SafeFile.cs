namespace Launchpad.Infrastructure;

public static class SafeFile
{
    public static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
