using System.Globalization;

namespace Tytan.Updater.Desktop;

internal readonly record struct PackageVersion(int Major, int Minor, int Patch) : IComparable<PackageVersion>
{
    public static bool TryParse(string? text, out PackageVersion version)
    {
        version = default;
        if (text is null || text.Length != 11 || text[3] != '.' || text[7] != '.' ||
            !int.TryParse(text.AsSpan(0, 3), NumberStyles.None, CultureInfo.InvariantCulture, out int major) ||
            !int.TryParse(text.AsSpan(4, 3), NumberStyles.None, CultureInfo.InvariantCulture, out int minor) ||
            !int.TryParse(text.AsSpan(8, 3), NumberStyles.None, CultureInfo.InvariantCulture, out int patch))
        {
            return false;
        }

        version = new PackageVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(PackageVersion other)
    {
        int result = Major.CompareTo(other.Major);
        if (result == 0)
        {
            result = Minor.CompareTo(other.Minor);
        }
        return result != 0 ? result : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => FormattableString.Invariant($"{Major:D3}.{Minor:D3}.{Patch:D3}");
}

internal enum UpdateStatus
{
    UpdateAvailable,
    UpToDate,
    InstalledNewer,
    NoPackage
}

// Keep the selected package with the comparison result for the future download phase.
internal sealed record ProductUpdate(InstalledProduct Product, RemoteEntry? Package,
    PackageVersion? AvailableVersion, UpdateStatus Status)
{
    public string StatusText => Status switch
    {
        UpdateStatus.UpdateAvailable => "Update available",
        UpdateStatus.UpToDate => "Up to date",
        UpdateStatus.InstalledNewer => "Installed version is newer",
        _ => "No package"
    };
}

internal static class UpdateComparison
{
    public static IReadOnlyList<ProductUpdate> Compare(LocalInstallation installation, IReadOnlyList<RemoteEntry> entries)
    {
        var results = new List<ProductUpdate>();
        foreach (InstalledProduct product in installation.Products)
        {
            if (!PackageVersion.TryParse(product.InstalledVersion, out PackageVersion installed))
            {
                throw new InvalidDataException($"Product '{product.Name}' has an invalid installed version.");
            }

            string prefix = product.Name + "_";
            RemoteEntry? selected = null;
            PackageVersion latest = default;
            foreach (RemoteEntry entry in entries)
            {
                // Product prefixes must match fully; dates do not select the version.
                if (entry.Type != "file" || !entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    !entry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                    entry.Path != installation.ClientFolder + "/" + entry.Name)
                {
                    continue;
                }

                string text = entry.Name[prefix.Length..^4];
                if (!PackageVersion.TryParse(text, out PackageVersion candidate))
                {
                    continue;
                }

                if (selected is null || candidate.CompareTo(latest) > 0 ||
                    (candidate == latest && string.CompareOrdinal(entry.Path, selected.Path) < 0))
                {
                    selected = entry;
                    latest = candidate;
                }
            }

            if (selected is null)
            {
                results.Add(new ProductUpdate(product, null, null, UpdateStatus.NoPackage));
                continue;
            }

            int comparison = latest.CompareTo(installed);
            UpdateStatus status = comparison > 0 ? UpdateStatus.UpdateAvailable
                : comparison == 0 ? UpdateStatus.UpToDate : UpdateStatus.InstalledNewer;
            results.Add(new ProductUpdate(product, selected, latest, status));
        }

        return results.AsReadOnly();
    }
}
