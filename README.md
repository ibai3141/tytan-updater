# tytan-updater

Módulo C# para TytanSQL que consulta actualizaciones por HTTPS, compara versiones y descarga el ZIP más reciente cuando es superior a la versión instalada. Tytan se encarga de aplicar la actualización.

El repositorio contiene la biblioteca .NET 9, una herramienta de prueba y pruebas locales sin dependencias externas.

## Inicio rápido

```powershell
dotnet build Tytan.Updater.sln --configuration Release
dotnet run --project tests/Tytan.Updater.Tests --configuration Release
dotnet run --project src/Tytan.Updater.Cli -- demo ./downloads/demo
```

La demostración genera un ZIP de ejemplo sin acceder al servidor. Consulta [docs/uso.md](docs/uso.md) para configurar credenciales, listar archivos, descargar y llamar al módulo desde Tytan.

## Alcance confirmado

- Integración con TytanSQL en C# mediante HttpClient.
- Servidor base: `https://tytan.poznan.pl/SQLupdate/`.
- Autenticación BasicAuth en todas las peticiones HTTPS.
- `api.php` lista archivos y carpetas en JSON.
- `api.php?dir=<carpeta>` lista la carpeta de un cliente.
- `download.php?file=<ruta-relativa>` descarga un archivo.
- Ambos endpoints ya están publicados, según confirmación del usuario.
- Cada carpeta contiene los paquetes de los productos que tiene el cliente.
- La carpeta, el producto y la versión instalada se reciben desde Tytan.
- El módulo entrega la ruta local del ZIP descargado y su versión.
- Tytan realiza la instalación y gestiona el estado de la versión instalada.

## Ejemplo

```text
Carpeta:           Barcin_Wodbar
Producto:          Faktury
Versión instalada: 008.000.042
Paquete remoto:    Faktury_008.000.043.zip
Resultado:         ZIP descargado y ruta local devuelta a Tytan
```

## Plan

1. Definir una interfaz C# que reciba los datos de Tytan.
2. Consultar la API y convertir el JSON en objetos.
3. Filtrar los ZIP del producto y comparar las versiones numéricas.
4. Descargar el paquete a un archivo temporal y publicarlo localmente al completarse.
5. Devolver un resultado explícito: sin actualización, descargado, cancelado o error.
6. Verificar las reglas localmente y comprobar después el contrato del servidor real.

Se utilizará el entorno .NET disponible, ajustando la compatibilidad al proyecto de Tytan durante la integración. La elección de versión no bloquea el inicio.

El contrato, los endpoints y las reglas están en [docs/planteamiento.md](docs/planteamiento.md). Los entregables y commits de cada fase están en [docs/fases.md](docs/fases.md).

## Fuentes y estado

Documentos revisados en `F:\SQL_Update`: proyecto v1.0, proyecto v1.1, guía de integración HTTPS + BasicAuth y su copia de seguridad. Alcance actualizado con las confirmaciones del usuario del 5 de octubre de 2026.

La publicación de los endpoints está confirmada por el usuario. La consulta real a `api.php?dir=Barcin_Wodbar` del 5 de octubre respondió HTTP 404; hay que contrastar la ruta publicada. Véase [docs/verificacion.md](docs/verificacion.md). Las credenciales se configuran fuera del repositorio.
