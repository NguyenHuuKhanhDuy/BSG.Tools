using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace BSG.Tools.Services
{
    /// <summary>
    /// Wraps Velopack's UpdateManager so MainWindow doesn't need to know
    /// about GithubSource setup or handle Velopack exceptions itself.
    /// </summary>
    public static class UpdateService
    {
        private const string RepoUrl = "https://github.com/NguyenHuuKhanhDuy/BSG.Tools";

        private static readonly UpdateManager Manager = new(new GithubSource(RepoUrl, null, false));

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
