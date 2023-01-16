using CommandLine;

namespace HelixCoreServerCtl;

[Verb("status")]
internal class StatusCommand : ICommand
{
    [Value(0, MetaValue = "<name>")]
    public IEnumerable<string> ServiceNames { get; set; } = null!;

    [Option('a', "all", Default = false, HelpText = "All servers.")]
    public bool AllServices { get; set; }

    public int Execute()
    {
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
                    Console.Error.WriteLine($"Service '{serviceName}' not found.");
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
                    Console.WriteLine($"'{service.Config.Name}' p4d service is disabled.");
                }
                else if (service.IsRunning)
                {
                    Console.WriteLine($"'{service.Config.Name}' p4d service is running.");
                }
                else
                {
                    exitCode = 1;
                    Console.WriteLine($"'{service.Config.Name}' p4d service not running.");
                }
            }

            return exitCode;
        }
        else
        {
            return 4;
        }
    }
}
