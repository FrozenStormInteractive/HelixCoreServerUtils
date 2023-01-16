using System.Reflection;
using System.Text;
using System.Text.Json;

namespace HelixCoreServerCtl;

internal static class AppPaths
{
    private class AppPathsJson
    {
        public string ConfigFile { get; set; } = default!;

        public string PidFileDirectory { get; set; } = default!;

        public string DefaultServerRootDirectory { get; set; } = default!;
        
        public string P4DExecute { get; set; } = default!;
    }

    private static AppPathsJson? jsonObject;

    public static string? ConfigFile => AppPaths.jsonObject!.ConfigFile;

    public static string? PidFileDirectory => AppPaths.jsonObject!.PidFileDirectory;

    public static string? DefaultServerRootDirectory => AppPaths.jsonObject!.DefaultServerRootDirectory;

    public static string? P4DExecute => AppPaths.jsonObject!.P4DExecute;

    static AppPaths()
    {
        var assembly = Assembly.GetEntryAssembly();
        var resourceStream = assembly?.GetManifestResourceStream($"HelixCoreServerCtl.AppPaths.json");
        if (resourceStream is not null)
        {
            try
            {
                jsonObject = JsonSerializer.Deserialize<AppPathsJson>(resourceStream);
            }
            catch (System.Exception)
            {
                throw;
            }
        }
    }
}
