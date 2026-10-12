using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace NikonCamera;

public partial class App
{
    private const string ProductName = "Nikon Camera";

    protected override void OnStartup(StartupEventArgs e)
    {
        ApplyInstalledIcon();
        base.OnStartup(e);
    }

    private static void ApplyInstalledIcon()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("ClickOnce_IsNetworkDeployed"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "camera.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        try
        {
            using var uninstall = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                writable: true);
            if (uninstall is null)
            {
                return;
            }

            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var appKey = uninstall.OpenSubKey(name, writable: true);
                if (appKey?.GetValue("DisplayName") as string != ProductName)
                {
                    continue;
                }

                appKey.SetValue("DisplayIcon", iconPath);
            }
        }
        catch (Exception)
        {
        }
    }
}
