using Tytan.Updater;
using System.Net;
using System.Text;
using System.IO.Compression;
using System.Text.Json;

// Register test cases in a lightweight runner without external packages.
var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Action run) => tests.Add((name, () =>
{
    run();
    return Task.CompletedTask;
}
));
void AsyncTest(string name, Func<Task> run) => tests.Add((name, run));

// Fail the current case with the expected and actual values.
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new Exception($"Expected {expected}; actual {actual}");
    }
}
FileEntry Entry(string name, string type = "file") => new(name, type, null, null, "cliente/" + name);

// Version parsing and selection tests use local listing data only.
Test("Version components compare numerically", () =>
{
    foreach (var pair in new[] { ("010.000.000", "009.999.999"), ("008.010.000", "008.009.999"), ("008.000.100", "008.000.099") })
    {
        Equal(true, PackageVersion.TryParse(pair.Item1, out var higher));
        Equal(true, PackageVersion.TryParse(pair.Item2, out var lower));
        Equal(true, higher.CompareTo(lower) > 0);
    }
    PackageVersion.TryParse("8.0.43", out var plain);
    PackageVersion.TryParse("008.000.043", out var padded);
    Equal(plain, padded);
});

Test("Invalid versions are rejected", () =>
{
    foreach (var value in new string?[] { null, "", "1.2", "1.2.3.4", "-1.0.0", "+1.0.0", " 1.0.0", "1.a.0", "99999999999.0.0" })
    {
        Equal(false, PackageVersion.TryParse(value, out _));
    }
});

Test("Selection isolates product and ignores dates and malformed names", () =>
{
    var files = new[] {
        Entry("Faktury_008.000.043.zip") with { Modified = "2099-01-01" },
        Entry("Faktury_008.002.066.zip"), Entry("FK2025_999.000.000.zip"),
        Entry("FakturyExtra_999.000.000.zip"), Entry("Faktury_999.000.000.zip", "folder"),
        Entry("Faktury_latest.zip"), Entry("Faktury_999.000.000.exe") };
    Equal("008.002.066", PackageSelector.SelectNewest(files, "Faktury")?.Version.ToString());
    Equal<UpdatePackage?>(null, PackageSelector.SelectNewest(files, "Unknown"));
    Equal<UpdatePackage?>(null, PackageSelector.SelectNewest([], "Faktury"));
});

// HTTP tests verify requests and responses through injected handlers.
AsyncTest("API uses BasicAuth, encodes folder and maps lowercase JSON", async () =>
{
    using var client = new UpdateApiClient(new Uri("https://example.test/SQLupdate"), "test", "secret",
        new StubHandler((request, _) =>
        {
            Equal("https://example.test/SQLupdate/api.php?dir=Cliente%20%26%20uno", request.RequestUri!.AbsoluteUri);
            Equal("Basic", request.Headers.Authorization?.Scheme);
            Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("test:secret")), request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                """[{"name":"Faktury_008.000.043.zip","type":"file","size":123,"modified":"2026-01-01","path":"Cliente & uno/Faktury_008.000.043.zip"},{"name":"folder","type":"folder","size":null,"modified":null,"path":"folder"}]""")
            });
        }));
    var entries = await client.ListAsync("Cliente & uno");
    Equal(2, entries.Count);
    Equal(123L, entries[0].Size);
    Equal<long?>(null, entries[1].Size);
    Equal("Faktury_008.000.043.zip", entries[0].Name);
});

AsyncTest("API rejects HTTP errors, redirects and malformed responses", async () =>
{
    foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError, HttpStatusCode.Redirect })
    {
        using var client = new UpdateApiClient(new Uri("https://example.test/"), "test", "secret",
            new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(code))));

        try
        {
            await client.ListAsync();
            throw new Exception("HTTP error accepted");
        }
        catch (HttpRequestException e)
        {
            Equal(code, e.StatusCode);
        }
    }
    foreach (var json in new[] { "<html>login</html>", "null", "{}", "[null]", "[{\"name\":\"x\"}]", "[{\"name\":\"x\",\"type\":\"file\",\"size\":-1,\"path\":\"x\"}]" })
    {
        using var client = new UpdateApiClient(new Uri("https://example.test/"), "test", "secret",
            new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) })));

        try
        {
            await client.ListAsync();
            throw new Exception("Invalid JSON accepted");
        }
        catch (InvalidDataException)
        {
            // This is the expected failure for a malformed response.
        }
    }
});

AsyncTest("Invalid client folders are rejected before contacting the API", async () =>
{
    using var client = new UpdateApiClient(new Uri("https://example.test/"), "test", "secret",
        new StubHandler((_, _) => throw new Exception("Unexpected request")));
    foreach (var folder in new[] { "../other", "a/b", "a\\b", "C:\\temp", ".", "..", "NUL", "cliente " })
    {
        try
        {
            await client.ListAsync(folder);
            throw new Exception("Invalid folder accepted");
        }
        catch (ArgumentException)
        {
            // Invalid caller input must fail before any request is sent.
        }
    }
});

Test("Base URL must use HTTPS without credentials or query", () =>
{
    foreach (var uri in new[] { "http://example.test/", "https://user:secret@example.test/", "https://example.test/?x=1" })
    {
        try
        {
            using var client = new UpdateApiClient(new Uri(uri), "test", "secret");
            throw new Exception("Invalid base URL accepted");
        }
        catch (ArgumentException)
        {
            // Invalid caller input must fail before any request is sent.
        }
    }
});

// Build a real ZIP so validation tests exercise archive reading, not just names.
byte[] MakeZip()
{
    using var output = new MemoryStream();
    using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
    using (var writer = new StreamWriter(archive.CreateEntry("example.txt").Open()))
    {
        writer.Write("test package");
    }

    return output.ToArray();
}

// Give each file test its own temporary workspace and clean it up afterward.
async Task WithDirectory(Func<string, Task> run)
{
    var directory = Path.Combine(Path.GetTempPath(), "tytan-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);

    try
    {
        await run(directory);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

// Route listing and download requests to predictable test responses.
UpdateApiClient DownloadClient(byte[] bytes, Func<HttpRequestMessage, HttpContent>? content = null, string? path = null, long? size = null)
{
    var entry = new FileEntry("Faktury_008.000.043.zip", "file", size ?? bytes.Length, null, path ?? "cliente/Faktury_008.000.043.zip");
    return new(new Uri("https://example.test/SQLupdate/"), "test", "secret", new StubHandler((request, _) =>
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("api.php"))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new[] { entry })) });
        }

        Equal("https://example.test/SQLupdate/download.php?file=cliente%2FFaktury_008.000.043.zip", request.RequestUri.AbsoluteUri);
        Equal("Basic", request.Headers.Authorization?.Scheme);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content?.Invoke(request) ?? new ByteArrayContent(bytes) });
    }));
}

// End-to-end file tests use the actual service and downloader with fake HTTP.
AsyncTest("Full flow publishes a valid ZIP and leaves installed version unchanged", () => WithDirectory(async directory =>
{
    var bytes = MakeZip();
    using var client = DownloadClient(bytes);
    var result = await new UpdateService(client).CheckAndDownloadAsync(new("cliente", "Faktury", "008.000.042", directory));
    Equal(UpdateStatus.Downloaded, result.Status);
    Equal("008.000.042", result.InstalledVersion);
    Equal("008.000.043", result.AvailableVersion);
    Equal(true, bytes.SequenceEqual(await File.ReadAllBytesAsync(result.LocalPath!)));
    Equal(1, Directory.GetFiles(directory).Length);
}));

AsyncTest("Equal or newer installed version does not download", () => WithDirectory(async directory =>
{
    using var client = DownloadClient(MakeZip(), _ => throw new Exception("Unexpected download"));
    foreach (var version in new[] { "008.000.043", "008.000.044" })
    {
        Equal(UpdateStatus.NoUpdate, (await new UpdateService(client).CheckAndDownloadAsync(new("cliente", "Faktury", version, directory))).Status);
    }

    Equal(0, Directory.GetFiles(directory).Length);
}));

AsyncTest("Corrupt ZIP, wrong size and interrupted stream never publish a package", () => WithDirectory(async directory =>
{
    var zip = MakeZip();
    foreach (var kind in new[] { "corrupt", "size", "interrupted" })
    {
        using var client = kind switch
        {
            "corrupt" => DownloadClient(Encoding.UTF8.GetBytes("not a zip")),
            "size" => DownloadClient(zip, size: zip.Length + 1),
            _ => DownloadClient(zip, _ => new StreamContent(new InterruptedStream()))
        };
        var result = await new UpdateService(client).CheckAndDownloadAsync(new("cliente", "Faktury", "008.000.042", directory));
        Equal(UpdateStatus.Error, result.Status);
        Equal<string?>(null, result.LocalPath);
        Equal(0, Directory.GetFiles(directory).Length);
    }
}));

AsyncTest("Cross-client paths and traversal are rejected", () => WithDirectory(async directory =>
{
    foreach (var path in new[] { "other/Faktury_008.000.043.zip", "cliente/../Faktury_008.000.043.zip", "https://other.test/x", "cliente/x.zip" })
    {
        using var client = DownloadClient(MakeZip(), _ => throw new Exception("Unexpected download"), path);
        Equal(UpdateStatus.Error, (await new UpdateService(client).CheckAndDownloadAsync(new("cliente", "Faktury", "008.000.042", directory))).Status);
        Equal(0, Directory.GetFiles(directory).Length);
    }
}));

AsyncTest("Existing destination is preserved", () => WithDirectory(async directory =>
{
    var target = Path.Combine(directory, "Faktury_008.000.043.zip");
    await File.WriteAllTextAsync(target, "existing file");
    using var client = DownloadClient(MakeZip(), _ => throw new Exception("Unexpected download"));
    Equal(UpdateStatus.Error, (await new UpdateService(client).CheckAndDownloadAsync(new("cliente", "Faktury", "008.000.042", directory))).Status);
    Equal("existing file", await File.ReadAllTextAsync(target));
    Equal(1, Directory.GetFiles(directory).Length);
}));

AsyncTest("Cancellation during download cleans partial file", () => WithDirectory(async directory =>
{
    using var cancellation = new CancellationTokenSource();
    using var client = DownloadClient(MakeZip(), _ =>
    {
        cancellation.Cancel();
        return new ByteArrayContent(MakeZip());
    });
    var result = await new UpdateService(client).CheckAndDownloadAsync(new("cliente", "Faktury", "008.000.042", directory), cancellation.Token);
    Equal(UpdateStatus.Cancelled, result.Status);
    Equal<string?>(null, result.LocalPath);
    Equal(0, Directory.GetFiles(directory).Length);
}));

AsyncTest("Concurrent downloads publish one file without overwrite", () => WithDirectory(async directory =>
{
    using var client = DownloadClient(MakeZip());
    var service = new UpdateService(client);
    var request = new UpdateRequest("cliente", "Faktury", "008.000.042", directory);
    var results = await Task.WhenAll(service.CheckAndDownloadAsync(request), service.CheckAndDownloadAsync(request));
    Equal(1, results.Count(r => r.Status == UpdateStatus.Downloaded));
    Equal(1, results.Count(r => r.Status == UpdateStatus.Error));
    Equal(1, Directory.GetFiles(directory).Length);
}));

AsyncTest("Invalid input produces an error without any HTTP request", () => WithDirectory(async directory =>
{
    using var client = new UpdateApiClient(new Uri("https://example.test/"), "test", "secret",
        new StubHandler((_, _) => throw new Exception("Unexpected request")));
    var result = await new UpdateService(client).CheckAndDownloadAsync(new("cliente", "Faktury", "invalid", directory));
    Equal(UpdateStatus.Error, result.Status);
}));

// Run every registered case; a nonzero exit code means at least one failed.
var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception e)
    {
        failed++;
        Console.WriteLine($"FAIL {test.Name}: {e.Message}");
    }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed");
return failed == 0 ? 0 : 1;
