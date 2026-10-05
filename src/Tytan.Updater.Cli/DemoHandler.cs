using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Tytan.Updater;

internal sealed class DemoHandler : HttpMessageHandler
{
    private readonly byte[] package;

    public DemoHandler()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("DEMO.txt").Open()))
            writer.Write("Demo package. This is not a Tytan update.");
        package = output.ToArray();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpContent content = request.RequestUri!.AbsolutePath.EndsWith("api.php", StringComparison.Ordinal)
            ? new StringContent(JsonSerializer.Serialize(new[] {
                new FileEntry("Demo_001.000.002.zip", "file", package.Length, null, "demo/Demo_001.000.002.zip") }))
            : new ByteArrayContent(package);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
