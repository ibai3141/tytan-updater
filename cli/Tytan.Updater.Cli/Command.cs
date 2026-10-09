using Tytan.Updater.Desktop;

namespace Tytan.Updater.Cli;

internal sealed record Credentials(string Username, string Password);

internal static class Command
{
    internal const string Usage = "Usage: Tytan.Updater.Cli <client-folder> <local-version-folder> [--output <download-folder>]";

    internal static async Task<int> RunAsync(string[] args, CloudApiClient api, TextWriter output,
        TextWriter error, Func<Credentials> credentials, CancellationToken token)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            await output.WriteLineAsync(Usage);
            await output.WriteLineAsync("The local folder must contain installation.json. Only newer ZIPs are downloaded; installation is not performed.");
            return 0;
        }

        if (args.Length is not (2 or 4) || (args.Length == 4 && args[2] != "--output"))
        {
            await error.WriteLineAsync(Usage);
            return 2;
        }

        try
        {
            token.ThrowIfCancellationRequested();

            // Provisional adapter: replace only this source when the real format is supplied.
            string localFolder = Path.GetFullPath(args[1]);
            LocalInstallation installation = InstallationFileReader.Read(Path.Combine(localFolder, "installation.json"));
            if (!InstallationFileReader.ValidName(args[0]) || args[0] != installation.ClientFolder)
            {
                throw new InvalidDataException("The client-folder argument must exactly match clientFolder in installation.json.");
            }

            Credentials account = credentials();
            if (string.IsNullOrEmpty(account.Password))
            {
                throw new InvalidDataException("Set TYTAN_API_PASSWORD before running the command.");
            }

            string destination = args.Length == 4
                ? Path.GetFullPath(args[3]) : Path.Combine(localFolder, "downloads");
            await output.WriteLineAsync("Checking cloud folder: " + installation.ClientFolder);
            IReadOnlyList<RemoteEntry> entries = await api.ListAsync(installation.ClientFolder,
                account.Username, account.Password, token);
            token.ThrowIfCancellationRequested();
            IReadOnlyList<ProductUpdate> comparisons = UpdateComparison.Compare(installation, entries);

            foreach (ProductUpdate update in comparisons)
            {
                await output.WriteLineAsync($"{update.Product.Name}: installed {update.Product.InstalledVersion}; " +
                    $"available {update.AvailableVersion?.ToString() ?? "none"}; {update.StatusText}.");
            }

            ProductUpdate[] updates = comparisons.Where(update => update.Status == UpdateStatus.UpdateAvailable).ToArray();
            if (updates.Length == 0)
            {
                // Missing products are distinct from a successful up-to-date comparison.
                if (comparisons.Any(update => update.Status == UpdateStatus.NoPackage))
                {
                    await error.WriteLineAsync("No matching package found for one or more products. No updates were downloaded.");
                    return 1;
                }
                await output.WriteLineAsync("No updates available.");
                return 0;
            }

            Directory.CreateDirectory(destination);
            foreach (ProductUpdate update in updates)
            {
                token.ThrowIfCancellationRequested();
                await output.WriteLineAsync("Downloading " + update.Package!.Name + "...");
                string path = await api.DownloadAsync(installation.ClientFolder, update,
                    Path.Combine(destination, update.Package.Name), account.Username, account.Password,
                    new TerminalProgress(output), token);
                await output.WriteLineAsync("ZIP saved to: " + path);
            }

            await output.WriteLineAsync($"Downloaded {updates.Length} update(s). Automatic installation is not available.");
            if (comparisons.Any(update => update.Status == UpdateStatus.NoPackage))
            {
                await error.WriteLineAsync("Some products have no matching package; see the comparison above.");
                return 1;
            }
            return 0;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync(token.IsCancellationRequested ? "Operation cancelled." : "The server operation timed out.");
            return token.IsCancellationRequested ? 130 : 1;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or
            UnauthorizedAccessException or HttpRequestException or ArgumentException or NotSupportedException)
        {
            await error.WriteLineAsync("Error: " + exception.Message);
            return 1;
        }
    }

    private sealed class TerminalProgress(TextWriter output) : IProgress<DownloadProgress>
    {
        private int lastPercent = -10;
        private string lastStage = "";

        public void Report(DownloadProgress value)
        {
            // Inline reporting avoids delayed callbacks after completion in a console process.
            if (value.Stage != lastStage || value.Percent >= lastPercent + 10)
            {
                output.WriteLine($"{value.Stage}: {value.Percent}% ({value.Bytes:N0}/{value.Total:N0} bytes).");
                lastStage = value.Stage;
                lastPercent = value.Percent;
            }
        }
    }
}
