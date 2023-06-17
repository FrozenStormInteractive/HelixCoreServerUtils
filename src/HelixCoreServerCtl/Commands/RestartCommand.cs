using CommandLine;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace HelixCoreServerCtl;

[Verb("restart")]
internal class RestartCommand : IAsyncCommand
{
    [Value(0, MetaValue = "<name>")]
    public IEnumerable<string> ServiceNames { get; set; } = null!;

    [Option('a', "all", Default = false, HelpText = "All servers.")]
    public bool AllServices { get; set; }

    [Option('f', "force", Default = false)]
    public bool Force { get; set; }

    [Option('q', Default = false, HelpText = "Send output to syslog instead of STDOUT or STDERR.")]
    public bool Syslog { get; set; }

    private Logger? log;

    public async Task<int> Execute()
    {
        var logConfiguration = new LoggerConfiguration();
        if (Syslog)
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

        log = logConfiguration.CreateLogger();

        IList<Service> services;
        if (AllServices)
        {
            services = ServiceManager.Instance.GetAllServices().Where(x => x.Config.Enabled && x.IsRunning).ToList();
        }
        else
        {
            List<Service> serviceList = new List<Service>();
            services = serviceList;
            foreach (var serviceName in ServiceNames)
            {
                var service = ServiceManager.Instance.FindServiceByName(serviceName);
                if (service is not null)
                {
                    serviceList.Add(service);
                }
                else
                {
                    log.Error($"Service '{serviceName}' not found.");
                }
            }
        }

        var tasks = new List<Task>();
        foreach (var service in services)
        {
            tasks.Add(RestartService(service));
        }
        await Task.WhenAll(tasks);

        log.Information($"Restarted {services.Count} service.");

        return 0;
    }

    public async Task RestartService(Service service)
    {
        if (service.IsRunning)
        {
            await service.RestartAsync(Force);
            log!.Information($"Restarted '{service.Config.Name}' p4d service.");
        }
        else
        {
            // TODO: Handle exceptions
            await service.StartAsync();
            log!.Information($"'{service.Config.Name}' p4d service not running.");
        }
    }
}
