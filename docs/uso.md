# Uso y conexión con Tytan

## Compilar y ejecutar las pruebas

Desde la raíz del repositorio, con el SDK .NET 9 instalado:

```powershell
dotnet build Tytan.Updater.sln --configuration Release
dotnet run --project tests/Tytan.Updater.Tests --configuration Release
```

Las pruebas son un ejecutable sin dependencias externas. Imprimen cada caso y devuelven código 0 si pasan todos, o 1 si falla alguno. No se ejecutan mediante `dotnet test`.

## Demostración local

```powershell
dotnet run --project src/Tytan.Updater.Cli -- demo ./downloads/demo
```

Simula el listado y la descarga en memoria; no contacta con el servidor. Genera `Demo_001.000.002.zip`, con un texto de ejemplo, y devuelve un resultado `Downloaded`. No es una actualización real de Tytan. Al repetir el comando en el mismo destino, el módulo comunica que el archivo ya existe y lo conserva.

## Acceso al servidor

Proporcionar las credenciales para la sesión de PowerShell, sin escribirlas en el código o en el historial:

```powershell
$env:TYTAN_USERNAME = Read-Host 'Usuario'
$env:TYTAN_PASSWORD = [System.Net.NetworkCredential]::new('', (Read-Host 'Contraseña' -AsSecureString)).Password
$env:TYTAN_BASE_URL = 'https://tytan.poznan.pl/SQLupdate/'

dotnet run --project src/Tytan.Updater.Cli -- list Barcin_Wodbar
dotnet run --project src/Tytan.Updater.Cli -- download Barcin_Wodbar Faktury 008.000.042 ./downloads/Barcin_Wodbar

Remove-Item Env:TYTAN_USERNAME, Env:TYTAN_PASSWORD
```

Las variables de entorno son una opción para esta herramienta de prueba; la aplicación Tytan puede proporcionar las credenciales mediante su configuración. `TYTAN_BASE_URL` permite utilizar la ruta real si difiere de la documentación.

La consulta real del 5 de octubre respondió HTTP 404 en la dirección documentada. Véase [verificacion.md](verificacion.md). Antes de esperar una descarga real, contrastar esa ruta; las pruebas locales no validan la publicación del servidor.

El comando `list` no descarga archivos. `download` consulta y descarga únicamente si hay una versión superior del producto. Ctrl+C cancela. Código de salida: 0 para éxito o ausencia de actualización, 1 para error de operación, 2 para argumentos/configuración no válidos y 130 para cancelación solicitada. La salida JSON del comando de descarga incluye el estado; una descarga fallida puede devolver 1 aunque su causa sea una entrada inválida, porque se comunica como `UpdateResult.Error`.

## Llamada desde TytanSQL

Añadir una referencia a `src/Tytan.Updater/Tytan.Updater.csproj` desde el proyecto compatible de Tytan, o distribuir la biblioteca compilada. Este ejemplo ilustra el contrato; sustituir las variables por los datos que ya tiene la aplicación:

```csharp
using Tytan.Updater;

// Reutilizar el cliente durante la vida del componente y disponerlo al terminar.
using var api = new UpdateApiClient(
    new Uri("https://tytan.poznan.pl/SQLupdate/"),
    usernameFromConfiguration,
    passwordFromConfiguration);

var service = new UpdateService(api);
var result = await service.CheckAndDownloadAsync(
    new UpdateRequest(clientFolder, product, installedVersion, localUpdatesDirectory),
    cancellationToken);

switch (result.Status)
{
    case UpdateStatus.Downloaded:
        // Entregar result.LocalPath y result.AvailableVersion al flujo existente de Tytan.
        // Solo Tytan aplica el ZIP y actualiza su registro de versión instalada.
        break;
    case UpdateStatus.NoUpdate:
        // Continuar sin instalar. Message distingue ausencia de paquete y versión no superior.
        break;
    case UpdateStatus.Cancelled:
    case UpdateStatus.Error:
        // Mostrar o registrar result.Message.
        break;
}
```

`UpdateApiClient.ListAsync()` permite listar la raíz; `ListAsync(clientFolder)` consulta una carpeta. `UpdateService` coordina la selección y descarga. La versión instalada se entrega como cadena de tres componentes numéricos.

## Política de archivos y límites

- El producto y la carpeta deben coincidir con los nombres del servidor; la selección distingue mayúsculas y minúsculas en el prefijo.
- Se admiten carpetas de cliente de un solo nivel, como las de los documentos.
- Los archivos existentes no se reutilizan ni se sobrescriben automáticamente. Tytan decide si corresponde moverlos o eliminarlos antes de otra descarga.
- Las descargas se escriben en un temporal propio y se publican mediante un movimiento en la misma carpeta, sin reemplazar archivos.
- Se valida el tamaño cuando existe y se comprueba que el ZIP pueda leerse y descomprimirse a un flujo descartado. No se extrae en disco ni se verifica una firma o hash: la API no los ofrece.
- El tiempo de espera predeterminado es de dos minutos por operación de listado o descarga, incluyendo lectura y validación. Puede configurarse en el constructor del cliente.
- El cliente no sigue redirecciones. Un handler de prueba inyectado debe conservar ese comportamiento. No hay reintentos automáticos.
- Si el sistema impide borrar un temporal después de un fallo, puede quedar un `.tytan-*.part`; nunca se entrega como ZIP listo.
- La compatibilidad con la aplicación real de Tytan y la instalación se validan en su proyecto. Esta biblioteca utiliza actualmente `net9.0`.

## Estructura del repositorio

```text
src/Tytan.Updater/        Biblioteca C#
src/Tytan.Updater.Cli/    Herramienta de prueba y demostración
tests/Tytan.Updater.Tests/ Pruebas locales sin servidor real
docs/                    Requisitos, fases, uso y verificación
```
