using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Cm6206DualRouter;

public static class VendorControlPanel
{
    // Likely control panel executables shipped with C-Media/Xear bundles.
    private static readonly string[] CandidateExeNames =
    [
        "CmeAuVist64.exe",
        "CmeAuVist.exe",
        "FaceLift_x64.exe",
        "FaceLift.exe",
        "CmElv64.exe",
        "CmElv.exe",
        "CmEnhance.exe"
    ];

    private static readonly string[] LikelyProductTokens =
    [
        "c-media",
        "cmedia",
        "cm6206",
        "xear",
        "usb 7.1",
        "usb audio"
    ];

    public static bool TryLaunch(out string message)
    {
        if (!TryFindExecutable(out var exePath, out var reason))
        {
            message = reason;
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            message = $"Launched: {exePath}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Failed to launch {exePath}: {ex.Message}";
            return false;
        }
    }

    public static bool TryFindExecutable(out string exePath, out string reason)
    {
        // 1) Look in uninstall registry entries for an install location / icon path.
        foreach (var location in EnumerateLikelyInstallLocationsFromRegistry())
        {
            var found = FindCandidateExe(location);
            if (found is not null)
            {
                exePath = found;
                reason = "";
                return true;
            }
        }

        // 2) Fallback: probe a few common vendor folders without deep recursion.
        foreach (var root in EnumerateCommonRoots())
        {
            var found = FindCandidateExe(root);
            if (found is not null)
            {
                exePath = found;
                reason = "";
                return true;
            }
        }

        exePath = "";
        reason = "No installed C-Media control panel was detected (searched uninstall registry + common Program Files folders).";
        return false;
    }

    private static IEnumerable<string> EnumerateCommonRoots()
    {
        static IEnumerable<string> RootsFor(string baseDir)
        {
            if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                yield break;

            // Known top-level vendor folder names.
            var names = new[]
            {
                "C-Media",
                "CMedia",
                "C-Media Audio",
                "Xear",
                "USB Audio"
            };

            foreach (var name in names)
            {
                var candidate = Path.Combine(baseDir, name);
                if (Directory.Exists(candidate))
                    yield return candidate;
            }
        }

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        foreach (var d in RootsFor(pf)) yield return d;
        foreach (var d in RootsFor(pfx86)) yield return d;
    }

    private static IEnumerable<string> EnumerateLikelyInstallLocationsFromRegistry()
    {
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var viewBaseKey in new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            })
            {
                using var uninstall = root.OpenSubKey(viewBaseKey);
                if (uninstall is null) continue;

                foreach (var subName in uninstall.GetSubKeyNames())
                {
                    using var sub = uninstall.OpenSubKey(subName);
                    if (sub is null) continue;

                    var displayName = (sub.GetValue("DisplayName") as string) ?? "";
                    if (!LooksLikeVendorProduct(displayName))
                        continue;

                    var installLocation = (sub.GetValue("InstallLocation") as string) ?? "";
                    var displayIcon = (sub.GetValue("DisplayIcon") as string) ?? "";

                    if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                        yield return installLocation;

                    // Some installers only provide DisplayIcon (often an .exe path).
                    var iconPath = TryParseFirstPath(displayIcon);
                    if (!string.IsNullOrWhiteSpace(iconPath))
                    {
                        var dir = Path.GetDirectoryName(iconPath);
                        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                            yield return dir;
                    }
                }
            }
        }
    }

    private static bool LooksLikeVendorProduct(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return false;

        var dn = displayName.Trim().ToLowerInvariant();
        foreach (var token in LikelyProductTokens)
        {
            if (dn.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? FindCandidateExe(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        // Check directory itself.
        foreach (var exe in CandidateExeNames)
        {
            var path = Path.Combine(directory, exe);
            if (File.Exists(path))
                return path;
        }

        // Check one level deep (some bundles use Driver/CPL subfolders).
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(directory))
            {
                foreach (var exe in CandidateExeNames)
                {
                    var path = Path.Combine(sub, exe);
                    if (File.Exists(path))
                        return path;
                }
            }
        }
        catch
        {
            // ignore access issues
        }

        return null;
    }

    private static string? TryParseFirstPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Common forms:
        //   C:\Path\App.exe,0
        //   "C:\Path\App.exe" /args
        var trimmed = value.Trim();

        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            if (end > 1)
                return trimmed[1..end];
        }

        var comma = trimmed.IndexOf(',');
        if (comma > 0)
            trimmed = trimmed[..comma];

        var space = trimmed.IndexOf(' ');
        if (space > 0)
            trimmed = trimmed[..space];

        return trimmed;
    }
}
