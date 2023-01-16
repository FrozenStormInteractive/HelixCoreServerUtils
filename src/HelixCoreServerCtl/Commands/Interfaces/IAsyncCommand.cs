namespace HelixCoreServerCtl;

public interface IAsyncCommand
{
    Task<int> Execute();
}
