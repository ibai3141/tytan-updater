using System.IO.Compression;

namespace Tytan.Updater;

public sealed partial class UpdateApiClient
{
    // Return a final path only after the complete ZIP has passed validation.
    public async Task<string> DownloadAsync(
        UpdatePackage package,
        string clientFolder,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        // Validate server-provided names and enforce the requested client folder.
        ArgumentNullException.ThrowIfNull(package);
        PathRules.ValidatePackage(package.File, clientFolder);
        if (!package.File.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The selected file is not a ZIP archive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        // Keep the temporary and final files in the same destination directory.
        var destination = Path.GetFullPath(destinationDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(destination);
        var finalPath = Path.Combine(destination, package.File.Name);
        if (File.Exists(finalPath) || Directory.Exists(finalPath))
        {
            throw new IOException("The destination file already exists; it will not be overwritten.");
        }

        // A unique name lets concurrent attempts write separate temporary files.
        var temporary = Path.Combine(destination, $".tytan-{Guid.NewGuid():N}.part");

        // This deadline covers headers, copying the body, and ZIP validation.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(http.Timeout);
        var token = timeout.Token;

        try
        {
            // Use the exact relative path returned by the listing API.
            using var request = CreateRequest("download.php?file=" + Uri.EscapeDataString(package.File.Path));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            CheckResponse(response);

            // Stream directly to disk and prevent sharing the open temporary file.
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(output, token);
                await output.FlushAsync(token);

                // Check both advertised sizes when present; either mismatch fails.
                if ((package.File.Size is long expected && output.Length != expected) ||
                    (response.Content.Headers.ContentLength is long length && output.Length != length))
                {
                    throw new InvalidDataException("The downloaded size does not match the expected size.");
                }
            }

            // Read the ZIP before making the final filename visible to Tytan.
            await ValidateZipAsync(temporary, token);
            token.ThrowIfCancellationRequested();

            // Same-directory move publishes only complete files, and never replaces another download.
            File.Move(temporary, finalPath, overwrite: false);
            return finalPath;
        }
        finally
        {
            // Delete only this operation's temporary file; preserve the original failure if cleanup fails.

            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch (IOException)
            {
                // Leave an inaccessible temporary file rather than hide the original error.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup is best-effort when the operating system denies deletion.
            }
        }
    }

    // Check decompression without extracting files or running package contents.
    private static async Task ValidateZipAsync(string path, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count == 0)
        {
            throw new InvalidDataException("The ZIP archive contains no files.");
        }

        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            await using var stream = entry.Open();
            await stream.CopyToAsync(Stream.Null, token);
        }
    }
}
