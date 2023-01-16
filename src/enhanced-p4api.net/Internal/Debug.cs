// Copyright (c) 2022, Perforce Software, Inc. All rights reserved.

namespace Perforce.P4;

class Debug
{
#if _DEBUG
    public static void Trace(string msg) { System.Diagnostics.Trace.WriteLine(msg); }
    public static void TraceIf(bool test, string msg) { System.Diagnostics.Trace.WriteLineIf(test, msg); }
#else
    public static void Trace(string msg) { }
    public static void TraceIf(bool test, string msg) { }
#endif
}