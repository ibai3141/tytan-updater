using System.Net;
using System.Text;
using System.Text.Json;

namespace Tytan.Updater.Desktop;

internal static class ManualInputChecks
{
    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "tytan-manual-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settings = Path.Combine(root, "folders.json");
        int count = 0;
        Exception? failure = null;
        int requests = 0;
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
                Check(window.ClientFolderInput == "" && window.InstalledFolderInput == "" && window.ProductCount == 0,
                    "First launch does not silently load example installations");
                window.SetFolderInputs("Barcin_Wodbar", "Faktury_008.000.042");
                window.SetCredentials("test-user", "test-password");
                await window.QueryCloudAsync();
                Check(window.ProductCount == 1 && window.DisplayedVersion(0) == "008.000.042" &&
                    window.DisplayedStatus("Faktury") == "Update available" && window.SelectedUpdate is not null,
                    "Direct folder names query and compare without loading any installation file");
                FolderPreferences saved = FolderPreferences.Read(settings)!;
                Check(saved.ClientFolder == "Barcin_Wodbar" && saved.InstalledFolder == "Faktury_008.000.042",
                    "Successful check remembers the two folder names");
                string savedText = File.ReadAllText(settings);
                Check(!savedText.Contains("test-password") && !savedText.Contains("test-user") &&
                    JsonDocument.Parse(savedText).RootElement.EnumerateObject().Count() == 2,
                    "Preferences contain only folder names and no credentials");

                window.SetFolderInputs("Barcin_Wodbar", "invalid");
                Check(window.CloudEntryCount == 0 && window.ComparedProductCount == 0 && window.SelectedUpdate is null,
                    "Editing folder names clears stale comparison and download selection");
                int before = requests;
                await window.QueryCloudAsync();
                Check(requests == before && window.StatusText.Contains("installed folder") && File.ReadAllText(settings) == savedText,
                    "Invalid folder names prevent HTTP and preserve previous saved settings");

                window.SetFolderInputs("Barcin_Wodbar", "Faktury_008.000.043");
                window.SetCredentials("test-user", "bad-password");
                await window.QueryCloudAsync();
                Check(window.StatusText.Contains("401") && File.ReadAllText(settings) == savedText,
                    "Rejected credentials do not replace remembered folder names");

                using var restoredHttp = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new[] { entry }), Encoding.UTF8, "application/json")
                }));
                using var restoredApi = new CloudApiClient(http: restoredHttp);
                using var restored = new MainForm(api: restoredApi, settingsPath: settings);
                restored.Show();
                Check(restored.ClientFolderInput == "Barcin_Wodbar" && restored.InstalledFolderInput == "Faktury_008.000.042",
                    "Next launch restores the customer's two folder names");
                restored.SetCredentials("test-user", "test-password");
                await restored.QueryCloudAsync();
                Check(restored.DisplayedStatus("Faktury") == "Update available",
                    "Restored configuration needs only credentials and a check");
                restored.SetFolderInputs("Barcin_Wodbar", "Faktury_008.000.043");
                await restored.QueryCloudAsync();
                Check(restored.DisplayedStatus("Faktury") == "Up to date" && restored.SelectedUpdate is null &&
                    FolderPreferences.Read(settings)!.InstalledFolder == "Faktury_008.000.043",
                    "Editing the actual installed version updates comparison and remembered names");
                restored.Close();

                File.WriteAllText(settings, "{");
                using var corrupt = new MainForm(settingsPath: settings);
                Check(corrupt.ClientFolderInput == "" && corrupt.InstalledFolderInput == "" &&
                    corrupt.StatusText.Contains("could not be loaded"),
                    "Corrupt preferences recover to empty inputs without example data");

                string? capture = Environment.GetEnvironmentVariable("TYTAN_MANUAL_CAPTURE");
                if (!string.IsNullOrEmpty(capture))
                {
                    window.SetCredentials("test-user", "test-password");
                    window.SetFolderInputs("Barcin_Wodbar", "Faktury_008.000.042");
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
            Console.WriteLine($"PASS {count} manual-input and remembered-folder checks. No production request was made.");
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
                Path.GetFileName(root).StartsWith("tytan-manual-check-", StringComparison.Ordinal))
                Directory.Delete(root, recursive: true);
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
