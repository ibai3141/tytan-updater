namespace Tytan.Updater.Desktop;

internal static class DesktopChecks
{
    public static int Run()
    {
        try
        {
            string sample = Path.Combine(AppContext.BaseDirectory, "installation.example.json");
            string json = File.ReadAllText(sample);
            LocalInstallation loaded = InstallationFileReader.Read(sample);
            Require(loaded.ClientFolder == "Barcin_Wodbar" && loaded.Products.Count == 3, "Read example installation");

            Reject("{", "Malformed JSON");
            Reject(json.Replace("Barcin_Wodbar", "../other"), "Folder traversal");
            Reject(json.Replace("008.000.042", "8.0.42"), "Malformed installed version");
            Reject(json.Replace("FK2025", "Faktury"), "Duplicate product");
            Reject("{\"clientFolder\":\"Barcin_Wodbar\",\"products\":[]}", "Empty product list");
            Reject("{\"clientFolder\":\"Barcin_Wodbar\",\"products\":[null]}", "Null product");
            Reject(json.Replace("clientFolder", "folder"), "Unexpected file fields");

            using var window = new MainForm();
            window.Show();
            window.LoadInstallation(sample, true);
            Application.DoEvents();
            Require(window.LoadedClient == "Barcin_Wodbar" && window.ProductCount == 3 &&
                window.DisplayedVersion(0) == "008.000.042", "Window displays the local installation");

            string invalid = Path.GetTempFileName();
            try
            {
                File.WriteAllText(invalid, "{}");
                try
                {
                    window.LoadInstallation(invalid, false);
                    throw new Exception("Invalid file unexpectedly loaded.");
                }
                catch (InvalidDataException)
                {
                    Require(window.LoadedClient == "Barcin_Wodbar" && window.ProductCount == 3,
                        "Invalid replacement preserves displayed state");
                }
            }
            finally
            {
                File.Delete(invalid);
            }

            // Optional image capture renders this application's window only.
            string? capture = Environment.GetEnvironmentVariable("TYTAN_DESKTOP_CAPTURE");
            if (!string.IsNullOrEmpty(capture))
            {
                using var bitmap = new Bitmap(window.Width, window.Height);
                window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
                bitmap.Save(capture, System.Drawing.Imaging.ImageFormat.Png);
            }

            window.Close();
            Console.WriteLine("PASS 10 desktop checks. No API requests were made.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
    }

    private static void Reject(string json, string description)
    {
        try
        {
            InstallationFileReader.Parse(json);
        }
        catch (InvalidDataException)
        {
            Console.WriteLine("PASS " + description);
            return;
        }

        throw new Exception(description + " was not rejected.");
    }

    private static void Require(bool condition, string description)
    {
        if (!condition)
        {
            throw new Exception(description);
        }

        Console.WriteLine("PASS " + description);
    }
}
