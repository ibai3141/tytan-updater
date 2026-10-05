namespace Tytan.Updater;

// Coordinates the workflow; Tytan remains responsible for installation.
public sealed class UpdateService(UpdateApiClient api)
{
    // Expected input, network, and file failures become explicit result values.
    public async Task<UpdateResult> CheckAndDownloadAsync(
        UpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? available = null;

        try
        {
            // Validate caller input before making any network request.
            cancellationToken.ThrowIfCancellationRequested();
            PathRules.ValidateSegment(request.ClientFolder, "Client folder");
            PathRules.ValidateSegment(request.Product, "Product");
            ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationDirectory);
            if (!PackageVersion.TryParse(request.InstalledVersion, out var installed))
            {
                throw new ArgumentException("The installed version must have three numeric components.");
            }

            // Fetch the client listing and isolate the requested product.
            var files = await api.ListAsync(request.ClientFolder, cancellationToken);
            var package = PackageSelector.SelectNewest(files, request.Product);
            available = package?.Version.ToString();

            // Missing, equal, or older versions do not trigger a download.
            if (package is null || package.Version.CompareTo(installed) <= 0)
            {
                return new(UpdateStatus.NoUpdate, request.Product, request.InstalledVersion, available,
                    Message: package is null ? "No package is available for this product." : "No newer version is available.");
            }

            // Deliver the ZIP path while preserving the original installed version.
            var path = await api.DownloadAsync(package, request.ClientFolder, request.DestinationDirectory, cancellationToken);
            return new(UpdateStatus.Downloaded, request.Product, request.InstalledVersion, available, path,
                "Package downloaded; Tytan can proceed with installation.");
        }

        // Distinguish caller cancellation from a timeout in the HTTP operation.
        catch (OperationCanceledException)
        {
            return new(cancellationToken.IsCancellationRequested ? UpdateStatus.Cancelled : UpdateStatus.Error,
                request.Product, request.InstalledVersion, available,
                Message: cancellationToken.IsCancellationRequested ? "Operation cancelled." : "Request timed out.");
        }

        // Avoid leaking raw request details through connection error messages.
        catch (HttpRequestException e)
        {
            return new(UpdateStatus.Error, request.Product, request.InstalledVersion, available,
                Message: e.StatusCode is { } status ? $"HTTP error {(int)status}; check access and the server path." : "Could not connect to the server.");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(UpdateStatus.Error, request.Product, request.InstalledVersion, available, Message: e.Message);
        }
    }
}
