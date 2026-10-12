using System.Text;

namespace Tytan.Updater.Desktop;

internal static class ConfigurationChecks
{
    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "tytan-env-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, ".env");
        int passed = 0;
        void Check(bool valid, string name)
        {
            if (!valid) throw new InvalidOperationException("FAIL: " + name);
            passed++;
            Console.WriteLine("PASS: " + name);
        }

        try
        {
            Check(StartupConfiguration.ReadFile(path).Count == 0, "missing .env is optional");
            File.WriteAllText(path, "# Comment\r\n\r\n TYTAN_API_USERNAME = SampleUser \r\nTYTAN_API_PASSWORD=secret#with=equals\r\nTYTAN_API_BASE_URL=https://example.test/updates/\r\n");
            var values = StartupConfiguration.ReadFile(path);
            Check(values.Count == 3 && values["TYTAN_API_USERNAME"] == "SampleUser" &&
                values["TYTAN_API_BASE_URL"] == "https://example.test/updates/", "comments, CRLF and surrounding whitespace are accepted");
            Check(values["TYTAN_API_PASSWORD"] == "secret#with=equals", "password hash and equals characters are literal");

            File.WriteAllText(path, "TYTAN_API_USERNAME='Quoted User'\nTYTAN_API_PASSWORD=\" spaces#=kept \"\n", new UTF8Encoding(true));
            values = StartupConfiguration.ReadFile(path);
            Check(values["TYTAN_API_USERNAME"] == "Quoted User" && values["TYTAN_API_PASSWORD"] == " spaces#=kept ",
                "UTF-8 BOM and matching quotes preserve literal spaces");
            File.WriteAllText(path, "TYTAN_API_PASSWORD=$OTHER_SECRET\\literal\n");
            Check(StartupConfiguration.ReadFile(path)["TYTAN_API_PASSWORD"] == "$OTHER_SECRET\\literal", "no variable or escape expansion");

            var process = new Dictionary<string, string?> { ["TYTAN_API_USERNAME"] = "EnvironmentUser" };
            StartupConfiguration.Apply(new Dictionary<string, string>
            {
                ["TYTAN_API_USERNAME"] = "FileUser", ["TYTAN_API_BASE_URL"] = "https://file.example.test/"
            }, key => process.GetValueOrDefault(key), (key, value) => process[key] = value);
            Check(process["TYTAN_API_USERNAME"] == "EnvironmentUser", "environment overrides .env");
            Check(process["TYTAN_API_BASE_URL"] == "https://file.example.test/", ".env fills unset environment values");
            Check(!process.ContainsKey("TYTAN_API_PASSWORD"), "omitted password is not generated or persisted");

            foreach (string invalid in new[]
            {
                "TYTAN_API_PASSWORD", "UNKNOWN_KEY=secret", "TYTAN_API_USERNAME=first\nTYTAN_API_USERNAME=second",
                "TYTAN_API_PASSWORD=\"secret", "TYTAN_API_PASSWORD='secret\"", "TYTAN_API_PASSWORD=\"secret\tvalue\""
            })
            {
                File.WriteAllText(path, invalid);
                bool rejected = false;
                try { StartupConfiguration.ReadFile(path); }
                catch (InvalidDataException error)
                {
                    rejected = error.Message.Contains("line ") && !error.Message.Contains("secret");
                }
                Check(rejected, "invalid .env rejected without exposing its contents");
            }

            File.WriteAllText(path, new string('x', 65537));
            bool oversized = false;
            try { StartupConfiguration.ReadFile(path); }
            catch (InvalidDataException) { oversized = true; }
            Check(oversized, "oversized configuration is rejected");

            foreach (byte[] invalidBytes in new byte[][] { [0xFF], [0xFF, 0xFE, 0x41, 0x00] })
            {
                File.WriteAllBytes(path, invalidBytes);
                bool invalidEncoding = false;
                try { StartupConfiguration.ReadFile(path); }
                catch (DecoderFallbackException) { invalidEncoding = true; }
                if (!invalidEncoding) throw new InvalidOperationException("Non-UTF-8 configuration was accepted.");
            }
            Check(true, "invalid UTF-8 and UTF-16 are rejected");

            Console.WriteLine($"All {passed} configuration checks passed. No real credentials or process variables were changed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        finally
        {
            // Only this invocation's uniquely created temporary directory is removed.
            Directory.Delete(root, recursive: true);
        }
    }
}
