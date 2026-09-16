using System.Windows;
using Velopack;
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
    }
}
