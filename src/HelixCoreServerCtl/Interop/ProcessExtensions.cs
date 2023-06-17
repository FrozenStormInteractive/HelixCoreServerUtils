using System.Diagnostics;
using System.Runtime.Versioning;
using Mono.Unix;
using Mono.Unix.Native;

namespace HelixCoreServerCtl;

static class ProcessExtensions
{
    [SupportedOSPlatformGuard("linux")]
    public static void Kill(this Process process, Signum signal)
    {
        int r = Syscall.kill(process.Id, signal);
        UnixMarshal.ThrowExceptionForLastErrorIf(r);
    }
}
