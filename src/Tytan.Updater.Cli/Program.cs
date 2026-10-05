using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tytan.Updater;

Console.OutputEncoding = Encoding.UTF8;
if (args.Length == 0 || args is ["--help"])
{
    Console.WriteLine("""
        Tytan Updater - consulta y descarga; la instalación corresponde a Tytan.
        list <carpeta-cliente>
        download <carpeta-cliente> <producto> <version-instalada> <destino>
        demo <destino>         Prueba local sin servidor ni credenciales.

        Acceso real: configurar TYTAN_USERNAME y TYTAN_PASSWORD.
        TYTAN_BASE_URL es opcional; por defecto https://tytan.poznan.pl/SQLupdate/
        Los archivos existentes no se sobrescriben. Ctrl+C cancela la operación.
        """);
    return 0;
}
if (!(args is ["list", _] or ["download", _, _, _, _] or ["demo", _]))
{
    Console.Error.WriteLine("Argumentos no válidos. Consulte --help.");
    return 2;
}

var json = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += cancel;
try
{
    var demo = args[0] == "demo";
    var username = demo ? "demo" : Environment.GetEnvironmentVariable("TYTAN_USERNAME");
    var password = demo ? "demo" : Environment.GetEnvironmentVariable("TYTAN_PASSWORD");
    if (string.IsNullOrWhiteSpace(username) || password is null)
    {
        Console.Error.WriteLine("Configure TYTAN_USERNAME y TYTAN_PASSWORD fuera del repositorio.");
        return 2;
    }
    var baseUrl = demo ? "https://demo.invalid/SQLupdate/" :
        Environment.GetEnvironmentVariable("TYTAN_BASE_URL") ?? "https://tytan.poznan.pl/SQLupdate/";
    using var api = new UpdateApiClient(new Uri(baseUrl), username, password, demo ? new DemoHandler() : null);
    if (args[0] == "list")
    {
        Console.WriteLine(JsonSerializer.Serialize(await api.ListAsync(args[1], cancellation.Token), json));
        return 0;
    }
    var request = demo ? new UpdateRequest("demo", "Demo", "001.000.001", args[1]) :
        new UpdateRequest(args[1], args[2], args[3], args[4]);
    var result = await new UpdateService(api).CheckAndDownloadAsync(request, cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(result, json));
    return result.Status switch { UpdateStatus.Error => 1, UpdateStatus.Cancelled => 130, _ => 0 };
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine(cancellation.IsCancellationRequested ? "Operación cancelada." : "Tiempo de espera agotado.");
    return cancellation.IsCancellationRequested ? 130 : 1;
}
catch (HttpRequestException e)
{
    Console.Error.WriteLine(e.StatusCode is { } code ? $"Error HTTP {(int)code}; compruebe acceso y ruta." : "No se pudo conectar con el servidor.");
    return 1;
}
catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
{
    // Do not print raw URL/credential-related exception details.
    Console.Error.WriteLine("Configuración, respuesta o destino no válidos. Compruebe los datos de entrada.");
    return 2;
}
finally { Console.CancelKeyPress -= cancel; }
