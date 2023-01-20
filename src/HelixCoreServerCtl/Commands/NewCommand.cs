using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net.NetworkInformation;
using CommandLine;
using Perforce.P4;
using P4 = Perforce.P4;
using Sharprompt;
using System.Reflection;
using Mono.Unix;
using Mono.Unix.Native;

namespace HelixCoreServerCtl;

[Verb("new")]
internal class NewCommand : IAsyncCommand
{
    [Value(0, MetaValue = "<name>", HelpText = "Set service name.")]
    public string? ServiceName { get; set; }

    [Option('p', "port", MetaValue = "<P4PORT>", HelpText = "Set Perforce Server's address.")]
    public string? Port { get; set; }

    [Option('r', "root-directory", MetaValue = "<P4ROOT>", HelpText = "Set Perforce Server's root directory.")]
    public string? RootDirectory { get; set; }

    [Option('u', "username", MetaValue = "<username>", HelpText = "Perforce super-user login name.")]
    public string? SuperUserName { get; set; }

    [Option('P', "password", MetaValue = "<password>", HelpText = "Perforce super-user password.")]
    public string? SuperUserPassword { get; set; }

    [Option("unicode", HelpText = "Enable unicode mode on server.")]
    public bool? Unicode { get; set; }

    [Option("case", HelpText = "Case-sensitivity.")]
    public bool? CaseSensitivity { get; set; }

    [Option("init", HelpText = "Initialize the server with the recommended settings.")]
    public bool? InitRecommended { get; set; }

    [Option("no-ssl")]
    public bool? NoSSL { get; set; }

    private string? SSLDirectory;

    public async Task<int> Execute()
    {
        {
            var sudoUidStr = Environment.GetEnvironmentVariable("SUDO_UID");
            var uid = UnixUserInfo.GetRealUserId();
            bool printWarning = false;

            if ((sudoUidStr is null && uid != 0))
            {
                printWarning = true;
            }
            else if (sudoUidStr is not null)
            {
                if (int.TryParse(sudoUidStr, out int sudoUid))
                {
                    if (uid != 0 && sudoUid == uid)
                    {
                        printWarning = true;
                    }
                }
            }

            if (printWarning)
            {
                Console.WriteLine("Warning: this command must be executed with the user who will run the Perforce server. To change the user, use sudo.");
            }
        }

        // TODO: Validate input

        ServiceName ??= Prompt.Input<string>("Perforce Service name", validators: new[] 
        { 
            Validators.Required(),
            Validators.RegularExpression(@"^[\w\-]+$", "Service name should only alphanumeric symbols"),
            Validators.MaxLength(32),
            PerforcePromptValidators.ServiceNameNotExists(),
        });

        var defaultRootDirectory = Path.Combine(AppConfig.Instance.DefaultServerRootDirectory!, ServiceName);
        RootDirectory ??= Prompt.Input<string>("Perforce Server root (P4ROOT)", defaultRootDirectory, validators: new[] 
        {
            Validators.Required(),
            PerforcePromptValidators.P4RootDirectoryNotConfigured(this),
            PerforcePromptValidators.DirectoryIsEmpty(),
        });
        Unicode ??= Prompt.Confirm("Perforce Server unicode-mode", false);
        CaseSensitivity ??= Prompt.Confirm("Perforce Server case-sensitive (Not recommended for compatibility with Windows clients)", false);
        Port ??= Prompt.Input<string>("Perforce Server address (P4PORT)", validators: new[]
        {
            Validators.Required(),
            PerforcePromptValidators.ValidP4Port(),
            PerforcePromptValidators.P4PortNotUsed(),
        });
        SuperUserName ??= Prompt.Input<string>("Perforce super-user login", validators: new[] 
        {
            Validators.Required(),
            Validators.MinLength(3),
        });
        SuperUserPassword ??= Prompt.Password("Perforce super-user password", validators: new[] 
        { 
            Validators.Required(), 
            Validators.MinLength(8),
            Validators.RegularExpression(@"(?=.*\d)(?=.*[a-z])(?=.*[A-Z])", "Password should be mixed case or contain non alphabetic characters"),
        });
        var temporaryUserPassword = SuperUserPassword + "0";

        InitRecommended ??= Prompt.Confirm("Initialize the server with the recommended settings", true);
        var startAfterConfiguration = Prompt.Confirm("Start the server after the configuration", true);

        var appConfig = AppConfig.Instance;

        string? serviceFileName = null;
        string? defaultServiceFileName = null;
        var includePath = appConfig.Includes?.FirstOrDefault();
        if (includePath is not null)
        {
            defaultServiceFileName = Path.Combine(includePath,  $"{ServiceName}.conf");
            if (System.IO.File.Exists(defaultServiceFileName))
            {
                int counter = 1;
                do
                {
                    defaultServiceFileName = Path.Combine(includePath,  $"{ServiceName}{counter}.conf");
                    counter++;
                } while (System.IO.File.Exists(defaultServiceFileName) || counter < 99);
            }
        }

        serviceFileName ??= Prompt.Input<string>("Service config path (where to save the .conf file)", defaultServiceFileName, validators: new[] 
        {
            Validators.Required(),
            PerforcePromptValidators.FileNotExists(),
        });

        if (NoSSL is null)
        {
            if (P4Port.TryParse(Port, out P4Port? p4Port) && p4Port.Protocol is not null && p4Port.Protocol.StartsWith("ssl"))
            {
                NoSSL = false;
            }
            else
            {
                NoSSL = true;
            }
        }

        Console.WriteLine($"Configuring p4d service '{ServiceName}' with the information you specified...");

        FilePermissions oldUmaskPerms = Mono.Unix.Native.Syscall.umask(FilePermissions.S_IRWXG | FilePermissions.S_IRWXO);
        
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(Path.Combine(RootDirectory, "root"));
        Directory.CreateDirectory(Path.Combine(RootDirectory, "journals"));
        Directory.CreateDirectory(Path.Combine(RootDirectory, "logs"));
        Directory.CreateDirectory(Path.Combine(RootDirectory, "archives"));

        {
            int r = Mono.Unix.Native.Syscall.chmod(RootDirectory, Mono.Unix.Native.FilePermissions.S_IRWXU);
            UnixMarshal.ThrowExceptionForLastErrorIf(r);
        }

        RootDirectory = Path.Combine(RootDirectory, "root");
        SSLDirectory = Path.Combine(RootDirectory, "ssl");

        await RunP4D("\"-cset P4JOURNAL=../journals/journal\"").WaitForExitAsync();
        await RunP4D("\"-cset P4LOG=../logs/log\"").WaitForExitAsync();
        await RunP4D($"-xD {ServiceName}").WaitForExitAsync();
        await RunP4D($"\"-cset {ServiceName}#P4PORT={Port}\"").WaitForExitAsync();

        if (!NoSSL.Value)
        {
            Console.WriteLine("Generating new SSL key pair");
            Directory.CreateDirectory(SSLDirectory);
            {
                int r = Mono.Unix.Native.Syscall.chmod(SSLDirectory, Mono.Unix.Native.FilePermissions.S_IRWXU);
                UnixMarshal.ThrowExceptionForLastErrorIf(r);
            }
            if (!System.IO.File.Exists(Path.Combine(SSLDirectory, "certificate.txt")) && 
                !System.IO.File.Exists(Path.Combine(SSLDirectory, "privatekey.txt")))
            {
                await RunP4D("-Gc").WaitForExitAsync();
            }
            else
            {
                Console.WriteLine($"SSL certificates found in {SSLDirectory}");
            }
        }

        var service = ServiceManager.Instance.CreateService(new ServiceConfig
        {
            FilePath = serviceFileName,
            Name = ServiceName,
            Execute = appConfig.P4DExecute,
            ServerType = "p4d",
            Environment = new Dictionary<string, string?>() {
                { "P4ROOT", RootDirectory },
                { "P4PORT", Port },
                { "P4SSLDIR", SSLDirectory },
            }
        })!;

        Console.WriteLine($"Service conf file saved as {serviceFileName}");

        if (Unicode.Value)
        {
            await RunP4D("-xi").WaitForExitAsync();
        }

        using (TemporaryDirectory tempDir = new TemporaryDirectory())
        {
            string[] settings = new string[] { "P4CLIENT", "P4CONFIG", "P4IGNORE", "P4PASSWD", "P4PORT", "P4TRUST", "P4TICKETS", "P4USER" };
            IDictionary<string, string?> settingValues = default!;

            try
            {
                await service.StartAsync(silent: true);

                settingValues = new Dictionary<string, string?>();
                foreach (var settingName in settings)
                {
                    settingValues[settingName] = P4Server.Get(settingName);
                    P4Server.Set(settingName, null);
                }

                P4Server.Set("P4TRUST", Path.Combine(tempDir.Path, ".p4trust.txt"));
                P4Server.Set("P4TICKETS", Path.Combine(tempDir.Path, ".p4tickets.txt"));

                var server = new P4.Server(new P4.ServerAddress(Port));
                var repository = new P4.Repository(server);
                var con = repository.Connection;
                con.UserName = SuperUserName;

                P4.Options connectOptions = new P4.Options();
                connectOptions["ProgramName"] ="p4dctl-ng";
                var programVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (programVersion is not null)
                {
                    connectOptions["ProgramVersion"] = programVersion.ToString();
                }

                if (!NoSSL.Value)
                {
                    con.TrustAndConnect(connectOptions, "-y", null);
                } 
                else
                {
                    con.Connect(connectOptions);
                }

                if (InitRecommended.Value)
                {
                    // Setting configurables
                    repository.ConfigureSet("run.users.authorize", "1");
                    repository.ConfigureSet("dm.user.noautocreate", "2");
                    repository.ConfigureSet("server.start.unlicensed", "1");

                    repository.ConfigureSet("server.maxcommands", "2500");
                    repository.ConfigureSet("net.backlog", "2048");
                    repository.ConfigureSet("net.autotune", "1");
                    repository.ConfigureSet("db.monitor.shared", "4096");
                    repository.ConfigureSet("db.reorg.disable", "1");
                    repository.ConfigureSet("lbr.autocompress", "1");

                    repository.ConfigureSet("filesys.bufsize", "1M");
                    repository.ConfigureSet("filesys.checklinks", "2");
                    repository.ConfigureSet("server.commandlimits", "2");
                    repository.ConfigureSet("rejectList", "P4EXP,version=2014.2");

                    repository.ConfigureSet("rpl.checksum.auto", "1");
                    repository.ConfigureSet("rpl.checksum.change", "2");
                    repository.ConfigureSet("rpl.checksum.table", "1");

                    repository.ConfigureSet("proxy.monitor.level", "1");
                    repository.ConfigureSet("monitor", "1");
                    repository.ConfigureSet("server", "3");

                    repository.ConfigureSet("filesys.P4ROOT.min", "2G");
                    repository.ConfigureSet("filesys.depot.min", "2G");
                    repository.ConfigureSet("filesys.P4JOURNAL.min", "2G");
                    repository.ConfigureSet("server.depot.root", "../archives");
                    repository.ConfigureSet("journalPrefix", $"../journals/{ServiceName}");

                    // Enabling structured logs
                    repository.ConfigureSet("serverlog.file.1", "../logs/commands.csv");
                    repository.ConfigureSet("serverlog.retain.1", "10");
                    repository.ConfigureSet("serverlog.file.2", "../logs/errors.csv");
                    repository.ConfigureSet("serverlog.retain.2", "10");
                    repository.ConfigureSet("serverlog.file.3", "../logs/events.csv");
                    repository.ConfigureSet("serverlog.retain.3", "10");
                    repository.ConfigureSet("serverlog.file.4", "../logs/integrity.csv");
                    repository.ConfigureSet("serverlog.retain.4", "10");
                    repository.ConfigureSet("serverlog.file.5", "../logs/auth.csv");
                    repository.ConfigureSet("serverlog.retain.5", "10");

                    // Refresh
                    con.Disconnect();
                    con.Connect(connectOptions);

                    Console.WriteLine("Enabling unload and spec depots");

                    var specDepot = new P4.Depot 
                    {
                        Id = "spec",
                        Type = P4.DepotType.Spec,
                        Owner = SuperUserName,
                        Map = "spec/...",
                        StreamDepth = "1",
                    };
                    specDepot = repository.CreateDepot(specDepot);
                    repository.AdminUpdateSpecDepot();

                    var unloadDepot = new P4.Depot 
                    {
                        Id = "unload",
                        Type = P4.DepotType.Unload,
                        Owner = SuperUserName,
                        Map = "unload/...",
                        StreamDepth = "1",
                    };
                    unloadDepot = repository.CreateDepot(unloadDepot);

                    Console.WriteLine("Populating the typemap");
                    P4.TypeMap typemap = new();
                    typemap.AddEntry(P4.BaseFileType.Text, "//....asp");
                    typemap.AddEntry(P4.BaseFileType.Text, "//....cnf");
                    typemap.AddEntry(P4.BaseFileType.Text, "//....css");
                    typemap.AddEntry(P4.BaseFileType.Text, "//....htm");
                    typemap.AddEntry(P4.BaseFileType.Text, "//....html");
                    typemap.AddEntry(P4.BaseFileType.Text, "//....inc");
                    typemap.AddEntry(P4.BaseFileType.Text, "//....js");
                    typemap.AddEntry(P4.BaseFileType.Text, P4.FileTypeModifier.Writable, "//....log");
                    typemap.AddEntry(P4.BaseFileType.Text, P4.FileTypeModifier.Writable, "//....ini");
                    typemap.AddEntry(P4.BaseFileType.Text, P4.FileTypeModifier.Writable, "//....pdm");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....zip");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....bz2");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....rar");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....gz");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....avi");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....jpg");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....jpeg");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....mpg");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....gif");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....tif");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....mov");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.FullRevisions | P4.FileTypeModifier.ExclusiveOpen, "//....jar");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....ico");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....exp");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....btr");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....bmp");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....doc");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....dot");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....xls");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....ppt");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....pdf");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....tar");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....exe");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....dll");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....lib");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....bin");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....class");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....war");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....ear");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....so");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....rpt");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....cfm");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....ma");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....mb");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....pac");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....m4a");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....mp4");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....aac");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....wma");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....docx");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....pptx");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....xlsx");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....png");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....raw");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....odt");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....ods");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....odg");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....odp");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....otg");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....ots");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....ott");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....psd");
                    typemap.AddEntry(P4.BaseFileType.Binary, P4.FileTypeModifier.ExclusiveOpen, "//....sxw");
                    repository.SetTypeMap(typemap);

                    Console.WriteLine("Initializing protections table...");
                    var protectionTable = repository.GetProtectionTable();
                    protectionTable.Add(new P4.ProtectionEntry(P4.ProtectionMode.List, P4.EntryType.User, "*", "*", "-//spec/...", true));
                    repository.SetProtectionTable(protectionTable);
                    repository.SetCounter("security", 3);
                }

                // Creating super-user account...
                var superUser = repository.GetUser(SuperUserName);
                superUser.Password = temporaryUserPassword;
                superUser.EmailAddress = $"{SuperUserName}@{ServiceName}";
                superUser = repository.UpdateUser(superUser);

                con.Login(temporaryUserPassword, null, null);

                con.SetPassword(temporaryUserPassword, SuperUserPassword, SuperUserName);

                con.Disconnect();
            }
            #if !DEBUG
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error when initializing the server: {e.Message}");
                return 1;
            }
            #endif
            finally
            {
                if (settingValues is not null)
                {
                    foreach (var entry in settingValues)
                    {
                        P4Server.Set(entry.Key, entry.Value);
                    }
                }

                if (!startAfterConfiguration && service.IsRunning)
                {
                    await service.StopAsync();
                }
            }
        }

        Mono.Unix.Native.Syscall.umask(oldUmaskPerms);
        Console.WriteLine("Setup complete");

        return 0;
    }

    private Process RunP4D(string arguments, IDictionary<string, string?>? environment = null,  bool silent = true)
    {
        if (CaseSensitivity.HasValue)
        {
            var caseSensitivityNumber = CaseSensitivity.Value ? 0 : 1;
            arguments = $"-C {caseSensitivityNumber} " + arguments;
        }

        var execProcessStartInfo = new ProcessStartInfo
        {
            FileName = AppConfig.Instance.P4DExecute,
            Arguments = arguments,
            UseShellExecute = false,
        };

        if (RootDirectory is not null)
        {
            execProcessStartInfo.Environment["P4ROOT"] = RootDirectory;
        }
        execProcessStartInfo.Environment["P4LOG"] = "../logs/log";
        execProcessStartInfo.Environment["P4JOURNAL"] = "../journals/journal";
        if (SSLDirectory is not null)
        {
            execProcessStartInfo.Environment["P4SSLDIR"] = SSLDirectory;
        }

        if (environment is not null)
        {
            foreach (var entry in environment)
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

        return Process.Start(execProcessStartInfo)!;
    }

    private static class PerforcePromptValidators
    {
        public static Func<object?, ValidationResult?> ServiceNameNotExists(string? errorMessage = default)
        {
            return input =>
            {
                if (input is not string strValue)
                {
                    return ValidationResult.Success;
                }

                if (ServiceManager.Instance.FindServiceByName(strValue) is null)
                {
                    return ValidationResult.Success;
                }

                return new ValidationResult(errorMessage ?? "Service name already used");
            };
        }

        public static Func<object?, ValidationResult?> ValidP4Port(string? errorMessage = default)
        {
            return input =>
            {
                if (input is not string strValue)
                {
                    return ValidationResult.Success;
                }

                if (P4Port.IsValid(strValue))
                {
                    return ValidationResult.Success;
                }

                return new ValidationResult(errorMessage ?? "Invalid port");
            };
        }

        public static Func<object?, ValidationResult?> P4PortNotUsed(string? errorMessage = default)
        {
            return input =>
            {

                if (input is not string strValue)
                {
                    return ValidationResult.Success;
                }

                if (!P4Port.TryParse(strValue, out P4Port? p4Port))
                {
                    return ValidationResult.Success;
                }

                foreach(var endPoint in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                {
                    if (endPoint.Port == p4Port.PortNumber)
                    {
                        return new ValidationResult(errorMessage ?? "Port is already used");
                    }
                }

                return ValidationResult.Success;
            };
        }

        public static Func<object?, ValidationResult?> P4RootDirectoryNotConfigured(NewCommand command, string? errorMessage = default)
        {
            return input =>
            {
                if (input is not string path)
                {
                    return ValidationResult.Success;
                }

                if (!Directory.Exists(Path.Combine(path, "root")))
                {
                    return ValidationResult.Success;
                }

                var environment = new Dictionary<string, string?>()
                {
                    { "P4ROOT", Path.Combine(path, "root") }
                };
                var process = command.RunP4D("-jd - db.protect", environment);
                process.WaitForExit();
                if (process.StandardOutput.ReadToEnd().IndexOf(" 255 ") > -1)
                {
                    return new ValidationResult(errorMessage ?? "P4 root directory is already initialized");
                }

                return ValidationResult.Success;
            };
        }

        public static Func<object?, ValidationResult?> DirectoryIsEmpty(string? errorMessage = default)
        {
            return input =>
            {
                if (input is not string path)
                {
                    return ValidationResult.Success;
                }

                if (!Directory.Exists(Path.Combine(path)))
                {
                    return ValidationResult.Success;
                }

                if (Directory.EnumerateFileSystemEntries(path).Any())
                {
                    return new ValidationResult(errorMessage ?? "Directory is not empty");
                }

                return ValidationResult.Success;
            };
        }

        public static Func<object?, ValidationResult?> FileNotExists(string? errorMessage = default)
        {
            return input =>
            {
                if (input is not string path)
                {
                    return ValidationResult.Success;
                }

                if (System.IO.File.Exists(path))
                {
                    return new ValidationResult(errorMessage ?? "File already exists");
                }

                return ValidationResult.Success;
            };
        }
    }
}
