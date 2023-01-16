using CommandLine;
using HelixCoreServerCtl;

#if !DEBUG
try
{
#endif
    AppConfig.InitInstance();
    AppConfig.Instance.Validate();

    Type[] commands = new Type[]
    {
        typeof(NewCommand),
        typeof(StartCommand),
        typeof(StopCommand),
        typeof(RestartCommand),
        typeof(ListCommand),
        typeof(StatusCommand),
        typeof(CheckpointCommand),
        typeof(ExecCommand),
        typeof(UpgradeCommand),
    };

    var parser = new Parser(with => {
        with.EnableDashDash = true;
        with.HelpWriter = Console.Error;
    });

    return await parser.ParseArguments(args, commands)
        .MapResult(
            (ICommand command) => Task.FromResult(command.Execute()),
            async (IAsyncCommand command) => await command.Execute(),
            _ => Task.FromResult(1));
#if !DEBUG
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
#endif
