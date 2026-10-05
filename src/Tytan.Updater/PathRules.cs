namespace Tytan.Updater;

internal static class PathRules
{
    public static void ValidateSegment(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." ||
            value.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)) ||
            value.EndsWith('.') || value.EndsWith(' '))
            throw new ArgumentException($"{label}: nombre no válido.");
        var stem = value.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
             stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9'))
            throw new ArgumentException($"{label}: nombre reservado.");
    }

    public static void ValidatePackage(FileEntry entry, string folder)
    {
        ValidateSegment(folder, "Carpeta del cliente");
        ValidateSegment(entry.Name, "Archivo");
        if (entry.Path != folder + "/" + entry.Name || entry.Type != "file" || entry.Size is < 0)
            throw new InvalidDataException("El paquete no pertenece a la carpeta solicitada o contiene datos inválidos.");
    }
}
