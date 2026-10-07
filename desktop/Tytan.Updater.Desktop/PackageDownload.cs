using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Tytan.Updater.Desktop;

internal sealed record DownloadProgress(long Bytes, long Total, string Stage)
{
    public int Percent => Total > 0 ? Math.Clamp((int)((double)Bytes / Total * 100), 0, 100) : 0;
}

internal sealed partial class CloudApiClient
{
    public async Task<string> DownloadAsync(string clientFolder, ProductUpdate update, string destination,
        string username, string password, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        // A download must still correspond to a newer package for this installation.
        RemoteEntry? package = update.Package;
        if (package is null || package.Size is null || package.Size <= 0 ||
            update.Status != UpdateStatus.UpdateAvailable ||
            UpdateComparison.Compare(new LocalInstallation(clientFolder, new[] { update.Product }), new[] { package })[0]
                is not { Status: UpdateStatus.UpdateAvailable } verified || verified.AvailableVersion != update.AvailableVersion)
        {
            throw new InvalidDataException("Select a newer ZIP package from the loaded client's comparison results.");
        }
        if (!InstallationFileReader.ValidName(clientFolder) || !InstallationFileReader.ValidName(package.Name) ||
            string.IsNullOrWhiteSpace(username) || password.Length == 0 || username.Contains(':') ||
            username.Any(char.IsControl) || password.Any(char.IsControl))
        {
            throw new InvalidDataException("A valid client folder, username and password are required.");
        }

        string target = Path.GetFullPath(destination);
        if (!target.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Path.GetDirectoryName(target)))
        {
            throw new InvalidDataException("Choose a ZIP destination in an existing directory.");
        }
        if (File.Exists(target) || Directory.Exists(target))
        {
            throw new IOException("The destination already exists. Choose another filename; existing files are preserved.");
        }

        // A unique sibling file keeps incomplete downloads separate and publishes by rename.
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".part";
        bool created = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(15));
        CancellationToken token = deadline.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            var endpoint = new Uri(baseUri, "download.php?file=" + Uri.EscapeDataString(package.Path));
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException($"Package download failed (HTTP {(int)response.StatusCode}). Check credentials and try again.",
                    null, response.StatusCode);
            }
            if (response.Content.Headers.ContentType?.MediaType != "application/octet-stream")
            {
                throw new InvalidDataException("The download endpoint did not return a binary package.");
            }
            long expected = package.Size.Value;
            if (response.Content.Headers.ContentLength is long length && length != expected)
            {
                throw new InvalidDataException("The package size changed since the listing. Check for updates again.");
            }

            long received = 0;
            using (Stream input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            {
                using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                created = true;
                byte[] buffer = new byte[65536];
                int count;
                while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    if (count > expected - received)
                    {
                        throw new InvalidDataException("The downloaded package is larger than its listed size.");
                    }
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    received += count;
                    progress?.Report(new DownloadProgress(received, expected, "Downloading"));
                }
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            if (received != expected)
            {
                throw new InvalidDataException("The downloaded package is incomplete or its size changed. Check for updates again.");
            }

            progress?.Report(new DownloadProgress(received, expected, "Validating ZIP"));
            await Task.Run(() => ValidateZip(temporary, token), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            // File.Move without overwrite also protects a destination created during transfer.
            File.Move(temporary, target, overwrite: false);
            created = false;
            progress?.Report(new DownloadProgress(received, expected, "Downloaded"));
            return target;
        }
        finally
        {
            if (created)
            {
                File.Delete(temporary);
            }
        }
    }

    private static void ValidateZip(string path, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(path);
        if (!archive.Entries.Any(entry => entry.Name.Length > 0))
        {
            throw new InvalidDataException("The ZIP contains no files.");
        }

        // Read entries without extraction; bound validation work for unexpectedly expanded packages.
        const long maximumExpandedBytes = 2L * 1024 * 1024 * 1024;
        long total = 0;
        byte[] buffer = new byte[65536];
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (entry.Length > maximumExpandedBytes - total)
            {
                throw new InvalidDataException("The ZIP exceeds the 2 GiB expanded validation limit.");
            }
            using Stream content = entry.Open();
            long read = 0;
            int count;
            while ((count = content.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                read += count;
                total += count;
                if (total > maximumExpandedBytes || read > entry.Length)
                {
                    throw new InvalidDataException("The ZIP has invalid or excessive expanded content.");
                }
            }
            if (read != entry.Length)
            {
                throw new InvalidDataException("A ZIP entry could not be read completely.");
            }
        }
    }
}
