using CommandLine;

namespace HelixCoreServerCtl;

[Verb("status")]
internal class StatusCommand : ICommand
{
    [Value(0, MetaValue = "<name>", Required = true)]
    public string ServiceName { get; set; } = null!;

    public int Execute()
    {
        var service = ServiceManager.Instance.FindServiceByName(ServiceName);
        if (service is not null)
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
                Console.WriteLine($"'{service.Config.Name}' p4d service not running.");
            }
            return 0;
        }
        else
        {
            Console.Error.WriteLine($"Service '{ServiceName}' not found.");
            return 1;
        }
    }
}
