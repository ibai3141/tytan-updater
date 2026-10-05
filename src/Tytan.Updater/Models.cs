using System.Text.Json.Serialization;

namespace Tytan.Updater;

public sealed record FileEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("modified")] string? Modified,
    [property: JsonPropertyName("path")] string Path);

public sealed record UpdateRequest(string ClientFolder, string Product, string InstalledVersion, string DestinationDirectory);
public sealed record UpdatePackage(FileEntry File, PackageVersion Version);
public enum UpdateStatus { NoUpdate, Downloaded, Error, Cancelled }
public sealed record UpdateResult(UpdateStatus Status, string Product, string InstalledVersion,
    string? AvailableVersion = null, string? LocalPath = null, string? Message = null);
