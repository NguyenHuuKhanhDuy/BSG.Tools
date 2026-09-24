using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace BSG.Tools.Services
{
    /// <summary>Wraps Velopack's UpdateManager and swallows its exceptions for MainWindow.</summary>
    public static class UpdateService
    {
        // Download URLs of the latest release rather than GithubSource, which depends on the
        // list-releases API (rate-limited, and it once returned new releases without assets).
        private const string FeedUrl = "https://github.com/NguyenHuuKhanhDuy/BSG.Tools/releases/latest/download/";

        private static readonly UpdateManager Manager = new(new SimpleWebSource(FeedUrl));

        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            // Not installed when running loose (e.g. `dotnet run`): nothing to update in place.
            if (!Manager.IsInstalled)
                return null;

            try
            {
                return await Manager.CheckForUpdatesAsync();
            }
            catch
            {
                // Offline or feed unreachable: keep running the current version.
                return null;
            }
        }

        public static async Task<bool> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo)
        {
            try
            {
                await Manager.DownloadUpdatesAsync(updateInfo);
                Manager.ApplyUpdatesAndRestart(updateInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
