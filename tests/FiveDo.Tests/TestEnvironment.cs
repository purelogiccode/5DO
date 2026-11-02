using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace FiveDo.Tests;

internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // The CLI tests execute Program.Main in-process. Never let expected test
        // failures reach the production bug-report endpoint or the user's log file.
        Environment.SetEnvironmentVariable("FIVEDO_BUG_REPORTS", "0");
        Environment.SetEnvironmentVariable("FIVEDO_STATS", "0");
        Environment.SetEnvironmentVariable("FIVEDO_UPDATE_CHECK", "0");
        Environment.SetEnvironmentVariable(
            "FIVEDO_LOG_DIR",
            Path.Combine(Path.GetTempPath(), "5do-tests-logs"));
    }
}
