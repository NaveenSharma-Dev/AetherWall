using Microsoft.Win32;
using System.IO;
using System.Reflection;

namespace AetherWall.Services
{
    public sealed class StartupRegistrationService
    {
        private const string RunKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AetherWall";

        public void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                using RegistryKey? existingKey =
                    Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                existingKey?.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            using RegistryKey runKey =
                Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException(
                    "Could not open the current user's startup settings.");

            runKey.SetValue(
                ValueName,
                GetStartupCommand(),
                RegistryValueKind.String);
        }

        private static string GetStartupCommand()
        {
            string processPath = Environment.ProcessPath
                ?? throw new InvalidOperationException(
                    "Could not locate the AetherWall application path.");

            string command = $"\"{processPath}\"";

            if (string.Equals(
                    Path.GetFileNameWithoutExtension(processPath),
                    "dotnet",
                    StringComparison.OrdinalIgnoreCase))
            {
                string? assemblyPath = Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrWhiteSpace(assemblyPath))
                    command += $" \"{assemblyPath}\"";
            }

            return command;
        }
    }
}
