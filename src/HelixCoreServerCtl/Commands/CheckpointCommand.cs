using CommandLine;

namespace HelixCoreServerCtl;

[Verb("checkpoint")]
internal class CheckpointCommand : IAsyncCommand
{
    [Value(0, MetaValue = "<name>", Required = true)]
    public string ServiceName { get; set; } = null!;

    public async Task<int> Execute()
    {
        var service = ServiceManager.Instance.FindServiceByName(ServiceName);
        if (service is not null)
        {
            return await service.ExecAsync("-jc");   
        }
        else
        {
            Console.Error.WriteLine($"Service '{ServiceName}' not found.");
            return 1;
        }
    }
}
