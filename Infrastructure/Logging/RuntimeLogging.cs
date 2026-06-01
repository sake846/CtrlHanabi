using System.IO;

namespace CtrlHanabi;

internal static class RuntimeLogging
{
    private static readonly string D3D11LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CtrlHanabi",
        "d3d11.log");

    public static bool IsD3D11LogEnabled()
    {
        var global = Environment.GetEnvironmentVariable("CTRLHANABI_LOG");
        if (string.Equals(global, "0", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(global, "1", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(Environment.GetEnvironmentVariable("CTRLHANABI_D3D11_LOG"), "1", StringComparison.Ordinal);
    }

    public static string GetD3D11LogPath() => D3D11LogPath;

    public static void AppendD3D11Log(string message)
    {
        if (!IsD3D11LogEnabled())
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(D3D11LogPath)!);
            File.AppendAllText(D3D11LogPath, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
