using CommandLine;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace HelixCoreServerCtl;

[Verb("start")]
internal class StartCommand : IAsyncCommand
{
    [Value(0, MetaValue = "<name>")]
    public IEnumerable<string> ServiceNames { get; set; } = null!;

    [Option('a', "all", Default = false, HelpText = "All servers.")]
    public bool AllServices { get; set; }

    [Option('q', Default = false, HelpText = "Send output to syslog instead of STDOUT or STDERR.")]
    public bool Silent { get; set; }

    private Logger? log;

    public async Task<int> Execute()
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

        log = logConfiguration.CreateLogger();

        IList<Service> services;
        if (AllServices)
        {
            services = ServiceManager.Instance.GetAllServices().Where(x => x.Config.Enabled && !x.IsRunning).ToList();
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
                    log.Error($"Service '{serviceName}' not found.");
                }
            }
        }

        var tasks = new List<Task>();
        foreach (var service in services)
        {
            tasks.Add(StartService(service));
        }
        await Task.WhenAll(tasks);
        log.Information($"Started {services.Count} service.");

        return 0;
    }

    public async Task StartService(Service service)
    {
        if (service.IsRunning)
        {
            log!.Information($"Started '{service.Config.Name}' p4d service.");
        }
        else
        {
            // TODO: Handle exceptions
            await service.StartAsync(Silent);
            log!.Information($"Started '{service.Config.Name}' p4d service.");
        }
    }
}
