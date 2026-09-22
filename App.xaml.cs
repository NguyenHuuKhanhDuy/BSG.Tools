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
            // Must run as early as possible (before any UI) so Velopack can handle
            // its internal install/uninstall/update hooks correctly.
            VelopackApp.Build().Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply the saved theme before the window is created, so it renders
            // with the right colors from the first frame instead of flashing light
            // then switching to dark.
            var settings = SettingsService.Load();
            ThemeManager.Apply(settings.Theme);

            new MainWindow().Show();
        }
    }
}
