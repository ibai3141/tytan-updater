using Tytan.Updater.Desktop;

namespace Tytan.Updater.Cli;

internal sealed record Credentials(string Username, string Password);

internal static class Command
{
    internal const string Usage = "Usage: Tytan.Updater.Cli <client-folder> <installed-folder-name>";

    internal static async Task<int> RunAsync(string[] args, CloudApiClient api, TextWriter output,
        TextWriter error, Func<Credentials> credentials, CancellationToken token,
        Func<string, CancellationToken, Task<string?>>? chooseDestination = null)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            await output.WriteLineAsync(Usage);
            await output.WriteLineAsync("Example: Tytan.Updater.Cli Barcin_Wodbar Faktury_008.000.042");
            await output.WriteLineAsync("If an update is available, a Save As window lets you choose the ZIP destination. No local configuration file is read.");
            return 0;
        }

        if (args.Length != 2)
        {
            await error.WriteLineAsync(Usage);
            return 2;
        }

        try
        {
            token.ThrowIfCancellationRequested();

            if (!InstallationFileReader.ValidName(args[0]))
            {
                throw new InvalidDataException("The first argument must be a single client folder name.");
            }

            // Parse the final underscore: product names may themselves contain underscores.
            string installedFolder = args[1];
            int separator = installedFolder.LastIndexOf('_');
            if (!InstallationFileReader.ValidName(installedFolder) || separator <= 0 ||
                !InstallationFileReader.ValidName(installedFolder[..separator]) ||
                !PackageVersion.TryParse(installedFolder[(separator + 1)..], out _))
            {
                throw new InvalidDataException("The second argument must be an installed folder name such as Faktury_008.000.042 (product_NNN.NNN.NNN).");
            }

            var product = new InstalledProduct(installedFolder[..separator], installedFolder[(separator + 1)..]);
            var installation = new LocalInstallation(args[0], new[] { product });

            Credentials account = credentials();
            if (string.IsNullOrEmpty(account.Password))
            {
                throw new InvalidDataException("Set TYTAN_API_PASSWORD before running the command.");
            }

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

            foreach (ProductUpdate update in updates)
            {
                token.ThrowIfCancellationRequested();
                await output.WriteLineAsync("Choose where to save " + update.Package!.Name + ".");
                string? destination = await (chooseDestination ?? DownloadLocation.ChooseAsync)(update.Package.Name, token);
                token.ThrowIfCancellationRequested();
                if (destination is null)
                {
                    await output.WriteLineAsync("Download cancelled. No package was downloaded.");
                    return 130;
                }
                await output.WriteLineAsync("Downloading " + update.Package!.Name + "...");
                string path = await api.DownloadAsync(installation.ClientFolder, update,
                    destination, account.Username, account.Password,
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
            UnauthorizedAccessException or HttpRequestException or ArgumentException or NotSupportedException or
            System.ComponentModel.Win32Exception)
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
