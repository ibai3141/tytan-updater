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
            return DesktopChecks.Run();
        }

        if (args.Length > 1 || (args.Length == 1 && args[0].StartsWith("--")))
        {
            MessageBox.Show("Usage: Tytan.Updater.Desktop.exe [installation.json]", "Tytan Updater");
            return 1;
        }

        Application.Run(new MainForm(args.Length == 1 ? Path.GetFullPath(args[0]) : null));
        return 0;
    }
}
