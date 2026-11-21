using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace DevVault.Tests;

internal static class TestEnvironmentInitializer
{
    private static string? _tempDataDir;

    [ModuleInitializer]
    internal static void Initialize()
    {
        _tempDataDir = Path.Combine(Path.GetTempPath(), $"DevVaultTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDataDir);
        Environment.SetEnvironmentVariable("DEVVAULT_DATA_DIR", _tempDataDir);

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (string.IsNullOrEmpty(_tempDataDir))
                return;

            try
            {
                if (Directory.Exists(_tempDataDir))
                    Directory.Delete(_tempDataDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup on process exit
            }
        };
    }
}
