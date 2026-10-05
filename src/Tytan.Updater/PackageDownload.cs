using System.IO.Compression;

namespace Tytan.Updater;

public sealed partial class UpdateApiClient
{
    public async Task<string> DownloadAsync(UpdatePackage package, string clientFolder, string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        PathRules.ValidatePackage(package.File, clientFolder);
        if (!package.File.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected file is not a ZIP archive.");
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        var destination = Path.GetFullPath(destinationDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(destination);
        var finalPath = Path.Combine(destination, package.File.Name);
        if (File.Exists(finalPath) || Directory.Exists(finalPath))
            throw new IOException("The destination file already exists; it will not be overwritten.");
        var temporary = Path.Combine(destination, $".tytan-{Guid.NewGuid():N}.part");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(http.Timeout);
        var token = timeout.Token;
        try
        {
            using var request = CreateRequest("download.php?file=" + Uri.EscapeDataString(package.File.Path));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            CheckResponse(response);
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(output, token);
                await output.FlushAsync(token);
                if ((package.File.Size is long expected && output.Length != expected) ||
                    (response.Content.Headers.ContentLength is long length && output.Length != length))
                    throw new InvalidDataException("The downloaded size does not match the expected size.");
            }
            await ValidateZipAsync(temporary, token);
            token.ThrowIfCancellationRequested();
            // Same-directory move publishes only complete files, and never replaces another download.
            File.Move(temporary, finalPath, overwrite: false);
            return finalPath;
        }
        finally
        {
            // Delete only this operation's temporary file; preserve the original failure if cleanup fails.
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task ValidateZipAsync(string path, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count == 0) throw new InvalidDataException("The ZIP archive contains no files.");
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            await using var stream = entry.Open();
            await stream.CopyToAsync(Stream.Null, token);
        }
    }
}
