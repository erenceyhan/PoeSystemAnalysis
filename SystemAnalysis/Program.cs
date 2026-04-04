using System.IO;
using SystemAnalysis.Config;
using SystemAnalysis.UI;
using System.Windows;

namespace SystemAnalysis;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var bundledConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var portableConfigPath = Path.Combine(AppContext.BaseDirectory, "user-settings.json");
        var legacyLocalConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemAnalysis",
            "appsettings.json");

        if (!File.Exists(portableConfigPath))
        {
            if (File.Exists(legacyLocalConfigPath))
            {
                File.Copy(legacyLocalConfigPath, portableConfigPath);
            }
            else if (File.Exists(bundledConfigPath))
            {
                File.Copy(bundledConfigPath, portableConfigPath);
            }
        }

        var configPath = portableConfigPath;
        var config = ConfigLoader.Load(configPath);
        var application = new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };

        application.Run(new MainWindow(configPath, config));
    }
}
