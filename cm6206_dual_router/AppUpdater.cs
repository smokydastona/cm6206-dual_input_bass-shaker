using System.Diagnostics;
using System.IO.Compression;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal static class AppUpdater
{
    public static async Task<bool> TryUpdateToLatestAsync(UpdateInfo info, IWin32Window owner, CancellationToken cancellationToken)
    {
        if (info.AssetDownloadUrl is null)
        {
            // Nothing to download automatically; just open release page.
            TryOpenUrl(info.HtmlUrl);
            return false;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            TryOpenUrl(info.HtmlUrl);
            return false;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            var tempRoot = Path.Combine(Path.GetTempPath(), "Cm6206DualRouter", "update");
            Directory.CreateDirectory(tempRoot);

            var assetName = info.AssetName ?? "update.bin";
            var downloadPath = Path.Combine(tempRoot, assetName);

            AppLog.Info($"Downloading update asset: {info.AssetDownloadUrl}");
            using (var resp = await http.GetAsync(info.AssetDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await resp.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            }

            var stagedExePath = downloadPath;
            if (downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var extractDir = Path.Combine(tempRoot, "extracted");
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, recursive: true);
                Directory.CreateDirectory(extractDir);

                ZipFile.ExtractToDirectory(downloadPath, extractDir);

                var found = Directory.GetFiles(extractDir, "Cm6206DualRouter.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (found is null)
                    throw new InvalidOperationException("Downloaded ZIP did not contain Cm6206DualRouter.exe");

                stagedExePath = found;
            }
            else if (!downloadPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                // Unknown asset type; fall back to browser.
                TryOpenUrl(info.HtmlUrl);
                return false;
            }

            return TryLaunchReplaceAndRelaunch(owner, exePath, stagedExePath);
        }
        catch (Exception ex)
        {
            AppLog.Error("Auto-update failed", ex);
            try
            {
                MessageBox.Show(owner, ex.Message, "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // ignore
            }

            TryOpenUrl(info.HtmlUrl);
            return false;
        }
    }

    private static bool TryLaunchReplaceAndRelaunch(IWin32Window owner, string targetExePath, string newExePath)
    {
        var pid = Environment.ProcessId;

        var tempDir = Path.Combine(Path.GetTempPath(), "Cm6206DualRouter", "update");
        Directory.CreateDirectory(tempDir);

        var cmdPath = Path.Combine(tempDir, "apply_update.cmd");

        // Minimal updater: wait for this PID to exit, copy new exe over old, relaunch.
        // Works for typical "Downloads" usage. If the app is installed under Program Files,
        // the copy will fail without admin.
        var cmd = "@echo off\r\n" +
                  "setlocal\r\n" +
                  $"set PID={pid}\r\n" +
                  $"set TARGET=\"{targetExePath}\"\r\n" +
                  $"set NEW=\"{newExePath}\"\r\n" +
                  ":wait\r\n" +
                  "tasklist /fi \"PID eq %PID%\" | find \"%PID%\" >nul\r\n" +
                  "if not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
                  "copy /y %NEW% %TARGET% >nul\r\n" +
                  "if errorlevel 1 (\r\n" +
                  "  echo Update copy failed. You may need to run as Administrator.\r\n" +
                  "  start \"\" %TARGET%\r\n" +
                  "  exit /b 1\r\n" +
                  ")\r\n" +
                  "start \"\" %TARGET%\r\n" +
                  "exit /b 0\r\n";

        File.WriteAllText(cmdPath, cmd);

        var result = MessageBox.Show(
            owner,
            "An update is ready to apply. The app will close, replace itself, and re-launch.\n\nContinue?",
            "Update available",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);

        if (result != DialogResult.OK)
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = cmdPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            // Exit the current process so the updater can replace the file.
            Application.Exit();
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to launch updater", ex);
            return false;
        }
    }

    private static void TryOpenUrl(Uri url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url.ToString(),
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore
        }
    }
}
