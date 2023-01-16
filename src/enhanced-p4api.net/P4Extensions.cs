using System.Runtime.InteropServices;

namespace Perforce.P4;

public static class P4Extensions
{
    internal static void Throw(P4ClientErrorList errors)
    {
        foreach (P4ClientError current in errors)
        {
            if (current.SeverityLevel >= P4Exception.MinThrowLevel)
                throw new P4Exception(errors);
        }
    }
}
