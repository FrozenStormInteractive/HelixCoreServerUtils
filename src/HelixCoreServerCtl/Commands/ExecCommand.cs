using CommandLine;

namespace HelixCoreServerCtl;

[Verb("exec")]
internal class ExecCommand : IAsyncCommand
{
    [Value(0, MetaValue = "<name>", Required = true)]
    public string ServiceName { get; set; } = null!;

    [Value(1, MetaValue = "<exec args>")]
	public IEnumerable<string> More { get; set; } = null!;

    [Option('f', "force", Default = false, HelpText = "Force exec to execute the command even if that p4d instance is currently running.")]
    public bool Force { get; set; }

    public async Task<int> Execute()
    {
        var service = ServiceManager.Instance.FindServiceByName(ServiceName);
        if (service is not null)
        {
            if (Force || !service.IsRunning)
            {
                return await service.ExecAsync(More);
            }
            else
            {
                Console.Error.WriteLine($"Service '{ServiceName}' is running.");
                return 1;
            }
        }
        else
        {
            Console.Error.WriteLine($"Service '{ServiceName}' not found.");
            return 1;
        }
    }
}
