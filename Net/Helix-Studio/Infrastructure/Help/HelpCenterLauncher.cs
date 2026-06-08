using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Helix_Studio.Infrastructure.Help;

/// <summary>
/// Extracts the built-in help pages and opens the help entry page.
/// </summary>
public static class HelpCenterLauncher
{
    private static readonly object SyncRoot = new();

    private static readonly string[] HelpFiles =
    [
        "index.html",
        "docs.html",
        "faq.html",
        "modules.html",
        "security.html",
        "settings.html",
        "troubleshooting.html",
        "help.css",
        "help.js",
    ];

    /// <summary>
    /// Opens the help index in the default browser.
    /// </summary>
    public static void OpenIndex()
    {
        var helpDirectory = EnsureHelpDirectory();
        var indexPath = Path.Combine(helpDirectory, "index.html");

        Process.Start(new ProcessStartInfo
        {
            FileName = indexPath,
            UseShellExecute = true,
        });
    }

    private static string EnsureHelpDirectory()
    {
        lock (SyncRoot)
        {
            var helpDirectory = Path.Combine(Path.GetTempPath(), "Helix-Studio", "Help");
            Directory.CreateDirectory(helpDirectory);

            foreach (var helpFile in HelpFiles)
            {
                ExtractHelpResource(helpFile, Path.Combine(helpDirectory, helpFile));
            }

            return helpDirectory;
        }
    }

    private static void ExtractHelpResource(string helpFile, string targetPath)
    {
        var resourceUri = new Uri($"pack://application:,,,/Assets/web/{helpFile}", UriKind.Absolute);
        var resourceInfo = Application.GetResourceStream(resourceUri)
            ?? throw new FileNotFoundException($"Help resource not found: {helpFile}");

        using var source = resourceInfo.Stream;
        using var destination = File.Create(targetPath);
        source.CopyTo(destination);
    }
}
