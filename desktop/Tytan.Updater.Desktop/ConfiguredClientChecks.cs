using System.Net;
using System.Text;
using System.Text.Json;

namespace Tytan.Updater.Desktop;

internal static class ConfiguredClientChecks
{
    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "tytan-configured-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settings = Path.Combine(root, "folders.json");
        new FolderPreferences("Barcin_Wodbar", "Faktury_008.000.042").Save(settings);
        string original = File.ReadAllText(settings);
        int count = 0;
        int requests = 0;
        Exception? failure = null;
        var entry = new RemoteEntry("Faktury_008.000.043.zip", "file", 123, "2026-10-09 10:00:00",
            "Barcin_Wodbar/Faktury_008.000.043.zip");
        using var http = new HttpClient(new Handler(request =>
        {
            requests++;
            if (request.RequestUri!.Query != "?dir=Barcin_Wodbar") throw new Exception("Wrong client folder queried.");
            bool rejected = request.Headers.Authorization?.Parameter ==
                Convert.ToBase64String(Encoding.UTF8.GetBytes("test-user:bad-password"));
            return new HttpResponseMessage(rejected ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new[] { entry }), Encoding.UTF8, "application/json")
            };
        }));
        using var api = new CloudApiClient(http: http);
        using var window = new MainForm(api: api, settingsPath: settings);

        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            count++;
            Console.WriteLine("PASS " + name);
        }

        window.Shown += async (_, _) =>
        {
            try
            {
                Check(window.LoadedClient == "Barcin_Wodbar" && window.ProductCount == 1 &&
                    window.DisplayedVersion(0) == "008.000.042", "Startup loads only this computer's configured customer and version");
                Check(Descendants(window).OfType<TextBox>().Count() == 2 &&
                    !Descendants(window).OfType<Button>().Any(button => button.Text.Contains("Import") || button.Text.Contains("example")),
                    "Window contains only credential inputs and no folder/example/import controls");
                window.SetCredentials("test-user", "test-password");
                await window.QueryCloudAsync();
                Check(window.DisplayedStatus("Faktury") == "Update available" && window.SelectedUpdate is not null && requests == 1,
                    "Credentials and a check query the configured client without entering folder names");
                Check(File.ReadAllText(settings) == original && !original.Contains("test-password") && !original.Contains("test-user"),
                    "Checking updates neither rewrites configuration nor persists credentials");

                window.SetCredentials("test-user", "bad-password");
                await window.QueryCloudAsync();
                Check(window.StatusText.Contains("401") && window.SelectedUpdate is null && window.ComparedProductCount == 0,
                    "Rejected credentials clear stale update and download selection");

                using var restarted = new MainForm(settingsPath: settings);
                Check(restarted.LoadedClient == "Barcin_Wodbar" && restarted.DisplayedVersion(0) == "008.000.042",
                    "Relaunch loads the same customer without a GUI setup step");

                int before = requests;
                using var missingApi = new CloudApiClient(http: http);
                using var missing = new MainForm(api: missingApi, settingsPath: Path.Combine(root, "missing.json"));
                missing.SetCredentials("test-user", "test-password");
                await missing.QueryCloudAsync();
                Check(missing.ProductCount == 0 && missing.StatusText.Contains("administrator") && requests == before,
                    "Missing configuration reports setup requirement without querying root or loading examples");

                foreach (string invalid in new[] { "{", "{}",
                    "{\"ClientFolder\":\"../other\",\"InstalledFolder\":\"Faktury_008.000.042\"}",
                    "{\"ClientFolder\":\"Barcin_Wodbar\",\"InstalledFolder\":\"invalid\"}" })
                {
                    File.WriteAllText(settings, invalid);
                    using var invalidApi = new CloudApiClient(http: http);
                    using var corrupt = new MainForm(api: invalidApi, settingsPath: settings);
                    await corrupt.QueryCloudAsync();
                    Check(corrupt.ProductCount == 0 && corrupt.StatusText.Contains("administrator") && requests == before,
                        "Invalid configured customer/version prevents HTTP: " + invalid);
                }

                string? capture = Environment.GetEnvironmentVariable("TYTAN_CONFIGURED_CAPTURE");
                if (!string.IsNullOrEmpty(capture))
                {
                    window.SetCredentials("test-user", "test-password");
                    await window.QueryCloudAsync();
                    using var bitmap = new Bitmap(window.Width, window.Height);
                    window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
                    bitmap.Save(capture);
                }
            }
            catch (Exception error) { failure = error; }
            finally { window.Close(); }
        };

        try
        {
            Application.Run(window);
            if (failure is not null) throw failure;
            Console.WriteLine($"PASS {count} configured-client checks. No production request was made.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        finally
        {
            if (Path.GetDirectoryName(root) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()) &&
                Path.GetFileName(root).StartsWith("tytan-configured-check-", StringComparison.Ordinal))
                Directory.Delete(root, recursive: true);
        }
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
