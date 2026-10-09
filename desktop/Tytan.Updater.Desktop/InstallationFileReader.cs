using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Tytan.Updater.Desktop;

// The UI consumes these models, independently of the provisional JSON format.
internal sealed record InstalledProduct(string Name, string InstalledVersion);

internal sealed record LocalInstallation(string ClientFolder, IReadOnlyList<InstalledProduct> Products);

internal static class InstallationFileReader
{
    public static LocalInstallation FromFolderNames(string clientFolder, string installedFolder)
    {
        if (!ValidName(clientFolder))
        {
            throw new InvalidDataException("Enter a valid client folder name.");
        }

        // Product names can contain underscores; only the last separates the version.
        int separator = installedFolder.LastIndexOf('_');
        if (!ValidName(installedFolder) || separator <= 0 || !ValidName(installedFolder[..separator]) ||
            !PackageVersion.TryParse(installedFolder[(separator + 1)..], out _))
        {
            throw new InvalidDataException("Enter an installed folder name such as Faktury_008.000.042 (product_NNN.NNN.NNN).");
        }

        return new LocalInstallation(clientFolder,
            new[] { new InstalledProduct(installedFolder[..separator], installedFolder[(separator + 1)..]) });
    }

    public static LocalInstallation Read(string path)
    {
        string json = File.ReadAllText(path);
        return Parse(json);
    }

    public static LocalInstallation Parse(string json)
    {
        // Keep all mapping from the provisional file format in this reader.
        var options = new JsonSerializerOptions
        {
            MaxDepth = 16,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        FileData? data;
        try
        {
            data = JsonSerializer.Deserialize<FileData>(json, options);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The installation file does not match the expected JSON format.", error);
        }

        if (data is null || !ValidName(data.ClientFolder))
        {
            throw new InvalidDataException("The installation file must contain a valid clientFolder name.");
        }

        if (data.Products is null || data.Products.Count == 0)
        {
            throw new InvalidDataException("The installation file must contain at least one product.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var products = new List<InstalledProduct>();

        foreach (ProductData? product in data.Products)
        {
            if (product is null || !ValidName(product.Name))
            {
                throw new InvalidDataException("Every product must have a valid name.");
            }

            if (!names.Add(product.Name!))
            {
                throw new InvalidDataException($"Product '{product.Name}' occurs more than once.");
            }

            if (!PackageVersion.TryParse(product.InstalledVersion, out _))
            {
                throw new InvalidDataException($"Product '{product.Name}' must have an installedVersion such as 008.000.042.");
            }

            products.Add(new InstalledProduct(product.Name!, product.InstalledVersion!));
        }

        return new LocalInstallation(data.ClientFolder!, products.AsReadOnly());
    }

    internal static bool ValidName(string? name)
    {
        // One folder/name segment: never accept an absolute path or traversal.
        return !string.IsNullOrWhiteSpace(name) && name[0] != '.' &&
            name == name.TrimEnd(' ', '.') &&
            !Regex.IsMatch(name, "[\\x00-\\x1F\\x7F<>:\"\\\\|?*/]");
    }

    private sealed class FileData
    {
        [JsonPropertyName("clientFolder")]
        public string? ClientFolder { get; set; }

        [JsonPropertyName("products")]
        public List<ProductData?>? Products { get; set; }
    }

    private sealed class ProductData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("installedVersion")]
        public string? InstalledVersion { get; set; }
    }
}
