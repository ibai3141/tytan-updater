using System.Net;
using System.IO.Compression;
using Tytan.Updater;

// Optional tests run by tests/server/test_endpoints.py against actual local PHP.
internal static class PhpIntegrationTests
{
    public static void Register(Action<string, Func<Task>> register)
    {
        var address = Environment.GetEnvironmentVariable("TYTAN_TEST_PHP_BASE_URL");
        if (address is null)
        {
            return;
        }

        var localUri = new Uri(address);
        if (localUri.Scheme != "http" || localUri.Host != "127.0.0.1")
        {
            throw new ArgumentException("PHP integration tests require an HTTP loopback server.");
        }

        register("Actual PHP listing maps into the C# file model", async () =>
        {
            using var api = CreateClient(localUri);
            var entries = await api.ListAsync("cliente");
            var selected = PackageSelector.SelectNewest(entries, "Faktury");
            if (selected?.Version.ToString() != "008.000.043" || selected.File.Size is not > 0)
            {
                throw new Exception("Unexpected PHP package metadata.");
            }
        });

        register("C# downloads a real package from PHP and detects no update", async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "tytan-php-client-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                using var api = CreateClient(localUri);
                var service = new UpdateService(api);
                var result = await service.CheckAndDownloadAsync(new("cliente", "Faktury", "008.000.042", directory));
                if (result.Status != UpdateStatus.Downloaded || result.LocalPath is null)
                {
                    throw new Exception("PHP download failed: " + result.Message);
                }

                using (var archive = ZipFile.OpenRead(result.LocalPath))
                using (var reader = new StreamReader(archive.GetEntry("example.txt")!.Open()))
                {
                    if (await reader.ReadToEndAsync() != "PHP integration sample")
                    {
                        throw new Exception("Unexpected package contents.");
                    }
                }

                var noUpdate = await service.CheckAndDownloadAsync(new("cliente", "Faktury", "008.000.043", directory));
                if (noUpdate.Status != UpdateStatus.NoUpdate)
                {
                    throw new Exception("An equal installed version must not download.");
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    private static UpdateApiClient CreateClient(Uri localUri) => new(
        new Uri("https://php.test/SQLupdate/"), "test-user", "test-password", new LoopbackHandler(localUri));

    // Only test transport changes HTTPS to local HTTP. Production code is unchanged.
    private sealed class LoopbackHandler(Uri localUri) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri = new Uri(localUri, request.RequestUri!.PathAndQuery.TrimStart('/'));
            return base.SendAsync(request, cancellationToken);
        }
    }
}
