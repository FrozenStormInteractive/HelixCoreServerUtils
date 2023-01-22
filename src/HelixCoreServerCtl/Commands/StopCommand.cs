using CommandLine;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace HelixCoreServerCtl;

[Verb("stop")]
internal class StopCommand : IAsyncCommand
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
            services = ServiceManager.Instance.GetAllServices().Where(x => x.Config.Enabled && x.IsRunning).ToList();
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
                    serviceList.Add(service);
                }
                else
                {
                    log.Error($"Service '{serviceName}' not found.");
                }
            }
        }

        var tasks = new List<Task<int>>();
        foreach (var service in services)
        {
            tasks.Add(StopService(service));
        }

        var taskResults = await Task.WhenAll(tasks);
        log.Information($"Stopped {taskResults.Count(x => x == 0)} service.");
        return taskResults.Any(x => x != 0) ? 1 : 0;
    }

    public async Task<int> StopService(Service service)
    {
        if (service.IsRunning)
        {
            // TODO: Handle exceptions
            var exitCode = await service.StopAsync();
            if (exitCode == 0)
            {
                log!.Information($"Stopped '{service.Config.Name}' p4d service.");
            }
            else
            {
                log!.Information($"'{service.Config.Name}' p4d service has error on stop.");
            }
            return exitCode;
        }
        else
        {
            log!.Information($"'{service.Config.Name}' p4d service not running.");
            return 0;
        }
    }
}
