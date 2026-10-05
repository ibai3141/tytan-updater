using System.Globalization;

namespace Tytan.Updater;

public readonly record struct PackageVersion(int Major, int Minor, int Patch) : IComparable<PackageVersion>
{
    public static bool TryParse(string? value, out PackageVersion version)
    {
        version = default;
        if (value is null) return false;
        var parts = value.Split('.');
        if (parts.Length != 3) return false;
        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || !parts[i].All(c => c is >= '0' and <= '9') ||
                !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return false;
        }
        version = new(numbers[0], numbers[1], numbers[2]);
        return true;
    }

    public int CompareTo(PackageVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;
        result = Minor.CompareTo(other.Minor);
        return result != 0 ? result : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major:D3}.{Minor:D3}.{Patch:D3}");
}
