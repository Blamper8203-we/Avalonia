using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DINBoard.Services;

/// <summary>
/// Rejestruje skojarzenie rozszerzenia .dinboard z aplikacja (Windows, HKCU).
/// </summary>
public class FileAssociationService
{
    private const string Extension = ".dinboard";
    private const string ProgId = "DINBoard.Project";
    private const string FriendlyTypeName = "Projekt DINBoard";
    private const string IconRelativePath = "Assets\\dinboard_file.ico";

    public void EnsureDinboardAssociation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            string? executablePath = ResolveExecutablePath();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return;
            }

            string openCommand = BuildOpenCommand(executablePath);
            string iconPath = ResolveIconPath(executablePath);

            using var extensionKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Extension);
            extensionKey?.SetValue(string.Empty, ProgId, RegistryValueKind.String);
            extensionKey?.SetValue("PerceivedType", "document", RegistryValueKind.String);

            using var classKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId);
            classKey?.SetValue(string.Empty, FriendlyTypeName, RegistryValueKind.String);

            using var defaultIconKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId + @"\DefaultIcon");
            defaultIconKey?.SetValue(string.Empty, $"\"{iconPath}\"", RegistryValueKind.String);

            using var commandKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId + @"\shell\open\command");
            commandKey?.SetValue(string.Empty, openCommand, RegistryValueKind.String);

            RefreshShellAssociations();
            AppLog.Info("Zaktualizowano skojarzenie plikow .dinboard");
        }
        catch (Exception ex)
        {
            AppLog.Warn("Nie udalo sie zarejestrowac skojarzenia plikow .dinboard", ex);
        }
    }

    private static string? ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        var process = System.Diagnostics.Process.GetCurrentProcess();
        return process.MainModule?.FileName;
    }

    private static string BuildOpenCommand(string executablePath)
    {
        string executableName = Path.GetFileName(executablePath);
        bool isDotnetHost = executableName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("dotnet", StringComparison.OrdinalIgnoreCase);

        if (isDotnetHost)
        {
            string appDllPath = Path.Combine(AppContext.BaseDirectory, "DINBoard.dll");
            if (File.Exists(appDllPath))
            {
                return $"\"{executablePath}\" \"{appDllPath}\" \"%1\"";
            }
        }

        return $"\"{executablePath}\" \"%1\"";
    }

    private static string ResolveIconPath(string executablePath)
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, IconRelativePath);
        return File.Exists(iconPath) ? iconPath : executablePath;
    }

    private static void RefreshShellAssociations()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const uint SHCNE_ASSOCCHANGED = 0x08000000;
        const uint SHCNF_IDLIST = 0x0000;
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
