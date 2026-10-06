using System.Net;
using System.Text;
using System.Text.Json;

namespace Tytan.Updater.Desktop;

internal static class CloudChecks
{
    private static int count;
    private static readonly RemoteEntry Package = new("Faktury_008.000.043.zip", "file", 17492922,
        "2026-10-02 06:57:29", "Barcin_Wodbar/Faktury_008.000.043.zip");

    public static int Run(bool live = false)
    {
        count = 0;
        try
        {
            if (!live)
            {
                // Keep asynchronous transport tests away from the WinForms context.
                Task.Run(CheckTransportAsync).GetAwaiter().GetResult();
            }
            CheckWindow(live);
            Console.WriteLine(live ? "PASS live desktop API listing. No ZIP was downloaded."
                : $"PASS {count} cloud checks using simulated responses. No production request was made.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
    }

    private static async Task CheckTransportAsync()
    {
        using var http = new HttpClient(new Handler((request, _) =>
        {
            Require(request.Method == HttpMethod.Get && request.RequestUri!.Query == "?dir=Client%20%26%20more" &&
                request.RequestUri.AbsolutePath == "/SQLupdate/api.php" &&
                request.Headers.Authorization?.ToString() == "Basic dGVzdC11c2VyOnRlc3QtcGFzc3dvcmQ=",
                "HTTPS folder query, encoded name and BasicAuth");
            RemoteEntry entry = Package with { Path = "Client & more/" + Package.Name };
            return Task.FromResult(Json(JsonSerializer.Serialize(new[] { entry })));
        }));
        using var api = new CloudApiClient(http: http);
        var entries = await api.ListAsync("Client & more", "test-user", "test-password", CancellationToken.None);
        Require(entries.Count == 1 && entries[0].Size == 17492922, "Lowercase JSON metadata maps correctly");

        foreach (HttpStatusCode status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError, HttpStatusCode.Redirect })
        {
            using var errorHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
            using var errorApi = new CloudApiClient(http: errorHttp);
            try
            {
                await errorApi.ListAsync("Barcin_Wodbar", "test-user", "test-password", CancellationToken.None);
                throw new Exception("HTTP failure was accepted.");
            }
            catch (HttpRequestException error) when (error.StatusCode == status)
            {
                Require(!error.Message.Contains("test-password"), "HTTP " + (int)status + " is reported without credentials");
            }
        }

        foreach (string json in new[] { "{", "null", "[null]", JsonSerializer.Serialize(new[] { Package with { Path = "other/" + Package.Name } }),
            JsonSerializer.Serialize(new[] { Package with { Size = -1 } }),
            JsonSerializer.Serialize(new[] { Package with { Modified = "invalid" } }) })
        {
            using var invalidHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Json(json))));
            using var invalidApi = new CloudApiClient(http: invalidHttp);
            await RejectAsync(() => invalidApi.ListAsync("Barcin_Wodbar", "test-user", "test-password", CancellationToken.None));
        }
        Require(true, "Malformed JSON, null entries, cross-client paths and invalid metadata rejected");

        using var emptyHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Json("[]"))));
        using var emptyApi = new CloudApiClient(http: emptyHttp);
        Require((await emptyApi.ListAsync("Barcin_Wodbar", "test-user", "test-password", CancellationToken.None)).Count == 0,
            "Empty cloud folder is accepted");
        await RejectAsync(() => emptyApi.ListAsync("../other", "test-user", "test-password", CancellationToken.None));
        await RejectAsync(() => emptyApi.ListAsync("Barcin_Wodbar", "test-user", "", CancellationToken.None));
        Require(true, "Invalid local folder and missing credentials rejected before HTTP");

        using var htmlHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Not an API</html>", Encoding.UTF8, "text/html")
        })));
        using var htmlApi = new CloudApiClient(http: htmlHttp);
        await RejectAsync(() => htmlApi.ListAsync("Barcin_Wodbar", "test-user", "test-password", CancellationToken.None));
        Require(true, "Successful HTML response rejected");

        try
        {
            using var unsafeApi = new CloudApiClient(new Uri("http://example.test/SQLupdate/"));
            throw new Exception("HTTP base URL was accepted.");
        }
        catch (ArgumentException)
        {
            Require(true, "Production client requires HTTPS");
        }
    }

    private static void CheckWindow(bool live)
    {
        int requests = 0;
        using var simulatedHttp = new HttpClient(new Handler(async (_, token) =>
        {
            requests++;
            if (requests == 2)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            if (requests == 3)
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            if (requests == 4)
            {
                throw new TaskCanceledException("Simulated deadline.");
            }
            return Json(JsonSerializer.Serialize(new[] { Package }));
        }));
        using var api = live ? new CloudApiClient() : new CloudApiClient(http: simulatedHttp);
        using var window = new MainForm(api: api);
        Exception? failure = null;

        window.Shown += async (_, _) =>
        {
            try
            {
                window.LoadInstallation(Path.Combine(AppContext.BaseDirectory, "installation.example.json"), true);
                string user = live ? Environment.GetEnvironmentVariable("TYTAN_API_USERNAME") ?? "" : "test-user";
                string password = live ? Environment.GetEnvironmentVariable("TYTAN_API_PASSWORD") ?? "" : "test-password";
                if (user == "" || password == "")
                {
                    throw new Exception("Set TYTAN_API_USERNAME and TYTAN_API_PASSWORD before live verification.");
                }
                window.SetCredentials(user, password);
                await window.QueryCloudAsync();
                Require(window.CloudEntryCount > 0 && !window.QueryBusy, "Window displays cloud entries and releases busy state");
                Require(window.ProductCount == 3 && window.DisplayedVersion(0) == "008.000.042",
                    "Cloud listing leaves installed versions unchanged");
                Capture(window);

                if (!live)
                {
                    await window.QueryCloudAsync();
                    Require(window.CloudEntryCount == 0 && window.StatusText.Contains("401") && !window.QueryBusy,
                        "Window reports rejected credentials and clears stale cloud results");
                    Task pending = window.QueryCloudAsync();
                    Require(window.QueryBusy, "Pending query enters busy state");
                    window.CancelCloudQuery();
                    await pending;
                    Require(window.StatusText == "Cloud request cancelled." && !window.QueryBusy, "Cancellation restores window controls");
                    await window.QueryCloudAsync();
                    Require(window.StatusText.Contains("timed out") && !window.QueryBusy, "Timeout is distinct from user cancellation");
                }
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                window.Close();
            }
        };

        Application.Run(window);
        if (failure is not null)
        {
            throw failure;
        }
    }

    private static void Capture(Form window)
    {
        string? path = Environment.GetEnvironmentVariable("TYTAN_DESKTOP_CAPTURE");
        if (!string.IsNullOrEmpty(path))
        {
            using var bitmap = new Bitmap(window.Width, window.Height);
            window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private static async Task RejectAsync(Func<Task<IReadOnlyList<RemoteEntry>>> action)
    {
        try
        {
            await action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new Exception("Invalid input or listing was accepted.");
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static void Require(bool condition, string description)
    {
        if (!condition)
        {
            throw new Exception(description);
        }
        count++;
        Console.WriteLine("PASS " + description);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}
