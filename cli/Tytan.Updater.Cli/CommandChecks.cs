using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tytan.Updater.Desktop;

namespace Tytan.Updater.Cli;

internal static class CommandChecks
{
    internal static async Task<int> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "tytan-cli-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "installation.json");
        string json = """
            {"clientFolder":"TestClient","products":[{"name":"Faktury","installedVersion":"008.000.042"}]}
            """;
        await File.WriteAllTextAsync(source, json);
        int passed = 0;

        try
        {
            using var package = new MemoryStream();
            using (var zip = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            using (var writer = new StreamWriter(zip.CreateEntry("sample.txt").Open()))
            {
                writer.Write("CLI update fixture");
            }
            byte[] bytes = package.ToArray();

            async Task<(int Code, string Out, string Error, int Requests)> Run(string[] args,
                string version = "008.000.043", bool empty = false, bool unauthorized = false,
                bool invalidZip = false, string password = "test-password", CancellationToken token = default)
            {
                int requests = 0;
                var entry = new RemoteEntry("Faktury_" + version + ".zip", "file", bytes.Length,
                    "2026-10-09 10:00:00", "TestClient/Faktury_" + version + ".zip");
                using var http = new HttpClient(new Handler(request =>
                {
                    requests++;
                    if (request.Headers.Authorization?.Scheme != "Basic" ||
                        request.Headers.Authorization.Parameter != Convert.ToBase64String(Encoding.UTF8.GetBytes("test-user:test-password")))
                    {
                        throw new InvalidDataException("Unexpected authentication header.");
                    }
                    if (unauthorized) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    if (request.RequestUri!.AbsolutePath.EndsWith("api.php"))
                    {
                        if (request.RequestUri.Query != "?dir=TestClient") throw new InvalidDataException("Unexpected directory.");
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(empty ? Array.Empty<RemoteEntry>() : new[] { entry }),
                                Encoding.UTF8, "application/json")
                        };
                    }
                    if (Uri.UnescapeDataString(request.RequestUri.Query) != "?file=" + entry.Path)
                        throw new InvalidDataException("Unexpected package path.");
                    var content = new ByteArrayContent(invalidZip ? new byte[bytes.Length] : bytes);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                }));
                using var api = new CloudApiClient(http: http);
                using var output = new StringWriter();
                using var error = new StringWriter();
                int code = await Command.RunAsync(args, api, output, error,
                    () => new Credentials("test-user", password), token);
                return (code, output.ToString(), error.ToString(), requests);
            }

            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + name);
                passed++;
                Console.WriteLine("PASS: " + name);
            }

            string[] standard = ["TestClient", root];
            var result = await Run(standard, version: "008.000.042");
            Check(result.Code == 0 && result.Out.Contains("No updates available.") && result.Requests == 1 &&
                !Directory.Exists(Path.Combine(root, "downloads")), "equal version does not download");

            result = await Run(standard, version: "008.000.041");
            Check(result.Code == 0 && result.Out.Contains("Installed version is newer") && result.Requests == 1,
                "older server package does not downgrade");

            result = await Run(standard);
            string target = Path.Combine(root, "downloads", "Faktury_008.000.043.zip");
            Check(result.Code == 0 && result.Requests == 2 && File.ReadAllBytes(target).SequenceEqual(bytes) &&
                result.Out.Contains("Downloaded 1 update(s).") && await File.ReadAllTextAsync(source) == json,
                "newer ZIP downloads with authentication and leaves installed version unchanged");

            result = await Run(standard);
            Check(result.Code == 1 && result.Error.Contains("already exists") && File.ReadAllBytes(target).SequenceEqual(bytes),
                "existing ZIP is preserved");

            result = await Run(["WrongClient", root]);
            Check(result.Code == 1 && result.Requests == 0, "client mismatch is rejected before HTTP");

            result = await Run(standard, empty: true);
            Check(result.Code == 1 && result.Error.Contains("No matching package") && !result.Out.Contains("No updates available."),
                "missing package is not reported as up to date");

            result = await Run(standard, unauthorized: true);
            Check(result.Code == 1 && result.Error.Contains("401"), "authentication failure returns an error");

            result = await Run(standard, password: "");
            Check(result.Code == 1 && result.Requests == 0 && result.Error.Contains("TYTAN_API_PASSWORD"),
                "missing credentials prevent HTTP requests");

            string badOutput = Path.Combine(root, "invalid-download");
            result = await Run(["TestClient", root, "--output", badOutput], invalidZip: true);
            Check(result.Code == 1 && !Directory.EnumerateFileSystemEntries(badOutput).Any(),
                "invalid ZIP leaves no final or partial file");

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            result = await Run(standard, token: cancelled.Token);
            Check(result.Code == 130 && result.Requests == 0, "cancellation returns 130");

            result = await Run(["TestClient"]);
            Check(result.Code == 2 && result.Error.Contains("Usage:"), "invalid arguments return usage and code 2");

            result = await Run(["--help"]);
            Check(result.Code == 0 && result.Out.Contains("installation.json") && result.Requests == 0,
                "help runs without credentials or HTTP");

            Console.WriteLine($"All {passed} CLI checks passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        finally
        {
            // Remove only this run's owned temporary fixture directory.
            if (Path.GetDirectoryName(root) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()) &&
                Path.GetFileName(root).StartsWith("tytan-cli-check-", StringComparison.Ordinal))
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
