using SystemAnalysis.Config;
using SystemAnalysis.UI;

namespace SystemAnalysis;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var config = ConfigLoader.Load(configPath);

        Application.Run(new MainForm(configPath, config));
    }
}
