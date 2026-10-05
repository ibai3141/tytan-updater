namespace Tytan.Updater;

public static class PackageSelector
{
    public static UpdatePackage? SelectNewest(IEnumerable<FileEntry> files, string product)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(product);
        UpdatePackage? newest = null;
        var prefix = product + "_";
        foreach (var file in files)
        {
            if (file is null || file.Type != "file" || string.IsNullOrEmpty(file.Name) ||
                !file.Name.StartsWith(prefix, StringComparison.Ordinal) ||
                !file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            var versionText = file.Name[prefix.Length..^4];
            if (!PackageVersion.TryParse(versionText, out var version)) continue;
            if (newest is null || version.CompareTo(newest.Version) > 0)
                newest = new(file, version);
        }
        return newest;
    }
}
