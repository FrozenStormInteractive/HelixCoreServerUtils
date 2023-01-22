
using System.Diagnostics;
using Mono.Unix.Native;

namespace HelixCoreServerCtl;

internal class Service
{
    public ServiceConfig Config { get; set; }

    public string? Root => Config.Environment?.GetValueOrDefault("P4ROOT");

    public string? Port => Config.Environment?.GetValueOrDefault("P4PORT");

    public string? Target => Config.Environment?.GetValueOrDefault("P4TARGET");

    public string PidFilePath { get; }

    public int ProcessID { get; private set; } = -1;

    private Process? _process;
    public Process? Process 
    {
        get 
        {
            if (_process is null && ProcessID > 0)
            {
                _process = Process.GetProcessById(ProcessID);
            }
            return _process;
        }
        private set 
        {
            _process = value;
        }
    }

    public bool IsRunning 
    {
        get
        {
            if (Process is not null)
            {
                return !Process.HasExited;
            }
            return false;
        }
    }

    public Service(ServiceConfig config)
    {
        this.Config = config;

        PidFilePath = Path.Combine(AppConfig.Instance.PidFileDirectory!, $"p4d.{config.Name}.pid");
        if (File.Exists(PidFilePath))
        {
            var pidFileContent = File.ReadAllText(PidFilePath);
            int pid = 0;
            if (int.TryParse(pidFileContent, out pid))
            {
                if (pid >= 0)
                {
                    ProcessID = pid;
                }
            }
        }
    }

    public async Task<int> StartAsync(bool silent = false)
    {
        if (!IsRunning)
        {
            var bootstrapProcess = StartProcess($"--pid-file={PidFilePath} -d", silent);
            if (bootstrapProcess is not null)
            {
                await bootstrapProcess.WaitForExitAsync();

                if (bootstrapProcess.ExitCode == 0)
                {
                    if (File.Exists(PidFilePath))
                    {
                        var pidFileContent = File.ReadAllText(PidFilePath);
                        int pid = 0;
                        if (int.TryParse(pidFileContent, out pid))
                        {
                            if (pid >= 0)
                            {
                                ProcessID = pid;
                            }
                        }

                    }
                    // TODO: Handle errors

                    return 0;
                }
                else
                {
                    return bootstrapProcess.ExitCode;
                }
            }
            else
            {
                // throw error
            }
        }
        else
        {
            // throw error
        }

        return 1;
    }

    public async Task<int> StopAsync()
    {
        if (IsRunning && Process is not null)
        {
            Process.Kill(Signum.SIGTERM);
            await Process.WaitForExitAsync();

            ProcessID = 0;
            Process = null;
        }
        else
        {
            // throw error
        }

        return 0;
    }

    public async Task RestartAsync(bool force = false)
    {
        if (IsRunning && Process is not null)
        {
            if (force)
            {
                await StopAsync();
                await StartAsync();
            }
            else
            {
                Process.Kill(Signum.SIGHUP);
            }
        }
        else
        {
            // throw error
        }
    }

    public async Task<int> ExecAsync(IEnumerable<string> arguments)
    {
        var execProcess = StartProcess(arguments, false);
        if (execProcess is not null)
        {
            await execProcess.WaitForExitAsync();
            return execProcess.ExitCode;
        }
        else
        {
            // throw error
            return 1;
        }
    }

    public async Task<int> ExecAsync(string arguments, bool silent = false)
    {
        var execProcess = StartProcess(arguments, silent);
        if (execProcess is not null)
        {
            await execProcess.WaitForExitAsync();
            return execProcess.ExitCode;
        }
        else
        {
            // throw error
            return 1;
        }
    }

    private Process? StartProcess(IEnumerable<string> arguments, bool silent)
    {
        var execProcessStartInfo = new ProcessStartInfo
        {
            FileName = Config.Execute,
            UserName = Config.Owner,
            Arguments = Config.Args + "" + string.Join(" ", arguments),
        };

        /*
        foreach (var arg in arguments)
        {
            execProcessStartInfo.ArgumentList.Add(arg);
        }
        */

        if (silent)
        {
            execProcessStartInfo.UseShellExecute = false;
            execProcessStartInfo.RedirectStandardError = true;
            execProcessStartInfo.RedirectStandardOutput = true;
            execProcessStartInfo.RedirectStandardInput = true;
        }

        if (Config.Environment is not null)
        {
            foreach (var entry in Config.Environment)
            {
                execProcessStartInfo.Environment.Add(entry.Key, entry.Value);
            }
        }

        return Process.Start(execProcessStartInfo);
    }

    private Process? StartProcess(string arguments, bool silent)
    {
        var finalArguments = Config.Args + " " + arguments;
        if (silent)
        {
            finalArguments += " -q";
        }

        var execProcessStartInfo = new ProcessStartInfo
        {
            FileName = Config.Execute,
            UserName = Config.Owner,
            Arguments = Config.Args + " " + arguments,
        };

        if (Config.Environment is not null)
        {
            foreach (var entry in Config.Environment)
            {
                execProcessStartInfo.Environment.Add(entry.Key, entry.Value);
            }
        }

        if (silent)
        {
            execProcessStartInfo.UseShellExecute = false;
            execProcessStartInfo.RedirectStandardError = true;
            execProcessStartInfo.RedirectStandardOutput = true;
            execProcessStartInfo.RedirectStandardInput = true;
        }

        return Process.Start(execProcessStartInfo);
    }
}
