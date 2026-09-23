using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace BSG.Tools.Services
{
    /// <summary>
    /// Wraps Velopack's UpdateManager so MainWindow doesn't need to know
    /// about update source setup or handle Velopack exceptions itself.
    /// </summary>
    public static class UpdateService
    {
        // Reads the feed straight from the latest GitHub release's download URLs instead of
        // GithubSource: GithubSource finds the feed through the GitHub "list releases" API, which
        // has returned newer releases (v1.0.7, v1.0.8) with an empty asset list even though their
        // files were uploaded, so installed apps silently kept seeing v1.0.6 as the newest. Plain
        // download URLs also aren't subject to the API's 60 requests/hour per-IP limit.
        private const string FeedUrl = "https://github.com/NguyenHuuKhanhDuy/BSG.Tools/releases/latest/download/";

        private static readonly UpdateManager Manager = new(new SimpleWebSource(FeedUrl));

        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            // Manager.IsInstalled is false when running loose (e.g. `dotnet run`),
            // where there's nothing Velopack can update in place.
            if (!Manager.IsInstalled)
                return null;

            try
            {
                return await Manager.CheckForUpdatesAsync();
            }
            catch
            {
                // No network / GitHub unreachable / release feed malformed: skip silently,
                // the app must keep working on the current version.
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
