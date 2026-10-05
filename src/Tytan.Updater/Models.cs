using System.Text.Json.Serialization;

namespace Tytan.Updater;

// One file or folder from the API listing. Attributes map the lowercase JSON fields.
public sealed record FileEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("modified")] string? Modified,
    [property: JsonPropertyName("path")] string Path);

// Tytan supplies these values; the updater does not inspect the installed application.
public sealed record UpdateRequest(
    string ClientFolder,
    string Product,
    string InstalledVersion,
    string DestinationDirectory);

// A remote file paired with its parsed numeric version.
public sealed record UpdatePackage(FileEntry File, PackageVersion Version);

// Downloaded means the ZIP is ready for Tytan, not that it has been installed.
public enum UpdateStatus
{
    NoUpdate,
    Downloaded,
    Error,
    Cancelled
}

// LocalPath is populated only after a complete package has been published locally.
public sealed record UpdateResult(
    UpdateStatus Status,
    string Product,
    string InstalledVersion,
    string? AvailableVersion = null,
    string? LocalPath = null,
    string? Message = null);
