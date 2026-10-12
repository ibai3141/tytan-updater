using Tytan.Updater.Desktop;

namespace Tytan.Updater.Cli;

internal sealed record Credentials(string Username, string Password);

internal static class Command
{
    internal const string Usage = "Usage: Tytan.Updater.Cli <client-folder> <installed-folder-name> [destination-folder]";

    internal static async Task<int> RunAsync(string[] args, CloudApiClient api, TextWriter output,
        TextWriter error, Func<Credentials> credentials, CancellationToken token,
        Func<string, CancellationToken, Task<string?>>? chooseDestination = null)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            await output.WriteLineAsync(Usage);
            await output.WriteLineAsync("Example: Tytan.Updater.Cli Barcin_Wodbar Faktury_008.000.042");
            await output.WriteLineAsync("The optional third argument is an existing destination folder. Without it, Save As opens for newer ZIPs. Set TYTAN_API_BASE_URL to change the HTTPS server directory. No local configuration file is read.");
            return 0;
        }

        if (args.Length is < 2 or > 3)
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

            // A supplied destination is a directory, never a ZIP filename.
            // Validate it before contacting the server and preserve existing files.
            string? destinationFolder = null;
            if (args.Length == 3)
            {
                if (string.IsNullOrWhiteSpace(args[2]))
                    throw new InvalidDataException("The destination folder must be an existing directory.");
                destinationFolder = Path.GetFullPath(args[2]);
                if (!Directory.Exists(destinationFolder))
                    throw new InvalidDataException("The destination folder must be an existing directory.");
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
                string? destination;
                if (destinationFolder is not null)
                {
                    // Use the validated server package name directly inside the chosen directory.
                    destination = Path.Combine(destinationFolder, update.Package!.Name);
                }
                else
                {
                    await output.WriteLineAsync("Choose where to save " + update.Package!.Name + ".");
                    destination = await (chooseDestination ?? DownloadLocation.ChooseAsync)(update.Package.Name, token);
                }
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
