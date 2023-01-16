using CommandLine;

namespace HelixCoreServerCtl;

[Verb("list")]
internal class ListCommand : ICommand
{
    public int Execute()
    {
        var services = ServiceManager.Instance.GetAllServices();

        int typeHeaderWidth = 8, ownerHeaderWidth = 12, nameHeaderWidth = 12;

        foreach (var service in services)
        {
            if (service.Config.ServerType is not null)
            {
                typeHeaderWidth = Math.Max(typeHeaderWidth, service.Config.ServerType.Length);
            }
            if (service.Config.Owner is not null)
            {
                ownerHeaderWidth = Math.Max(ownerHeaderWidth, service.Config.Owner.Length);
            }
            if (service.Config.Name is not null)
            {
                nameHeaderWidth = Math.Max(nameHeaderWidth, service.Config.Name.Length);
            }
        }

        var typeHeader = "Type".PadRight(typeHeaderWidth);
        var ownerHeader = "Owner".PadRight(ownerHeaderWidth);
        var nameHeader = "Name".PadRight(nameHeaderWidth);
        var configHeader = "Config";

        Console.WriteLine($"{typeHeader} {ownerHeader} {nameHeader} {configHeader}");

        foreach (var service in services)
        {
            var type = service.Config.ServerType ?? "";
            var owner = service.Config.Owner ?? "";
            var name = service.Config.Name ?? "";

            type = type.PadRight(typeHeaderWidth);
            owner = owner.PadRight(ownerHeaderWidth);
            name = name.PadRight(nameHeaderWidth);

            Console.WriteLine($"{type} {owner} {name} Config");
        }

        return 0;
    }
}
