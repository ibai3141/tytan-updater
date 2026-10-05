using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Tytan.Updater;

// Replace the remote server with a listing and ZIP generated in memory.
internal sealed class DemoHandler : HttpMessageHandler
{
    private readonly byte[] package;

    public DemoHandler()
    {
        // Dispose the archive before reading its bytes so the ZIP is complete.
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("DEMO.txt").Open()))
        {
            writer.Write("Demo package. This is not a Tytan update.");
        }

        package = output.ToArray();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // api.php returns JSON; the download request returns the sample ZIP.
        HttpContent content = request.RequestUri!.AbsolutePath.EndsWith("api.php", StringComparison.Ordinal)
            ? new StringContent(JsonSerializer.Serialize(new[] {
                new FileEntry("Demo_001.000.002.zip", "file", package.Length, null, "demo/Demo_001.000.002.zip") }))
            : new ByteArrayContent(package);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
