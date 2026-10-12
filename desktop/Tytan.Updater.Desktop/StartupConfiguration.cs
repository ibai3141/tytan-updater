using System.Text;

namespace Tytan.Updater.Desktop;

internal static class StartupConfiguration
{
    private static readonly HashSet<string> Keys = new(StringComparer.Ordinal)
    {
        "TYTAN_API_BASE_URL", "TYTAN_API_USERNAME", "TYTAN_API_PASSWORD"
    };

    internal static void Load()
    {
        // Resolve next to the executable, independently of the working directory.
        var values = ReadFile(Path.Combine(AppContext.BaseDirectory, ".env"));
        Apply(values, Environment.GetEnvironmentVariable, Environment.SetEnvironmentVariable);
    }

    internal static Dictionary<string, string> ReadFile(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string text;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > 65536)
                throw new InvalidDataException("The .env file must not exceed 64 KiB.");
            using var reader = new StreamReader(file, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
            text = reader.ReadToEnd().TrimStart('\uFEFF');
        }
        catch (FileNotFoundException)
        {
            // A configuration file is optional; defaults and environment still work.
            return values;
        }

        int lineNumber = 0;
        foreach (string source in text.Split('\n'))
        {
            lineNumber++;
            string line = source.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            int separator = line.IndexOf('=');
            string key = separator < 0 ? "" : line[..separator].Trim();
            if (!Keys.Contains(key) || values.ContainsKey(key))
                throw InvalidLine(lineNumber);

            string value = line[(separator + 1)..].Trim();
            if (value.StartsWith('"') || value.StartsWith('\''))
            {
                if (value.Length < 2 || value[^1] != value[0])
                    throw InvalidLine(lineNumber);
                value = value[1..^1];
            }
            if (value.Any(char.IsControl))
                throw InvalidLine(lineNumber);

            // Values are literal: '#' and '=' can be part of a password.
            // Do not expand variables, execute commands or parse inline comments.
            values.Add(key, value);
        }
        return values;
    }

    internal static void Apply(IReadOnlyDictionary<string, string> values,
        Func<string, string?> getVariable, Action<string, string?> setVariable)
    {
        // Parse the complete file first, then fill only unset process variables.
        foreach (var (key, value) in values)
        {
            if (getVariable(key) is null)
                setVariable(key, value);
        }
    }

    private static InvalidDataException InvalidLine(int number) =>
        new($"Invalid .env configuration at line {number}. Use a unique supported KEY=value entry with matching optional quotes.");
}
