using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tytan.Updater;

// Keep console output readable and show help before creating an HTTP client.
Console.OutputEncoding = Encoding.UTF8;
if (args.Length == 0 || args is ["--help"])
{
    Console.WriteLine("""
        Tytan Updater - check and download updates; Tytan handles installation.
        list <client-folder>
        download <client-folder> <product> <installed-version> <destination>
        demo <destination>    Local demo without a server or credentials.

        Server access: set TYTAN_USERNAME and TYTAN_PASSWORD.
        TYTAN_BASE_URL is optional; defaults to https://tytan.poznan.pl/SQLupdate/
        Existing files are never overwritten. Press Ctrl+C to cancel.
        """);
    return 0;
}

// Check command shapes so positional arguments can be accessed safely.
if (!(args is ["list", _] or ["download", _, _, _, _] or ["demo", _]))
{
    Console.Error.WriteLine("Invalid arguments. See --help.");
    return 2;
}

// Print result statuses as names, such as Downloaded, rather than numbers.
var json = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

// Ctrl+C requests cooperative cancellation instead of terminating immediately.
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};
Console.CancelKeyPress += cancel;

try
{
    // Demo mode replaces HTTP transport; real mode reads external configuration.
    var demo = args[0] == "demo";
    var username = demo ? "demo" : Environment.GetEnvironmentVariable("TYTAN_USERNAME");
    var password = demo ? "demo" : Environment.GetEnvironmentVariable("TYTAN_PASSWORD");
    if (string.IsNullOrWhiteSpace(username) || password is null)
    {
        Console.Error.WriteLine("Set TYTAN_USERNAME and TYTAN_PASSWORD outside the repository.");
        return 2;
    }

    // No real request is made to demo.invalid when DemoHandler is injected.
    var baseUrl = demo ? "https://demo.invalid/SQLupdate/" :
        Environment.GetEnvironmentVariable("TYTAN_BASE_URL") ?? "https://tytan.poznan.pl/SQLupdate/";
    using var api = new UpdateApiClient(new Uri(baseUrl), username, password, demo ? new DemoHandler() : null);

    // Listing is read-only and does not invoke the download workflow.
    if (args[0] == "list")
    {
        Console.WriteLine(JsonSerializer.Serialize(await api.ListAsync(args[1], cancellation.Token), json));
        return 0;
    }

    // The same service handles both the demo and actual update requests.
    var request = demo ? new UpdateRequest("demo", "Demo", "001.000.001", args[1]) :
        new UpdateRequest(args[1], args[2], args[3], args[4]);
    var result = await new UpdateService(api).CheckAndDownloadAsync(request, cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(result, json));

    // Exit codes allow a calling script to distinguish failure and cancellation.
    return result.Status switch
    {
        UpdateStatus.Error => 1,
        UpdateStatus.Cancelled => 130,
        _ => 0
    };
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine(cancellation.IsCancellationRequested ? "Operation cancelled." : "Request timed out.");
    return cancellation.IsCancellationRequested ? 130 : 1;
}
catch (HttpRequestException e)
{
    Console.Error.WriteLine(e.StatusCode is { } code ? $"HTTP error {(int)code}; check access and the server path." : "Could not connect to the server.");
    return 1;
}
catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
{
    // Do not print raw URL/credential-related exception details.
    Console.Error.WriteLine("Invalid configuration, response or destination. Check the input values.");
    return 2;
}
finally
{
    Console.CancelKeyPress -= cancel;
}
