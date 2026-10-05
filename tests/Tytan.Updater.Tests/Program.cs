using Tytan.Updater;
using System.Net;
using System.Text;

var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Action run) => tests.Add((name, () => { run(); return Task.CompletedTask; }));
void AsyncTest(string name, Func<Task> run) => tests.Add((name, run));
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}");
}
FileEntry Entry(string name, string type = "file") => new(name, type, null, null, "cliente/" + name);

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
        Equal(false, PackageVersion.TryParse(value, out _));
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

AsyncTest("API uses BasicAuth, encodes folder and maps lowercase JSON", async () =>
{
    using var client = new UpdateApiClient(new Uri("https://example.test/SQLupdate"), "test", "secret",
        new StubHandler((request, _) =>
        {
            Equal("https://example.test/SQLupdate/api.php?dir=Cliente%20%26%20uno", request.RequestUri!.AbsoluteUri);
            Equal("Basic", request.Headers.Authorization?.Scheme);
            Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("test:secret")), request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                """[{"name":"Faktury_008.000.043.zip","type":"file","size":123,"modified":"2026-01-01","path":"Cliente & uno/Faktury_008.000.043.zip"},{"name":"folder","type":"folder","size":null,"modified":null,"path":"folder"}]""") });
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
        try { await client.ListAsync(); throw new Exception("HTTP error accepted"); }
        catch (HttpRequestException e) { Equal(code, e.StatusCode); }
    }
    foreach (var json in new[] { "<html>login</html>", "null", "{}", "[null]", "[{\"name\":\"x\"}]", "[{\"name\":\"x\",\"type\":\"file\",\"size\":-1,\"path\":\"x\"}]" })
    {
        using var client = new UpdateApiClient(new Uri("https://example.test/"), "test", "secret",
            new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) })));
        try { await client.ListAsync(); throw new Exception("Invalid JSON accepted"); }
        catch (InvalidDataException) { }
    }
});
AsyncTest("Invalid client folders are rejected before contacting the API", async () =>
{
    using var client = new UpdateApiClient(new Uri("https://example.test/"), "test", "secret",
        new StubHandler((_, _) => throw new Exception("Unexpected request")));
    foreach (var folder in new[] { "../other", "a/b", "a\\b", "C:\\temp", ".", "..", "NUL", "cliente " })
    {
        try { await client.ListAsync(folder); throw new Exception("Invalid folder accepted"); }
        catch (ArgumentException) { }
    }
});
Test("Base URL must use HTTPS without credentials or query", () =>
{
    foreach (var uri in new[] { "http://example.test/", "https://user:secret@example.test/", "https://example.test/?x=1" })
    {
        try { using var client = new UpdateApiClient(new Uri(uri), "test", "secret"); throw new Exception("Invalid base URL accepted"); }
        catch (ArgumentException) { }
    }
});

var failed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception e) { failed++; Console.WriteLine($"FAIL {test.Name}: {e.Message}"); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed");
return failed == 0 ? 0 : 1;
