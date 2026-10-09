namespace Tytan.Updater.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // A smoke check exercises real file parsing and real WinForms controls.
        if (args.Length > 0 && args[0] == "--self-test")
        {
            int local = DesktopChecks.Run();
            if (local != 0)
            {
                return local;
            }
            int comparisons = ComparisonChecks.Run();
            if (comparisons != 0)
            {
                return comparisons;
            }
            int cloud = CloudChecks.Run();
            if (cloud != 0) return cloud;
            int configured = ConfiguredClientChecks.Run();
            return configured == 0 ? DownloadChecks.Run() : configured;
        }

        if (args.Length == 1 && args[0] == "--verify-live")
        {
            return CloudChecks.Run(live: true);
        }

        if (args.Length == 1 && args[0] == "--verify-live-download")
        {
            return DownloadChecks.Run(live: true);
        }

        if (args.Length > 1 || (args.Length == 1 && args[0].StartsWith("--")))
        {
            MessageBox.Show("Usage: Tytan.Updater.Desktop.exe [installation.json]", "Tytan Updater");
            return 1;
        }

        Application.Run(new MainForm(args.Length == 1 ? Path.GetFullPath(args[0]) : null,
            settingsPath: FolderPreferences.DefaultPath));
        return 0;
    }
}
