using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace Tytan.Updater.Desktop;

internal static class DownloadChecks
{
    private static int count;

    public static int Run(bool live = false)
    {
        count = 0;
        string directory = Path.Combine(Path.GetTempPath(), "tytan-download-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            if (!live)
            {
                Task.Run(() => CheckTransferAsync(directory)).GetAwaiter().GetResult();
            }
            CheckWindow(directory, live);
            Console.WriteLine(live ? "PASS real API ZIP downloaded and read successfully. No installation was performed."
                : $"PASS {count} download checks. No production request was made.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
        finally
        {
            // This directory is created here and contains only disposable check files.
            string resolved = Path.GetFullPath(directory);
            string temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("tytan-download-checks-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unexpected test cleanup directory.");
            }
            Directory.Delete(resolved, recursive: true);
        }
    }

    private static byte[] SampleZip()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("example.txt").Open());
            writer.Write("Local download check sample.");
        }
        return buffer.ToArray();
    }

    private static ProductUpdate Update(long size) => new(new InstalledProduct("Faktury", "008.000.042"),
        new RemoteEntry("Faktury_008.000.043.zip", "file", size, "2026-10-02 06:57:29", "Barcin_Wodbar/Faktury_008.000.043.zip"),
        new PackageVersion(8, 0, 43), UpdateStatus.UpdateAvailable);

    private static HttpResponseMessage Binary(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("application/octet-stream");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static async Task CheckTransferAsync(string directory)
    {
        byte[] bytes = SampleZip();
        using var http = new HttpClient(new Handler((request, _) =>
        {
            Check(request.RequestUri!.AbsolutePath == "/SQLupdate/download.php" &&
                request.RequestUri.Query == "?file=Barcin_Wodbar%2FFaktury_008.000.043.zip" &&
                request.Headers.Authorization?.Scheme == "Basic", "ZIP endpoint, encoded path and BasicAuth");
            return Task.FromResult(Binary(bytes));
        }));
        using var api = new CloudApiClient(http: http);
        string target = Path.Combine(directory, "complete.zip");
        var reports = new List<DownloadProgress>();
        string saved = await api.DownloadAsync("Barcin_Wodbar", Update(bytes.Length), target, "test-user", "test-password",
            new InlineProgress(value => reports.Add(value)), CancellationToken.None);
        Check(saved == target && File.ReadAllBytes(target).SequenceEqual(bytes) && !Directory.EnumerateFiles(directory, "*.part").Any(),
            "Complete ZIP published byte-for-byte without leftover partials");
        Check(reports.Any(value => value.Stage == "Validating ZIP") && reports.Last().Stage == "Downloaded" && reports.Last().Percent == 100,
            "Progress includes validation and successful completion");
        Check(reports.Where(value => value.Stage != "Downloaded").All(value => value.Percent < 100),
            "Progress stays below 100 until validation and final publication finish");
        await ExpectFailure(() => api.DownloadAsync("Barcin_Wodbar", Update(bytes.Length), target, "test-user", "test-password", null, CancellationToken.None));
        Check(File.ReadAllBytes(target).SequenceEqual(bytes), "Existing destination remains unchanged");
        await ExpectFailure(() => api.DownloadAsync("other", Update(bytes.Length), Path.Combine(directory, "other.zip"), "test-user", "test-password", null, CancellationToken.None));
        await ExpectFailure(() => api.DownloadAsync("Barcin_Wodbar", Update(bytes.Length) with { Status = UpdateStatus.UpToDate },
            Path.Combine(directory, "equal.zip"), "test-user", "test-password", null, CancellationToken.None));
        Check(true, "Cross-client and non-update downloads rejected");

        async Task FailureCase(string name, Func<HttpResponseMessage> response)
        {
            using var failureHttp = new HttpClient(new Handler((_, _) => Task.FromResult(response())));
            using var failureApi = new CloudApiClient(http: failureHttp);
            string path = Path.Combine(directory, name + ".zip");
            await ExpectFailure(() => failureApi.DownloadAsync("Barcin_Wodbar", Update(bytes.Length), path,
                "test-user", "test-password", null, CancellationToken.None));
            Check(!File.Exists(path) && !Directory.EnumerateFiles(directory, "*.part").Any(), name + " never publishes an incomplete package");
        }

        await FailureCase("HTTP401", () => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        await FailureCase("Redirect", () => new HttpResponseMessage(HttpStatusCode.Redirect));
        await FailureCase("Partial HTTP response", () =>
        {
            var response = Binary(bytes);
            response.StatusCode = HttpStatusCode.PartialContent;
            return response;
        });
        await FailureCase("Header size mismatch", () => Binary(bytes[..^1]));
        await FailureCase("Truncated body", () =>
        {
            var response = Binary(bytes[..^1]);
            response.Content.Headers.ContentLength = bytes.Length;
            return response;
        });
        await FailureCase("Oversized body", () =>
        {
            var response = Binary(bytes.Concat(new byte[] { 0 }).ToArray());
            response.Content.Headers.ContentLength = bytes.Length;
            return response;
        });
        await FailureCase("Invalid ZIP", () => Binary(new byte[bytes.Length]));
        await FailureCase("Interrupted stream", () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream(bytes)) };
            response.Content.Headers.ContentType = new("application/octet-stream");
            return response;
        });

        using var cancellation = new CancellationTokenSource();
        string cancelled = Path.Combine(directory, "cancelled.zip");
        await ExpectFailure(() => api.DownloadAsync("Barcin_Wodbar", Update(bytes.Length), cancelled, "test-user", "test-password",
            new InlineProgress(value => { if (value.Stage == "Downloading") cancellation.Cancel(); }), cancellation.Token));
        Check(!File.Exists(cancelled) && !Directory.EnumerateFiles(directory, "*.part").Any(), "Cancellation removes the partial file");

        string raced = Path.Combine(directory, "raced.zip");
        using var raceHttp = new HttpClient(new Handler((_, _) =>
        {
            File.WriteAllText(raced, "Existing concurrent file");
            return Task.FromResult(Binary(bytes));
        }));
        using var raceApi = new CloudApiClient(http: raceHttp);
        await ExpectFailure(() => raceApi.DownloadAsync("Barcin_Wodbar", Update(bytes.Length), raced, "test-user", "test-password", null, CancellationToken.None));
        Check(File.ReadAllText(raced) == "Existing concurrent file" && !Directory.EnumerateFiles(directory, "*.part").Any(),
            "Concurrent destination creation is preserved");
    }

    private static void CheckWindow(string directory, bool live)
    {
        byte[] bytes = SampleZip();
        int downloads = 0;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("api.php"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new[] { Update(bytes.Length).Package }), System.Text.Encoding.UTF8, "application/json")
                });
            }
            downloads++;
            return Task.FromResult(Binary(downloads == 1 ? bytes : new byte[bytes.Length]));
        }));
        using var api = live ? new CloudApiClient() : new CloudApiClient(http: http);
        using var window = new MainForm(api: api);
        Exception? failure = null;
        window.Shown += async (_, _) =>
        {
            try
            {
                string sample = Path.Combine(AppContext.BaseDirectory, "installation.example.json");
                byte[] original = File.ReadAllBytes(sample);
                window.LoadInstallation(sample, true);
                window.SetCredentials(live ? Environment.GetEnvironmentVariable("TYTAN_API_USERNAME") ?? "" : "test-user",
                    live ? Environment.GetEnvironmentVariable("TYTAN_API_PASSWORD") ?? "" : "test-password");
                await window.QueryCloudAsync();
                Check(window.SelectedUpdate is not null, "Window selects a newer product for download");
                string target = Path.Combine(directory, "Faktury_008.000.043.zip");
                string? result = await window.DownloadSelectedAsync(target);
                Check(result == target && File.Exists(target) && !window.QueryBusy, "Window download completes and releases busy state");
                Check(window.DownloadedPath == target && window.DownloadPercent == 100 && window.StatusText.Contains("Automatic installation is not available"),
                    "Completed UI exposes the saved ZIP path and clarifies installation is unavailable");
                using (var archive = ZipFile.OpenRead(target))
                {
                    Check(archive.Entries.Count > 0, "Saved ZIP is readable");
                }
                Check(window.DisplayedVersion(0) == "008.000.042" && File.ReadAllBytes(sample).SequenceEqual(original),
                    "Download leaves installed versions and their file unchanged");
                string? capture = Environment.GetEnvironmentVariable("TYTAN_DESKTOP_CAPTURE");
                if (!string.IsNullOrEmpty(capture))
                {
                    using var bitmap = new Bitmap(window.Width, window.Height);
                    window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
                    bitmap.Save(capture, System.Drawing.Imaging.ImageFormat.Png);
                }
                if (!live)
                {
                    string invalid = Path.Combine(directory, "invalid-window.zip");
                    string? rejected = await window.DownloadSelectedAsync(invalid);
                    Check(rejected is null && !File.Exists(invalid) && !window.QueryBusy &&
                        !Directory.EnumerateFiles(directory, "*.part").Any() && File.Exists(target),
                        "Window handles ZIP validation errors without publishing or losing the prior download");
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

    private static async Task ExpectFailure(Func<Task<string>> action)
    {
        try
        {
            await action();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or HttpRequestException or OperationCanceledException)
        {
            return;
        }
        throw new Exception("Invalid or interrupted download unexpectedly succeeded.");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
        {
            throw new Exception(description);
        }
        count++;
        Console.WriteLine("PASS " + description);
    }

    private sealed class InlineProgress(Action<DownloadProgress> action) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => action(value);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => respond(request, token);
    }

    private sealed class BrokenStream(byte[] bytes) : MemoryStream(bytes)
    {
        private bool read;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (read)
            {
                throw new IOException("Simulated interrupted stream.");
            }
            read = true;
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, 10)], token);
        }
    }
}
