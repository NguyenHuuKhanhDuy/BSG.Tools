using System.Windows;
using Velopack;
using BSG.Tools.Services;
using Application = System.Windows.Application;

namespace BSG.Tools
{
    public partial class App : Application
    {
        public App()
        {
            // Must run before any UI so Velopack can handle its install/update hooks.
            VelopackApp.Build().Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply saved theme/language before the window exists, so the first frame is already right.
            var settings = SettingsService.Load();
            ThemeManager.Apply(settings.Theme);
            LocalizationManager.Apply(settings.Language);

            new MainWindow().Show();
        }
    }
}
