namespace Tytan.Updater;

public static class PackageSelector
{
    // Select only the requested product. File dates do not determine version order.
    public static UpdatePackage? SelectNewest(IEnumerable<FileEntry> files, string product)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(product);
        UpdatePackage? newest = null;

        var prefix = product + "_";

        // Including the separator prevents Faktury from matching FakturyExtra.
        foreach (var file in files)
        {
            if (file is null || file.Type != "file" || string.IsNullOrEmpty(file.Name) ||
                !file.Name.StartsWith(prefix, StringComparison.Ordinal) ||
                !file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Remove the product prefix and the four-character .zip extension.
            var versionText = file.Name[prefix.Length..^4];
            if (!PackageVersion.TryParse(versionText, out var version))
            {
                continue;
            }

            // Equal versions keep the first matching entry from the listing.
            if (newest is null || version.CompareTo(newest.Version) > 0)
            {
                newest = new(file, version);
            }
        }

        return newest;
    }
}
