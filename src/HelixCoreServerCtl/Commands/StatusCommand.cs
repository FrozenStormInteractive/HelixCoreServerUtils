using CommandLine;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace HelixCoreServerCtl;

[Verb("status")]
internal class StatusCommand : ICommand
{
    [Value(0, MetaValue = "<name>")]
    public IEnumerable<string> ServiceNames { get; set; } = null!;

    [Option('a', "all", Default = false, HelpText = "All servers.")]
    public bool AllServices { get; set; }

    [Option('q', Default = false, HelpText = "Send output to syslog instead of STDOUT or STDERR.")]
    public bool Silent { get; set; }

    public int Execute()
    {
        var logConfiguration = new LoggerConfiguration();
        if (Silent)
        {
            logConfiguration.WriteTo.LocalSyslog("p4dctl-ng");
        }
        else
        {
            logConfiguration.WriteTo.Console(
                outputTemplate: "{Message:lj}{NewLine}{Exception}", 
                theme: ConsoleTheme.None,
                standardErrorFromLevel: LogEventLevel.Error);
        }

        var log = logConfiguration.CreateLogger();

        bool errorWhenLoadingServices = false;

        IList<Service> services;
        if (AllServices)
        {
            services = ServiceManager.Instance.GetAllServices().Where(x => x.Config.Enabled).ToList();
        }
        else
        {
            List<Service> serviceList =  new List<Service>();
            services = serviceList;
            foreach (var serviceName in ServiceNames)
            {
                var service = ServiceManager.Instance.FindServiceByName(serviceName);
                if (service is not null)
                {
                    if (service.Config.Enabled)
                    {
                        serviceList.Add(service);
                    }
                }
                else
                {
                    errorWhenLoadingServices = true;
                    log.Error($"Service '{serviceName}' not found.");
                }
            }
        }

        if (services.Count > 0)
        {
            int exitCode = 0;

            foreach (var service in services)
            {
                if (!service.Config.Enabled)
                {
                    log.Information($"'{service.Config.Name}' p4d service is disabled.");
                }
                else if (service.IsRunning)
                {
                    log.Information($"'{service.Config.Name}' p4d service is running.");
                }
                else
                {
                    exitCode = 1;
                    log.Information($"'{service.Config.Name}' p4d service not running.");
                }
            }

            return exitCode;
        }
        else
        {
            return errorWhenLoadingServices ? 1 : 0;
        }
    }
}
