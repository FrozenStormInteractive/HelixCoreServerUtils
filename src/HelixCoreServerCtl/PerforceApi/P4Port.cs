
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace HelixCoreServerCtl;

public class P4Port
{
    private static readonly string[] Protocols = new string[] { "tcp", "tcp4", "tcp6", "tcp46", "tcp64", "ssl", "ssl4", "ssl6", "ssl46", "ssl64" };

    public string? Protocol { get; private set; }

    public string? Host { get; private set; }

    public int PortNumber { get; private set; }

    public static bool TryParse(string portStr, [NotNullWhen(true)] out P4Port? result)
    {
        if (string.IsNullOrEmpty(portStr))
        {
            result = null;
            return false;
        }

        portStr = portStr.Trim();
        var fragments = portStr.Split(':');

        string portNumberStr = "";
        string? host = null, proto = null;
        if (fragments.Length == 0 || fragments.Length > 3)
        {
            result = null;
            return false;
        }
        else if (fragments.Length == 1)
        {
            portNumberStr = fragments[0].Trim();
        }
        else if (fragments.Length == 2)
        {
            var firstFragment = fragments[0].Trim();
            if (Protocols.Contains(firstFragment))
            {
                proto = !string.IsNullOrWhiteSpace(firstFragment) ? firstFragment : null;
            }
            else
            {
                host = !string.IsNullOrWhiteSpace(firstFragment) ? firstFragment : null;
            }

            portNumberStr = fragments[1].Trim();
        }
        else if (fragments.Length == 3)
        {
            proto = fragments[0].Trim();
            proto = !string.IsNullOrWhiteSpace(proto) ? proto : null;

            host = fragments[1].Trim();
            host = !string.IsNullOrWhiteSpace(host) ? host : null;

            portNumberStr = fragments[2].Trim();
        }

        if (!int.TryParse(portNumberStr, out int portNumber))
        {
            result = null;
            return false;
        }
        
        if (portNumber < 1025 || portNumber > 65535)
        {
            result = null;
            return false;
        }

        result = new P4Port
        {
            Protocol = proto,
            Host = host,
            PortNumber = portNumber,
        };
        return true;
    }

    public static bool IsValid(string portStr)
    {
        return TryParse(portStr, out _);
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(Protocol))
        {
            builder.Append(Protocol).Append(':');
        }

        if (!string.IsNullOrWhiteSpace(Host))
        {
            builder.Append(Host).Append(':');
        }

        builder.Append(PortNumber);
        
        return builder.ToString();
    }
}
