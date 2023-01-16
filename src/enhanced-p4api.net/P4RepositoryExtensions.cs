using System.Text;

namespace Perforce.P4;

public static class P4RepositoryExtensions
{
    public static object? ConfigureSet(this Repository perforceRepository, string variable, string value, Options? options = null)
    {
        options ??= new Options();
        using (P4Command cmd = new P4Command(perforceRepository, "configure", true, "set", $"{variable}={value}"))
        {
            P4CommandResult r = cmd.Run(options);
            if (r.Success != true)
            {
                P4Extensions.Throw(r.ErrorList);
                return null;
            }

            return r.InfoOutput;
        }
    }

    public static object? SetCounter(this Repository perforceRepository, string name, int value, Options? options = null)
    {
        options ??= new Options();
        options["-f"] = null;
        using (P4Command cmd = new P4Command(perforceRepository, "counter", false, name, value.ToString()))
        {
            P4CommandResult r = cmd.Run(options);
            if (r.Success != true)
            {
                P4Extensions.Throw(r.ErrorList);
                return null;
            }

            return r.InfoOutput;
        }
    }

    public static object? SetTypeMap(this Repository perforceRepository, TypeMap typemap, Options? options = null)
    {
        options ??= new Options();
        options["-i"] = null;
        using (P4Command cmd = new P4Command(perforceRepository, "typemap", false))
        {
            StringBuilder inStrBuilder = new StringBuilder("TypeMap:").Append("\r\n");
            foreach (var entry in typemap)
            {
                inStrBuilder.Append("\t").Append(entry.FileType.ToString()).Append(" ").Append(entry.Path).Append("\r\n");
            }

            cmd.DataSet = inStrBuilder.ToString();
            P4CommandResult r = cmd.Run(options);
            if (r.Success != true)
            {
                P4Extensions.Throw(r.ErrorList);
                return null;
            }

            return r.InfoOutput;
        }
    }

    public static object? SetProtectionTable(this Repository perforceRepository, IList<ProtectionEntry> protectionTable)
    {
        GetProtectionTableCmdOptions options = new GetProtectionTableCmdOptions(GetProtectionTableCmdFlags.Input);

        using (P4Command cmd = new P4Command(perforceRepository, "protect", false))
        {
            StringBuilder inStrBuilder = new StringBuilder("Protections:").Append("\r\n");
            foreach (var entry in protectionTable)
            {
                inStrBuilder.Append("\t").Append(new StringEnum<ProtectionMode>(entry.Mode).ToString(StringEnumCase.Lower)).Append(" ")
                    .Append(new StringEnum<EntryType>(entry.Type).ToString(StringEnumCase.Lower)).Append(" ").Append(entry.Name).Append(" ")
                    .Append(entry.Host).Append(" ").Append(entry.Path).Append(" ")
                    .Append("\r\n");
            }

            cmd.DataSet = inStrBuilder.ToString();
            P4CommandResult r = cmd.Run(options);
            if (r.Success != true)
            {
                P4Extensions.Throw(r.ErrorList);
                return null;
            }

            return r.InfoOutput;
        }
    }

    public static object? AdminUpdateSpecDepot(this Repository perforceRepository, Options? options = null)
    {
        using (P4Command cmd = new P4Command(perforceRepository, "admin", false, "updatespecdepot", "-a"))
        {
            P4CommandResult r = cmd.Run(options);
            if (r.Success != true)
            {
                P4Extensions.Throw(r.ErrorList);
                return null;
            }

            return r.InfoOutput;
        }
    }
}
