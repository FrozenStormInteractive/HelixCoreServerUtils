using System.Diagnostics;

namespace HelixCoreServerCtl;

public enum HelixCoreServerCaseSensitivity
{
    Sensitive = 0,
    Insensitive = 1,
    None = 2,
}

public class HelixCoreServerCmdOptions
{
    public string? Journal { get; set; }

    public string? Log { get; set; }

    public string? Port { get; set; }

    public string? Root { get; set; }

    public HelixCoreServerCaseSensitivity CaseSensitivity { get; set; } = HelixCoreServerCaseSensitivity.None;
}

internal class HelixCoreServer
{
    public string ExecutablePath { get; }

    public HelixCoreServer(string ExecutablePath)
    {
        this.ExecutablePath = ExecutablePath;
    }

    public async Task EnableUnicodeAsync(string root, HelixCoreServerCmdOptions? options = null)
    {
        options ??= new HelixCoreServerCmdOptions();
        options.Root = root;
        await ExecuteCommand($"-r \"{root}\" -xi", options);
    }

    public async Task ExecuteCommand(string cmd, HelixCoreServerCmdOptions? options = null)
    {
        if (options?.Port is not null)
        {
            cmd += " -p " + options.Port;
        }

        if (options?.Root is not null)
        {
            cmd += $" -r \"{options.Root}\"";
        }

        if (options?.Journal is not null)
        {
            cmd += $" -J \"{options.Journal}\"";
        }

        if (options?.Log is not null)
        {
            cmd += $" -L \"{options.Log}\"";
        }

        if (options is not null && options.CaseSensitivity != HelixCoreServerCaseSensitivity.None)
        {
            cmd += " -C " + options.CaseSensitivity.ToString();
        }

        await Process.Start(ExecutablePath, cmd).WaitForExitAsync();
    }
}
