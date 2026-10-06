namespace Tytan.Updater.Desktop;

internal static class ComparisonChecks
{
    public static int Run()
    {
        int count = 0;
        try
        {
            void Check(bool condition, string description)
            {
                if (!condition)
                {
                    throw new Exception(description);
                }
                count++;
                Console.WriteLine("PASS " + description);
            }

            PackageVersion.TryParse("008.009.999", out var older);
            PackageVersion.TryParse("008.010.000", out var newer);
            Check(newer.CompareTo(older) > 0, "Minor version takes precedence over patch");
            PackageVersion.TryParse("009.000.000", out var nextMajor);
            Check(nextMajor.CompareTo(newer) > 0, "Major version takes precedence over minor");
            PackageVersion.TryParse("008.010.001", out var nextPatch);
            Check(nextPatch.CompareTo(newer) > 0 && nextPatch.ToString() == "008.010.001", "Patch comparison and fixed-width formatting");
            Check(new[] { "8.0.1", "008.000.001.zip", "+08.000.001", "008.000.001 ", "008..001", "008.000.abc" }
                .All(text => !PackageVersion.TryParse(text, out _)), "Invalid version formats rejected");

            var local = new LocalInstallation("Barcin_Wodbar", new[]
            {
                new InstalledProduct("Faktury", "008.000.042"),
                new InstalledProduct("FK2025", "005.005.007"),
                new InstalledProduct("FK2026", "005.005.041"),
                new InstalledProduct("Missing", "001.000.000")
            });
            RemoteEntry[] entries =
            [
                Package("Faktury_008.000.040.zip") with { Modified = "2026-12-31 23:59:59" },
                Package("Faktury_008.000.043.zip") with { Modified = "2020-01-01 00:00:00" },
                Package("FK2025_005.005.007.zip"),
                Package("FK2026_005.005.040.zip"),
                Package("FakturyExtra_999.999.999.zip"),
                Package("Faktury_invalid.zip"),
                Package("Faktury_999.999.999.zip") with { Type = "folder", Size = null }
            ];
            var results = UpdateComparison.Compare(local, entries);
            Check(results[0].Status == UpdateStatus.UpdateAvailable && results[0].AvailableVersion?.ToString() == "008.000.043" &&
                results[0].Package?.Name == "Faktury_008.000.043.zip", "Newest product package chosen numerically, ignoring timestamps and invalid names");
            Check(results[1].Status == UpdateStatus.UpToDate, "Equal installed and available versions are up to date");
            Check(results[2].Status == UpdateStatus.InstalledNewer, "Newer installed version does not suggest a downgrade");
            Check(results[3].Status == UpdateStatus.NoPackage && results[3].Package is null, "Missing product is distinguished from up to date");
            Check(local.Products[0].InstalledVersion == "008.000.042" && results.Count == local.Products.Count,
                "Comparison leaves installed information unchanged");
            Check(UpdateComparison.Compare(local, Array.Empty<RemoteEntry>()).All(result => result.Status == UpdateStatus.NoPackage),
                "Empty listing reports no package for every installed product");
            var isolated = UpdateComparison.Compare(local, new[]
            {
                Package("Faktury_999.999.999.zip") with { Path = "other/Faktury_999.999.999.zip" },
                Package("FakturyExtra_999.999.999.zip")
            });
            Check(isolated[0].Status == UpdateStatus.NoPackage, "Cross-client entries and different product prefixes cannot be selected");
            var named = new LocalInstallation("Barcin_Wodbar", new[] { new InstalledProduct("My_Product", "001.000.000") });
            var namedResult = UpdateComparison.Compare(named, new[] { Package("my_product_001.000.001.ZIP") })[0];
            Check(namedResult.Status == UpdateStatus.UpdateAvailable, "Product underscores and ZIP letter case are supported");

            Console.WriteLine($"PASS {count} version comparison checks. No network or installation-file writes.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
    }

    private static RemoteEntry Package(string name) => new(name, "file", 100, "2026-10-02 06:57:29", "Barcin_Wodbar/" + name);
}
