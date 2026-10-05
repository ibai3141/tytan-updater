namespace Tytan.Updater;

public sealed class UpdateService(UpdateApiClient api)
{
    public async Task<UpdateResult> CheckAndDownloadAsync(UpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? available = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            PathRules.ValidateSegment(request.ClientFolder, "Carpeta del cliente");
            PathRules.ValidateSegment(request.Product, "Producto");
            ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationDirectory);
            if (!PackageVersion.TryParse(request.InstalledVersion, out var installed))
                throw new ArgumentException("La versión instalada debe tener tres componentes numéricos.");
            var files = await api.ListAsync(request.ClientFolder, cancellationToken);
            var package = PackageSelector.SelectNewest(files, request.Product);
            available = package?.Version.ToString();
            if (package is null || package.Version.CompareTo(installed) <= 0)
                return new(UpdateStatus.NoUpdate, request.Product, request.InstalledVersion, available,
                    Message: package is null ? "No hay paquete disponible para el producto." : "No hay una versión superior.");
            var path = await api.DownloadAsync(package, request.ClientFolder, request.DestinationDirectory, cancellationToken);
            return new(UpdateStatus.Downloaded, request.Product, request.InstalledVersion, available, path,
                "Paquete descargado; Tytan puede continuar con su instalación.");
        }
        catch (OperationCanceledException)
        {
            return new(cancellationToken.IsCancellationRequested ? UpdateStatus.Cancelled : UpdateStatus.Error,
                request.Product, request.InstalledVersion, available,
                Message: cancellationToken.IsCancellationRequested ? "Operación cancelada." : "Tiempo de espera agotado.");
        }
        catch (HttpRequestException e)
        {
            return new(UpdateStatus.Error, request.Product, request.InstalledVersion, available,
                Message: e.StatusCode is { } status ? $"Error HTTP {(int)status}; compruebe acceso y ruta del servidor." : "No se pudo conectar con el servidor.");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(UpdateStatus.Error, request.Product, request.InstalledVersion, available, Message: e.Message);
        }
    }
}
