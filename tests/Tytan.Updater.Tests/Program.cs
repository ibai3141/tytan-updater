using Tytan.Updater;

var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Action run) => tests.Add((name, () => { run(); return Task.CompletedTask; }));
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

var failed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception e) { failed++; Console.WriteLine($"FAIL {test.Name}: {e.Message}"); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed");
return failed == 0 ? 0 : 1;
