# Consulta y descarga de actualizaciones de TytanSQL

Actualizado: 5 de octubre de 2026.

## 1. Objetivo y alcance

Implementar un módulo C# que TytanSQL pueda llamar para consultar la carpeta de un cliente, detectar una versión superior de un producto y descargar su ZIP a una carpeta local.

Tytan aplica el paquete y gestiona la versión instalada. Nuestro módulo termina al entregar el archivo completo y su resultado. No necesita inspeccionar la instalación, extraer paquetes, sustituir ejecutables, ejecutar SQL ni implementar copias de seguridad o recuperación de la instalación.

Hay información suficiente para empezar: lenguaje, autenticación, endpoints, listado JSON y regla de comparación están descritos. Una interfaz de entrada y salida permite desarrollar y probar el módulo sin necesitar el código completo de Tytan.

## 2. Fuentes y confirmaciones

| Fuente | Información |
| --- | --- |
| `SQl_Update_Projekt_v_1.0.docx` | Carpeta por cliente, versión guardada en la aplicación y descarga de una versión superior |
| `SQl_Update_Projekt_v_1.1.docx` | La carpeta contiene ZIP de las últimas versiones de todos los productos del cliente |
| `Connect to your SQLupdate directory on the Idea server.docx` | C#, HttpClient, BasicAuth, listado JSON y descarga; ejemplos de cliente y servidor |
| `Kopia zapasowa … .wbk` | Copia más corta de la guía, sin requisitos adicionales |
| Usuario, 05/10/2026 | API y descarga ya publicadas; utilizar el entorno disponible; Tytan se encarga del proceso posterior |

Los originales están en `F:\SQL_Update`. El archivo `.~lock.…docx#` es temporal. Las credenciales no se copian al repositorio.

La publicación de los endpoints está confirmada por el usuario. Su respuesta real todavía no se ha comprobado desde el proyecto. Los ejemplos de esta documentación describen la guía, no respuestas capturadas del servidor.

## 3. Reparto de responsabilidades

| TytanSQL | módulo de este repositorio |
| --- | --- |
| Proporcionar carpeta, producto y versión instalada | Consultar la carpeta y comparar versiones del producto |
| Facilitar configuración de acceso y destino local | Autenticarse, descargar y guardar un paquete completo |
| Decidir cuándo comprobar actualizaciones | Comunicar ausencia de actualización, descarga, cancelación o error |
| Instalar el paquete y registrar la versión instalada | Entregar ruta local y versión del paquete descargado |

## 4. API publicada

Dirección base: `https://tytan.poznan.pl/SQLupdate/`.

Todas las peticiones requieren HTTPS y BasicAuth. Configurar HttpClient con credenciales recibidas desde configuración externa. No incrustarlas en código, URL, ejemplos o registros.

### Listar

```http
GET /SQLupdate/api.php
GET /SQLupdate/api.php?dir=Barcin_Wodbar
```

La primera petición permite explorar las carpetas. Si Tytan ya proporciona la carpeta del cliente, consultar directamente la segunda.

| Campo JSON | Contenido |
| --- | --- |
| `name` | Nombre del archivo o carpeta |
| `type` | `file` o `folder` |
| `size` | tamaño en bytes; puede ser nulo para carpetas |
| `modified` | Fecha de modificación como texto |
| `path` | Ruta relativa para la descarga |

Ejemplo ilustrativo basado en el esquema de la guía:

```json
[
  {
    "name": "Faktury_008.000.043.zip",
    "type": "file",
    "size": 17492992,
    "modified": "2026-09-29 09:15:00",
    "path": "Barcin_Wodbar/Faktury_008.000.043.zip"
  }
]
```

Mapear expresamente los campos JSON en minúscula a las propiedades C#, o configurar la deserialización para admitir diferencias entre mayúsculas y minúsculas. La guía muestra propiedades como `Name`, mientras que su PHP devuelve `name`.

### Descargar

```http
GET /SQLupdate/download.php?file=Barcin_Wodbar/Faktury_008.000.043.zip
```

Utilizar el campo `path` del archivo seleccionado y codificar los valores de los parámetros de consulta. Mantener las peticiones en la dirección base configurada.

El PHP documentado devuelve el contenido binario con tipo `application/octet-stream`, nombre de descarga y tamaño. No hay que desarrollar ni publicar PHP en este proyecto.

## 5. Contrato propuesto con Tytan

Los documentos no fijan firmas de métodos; este contrato es una propuesta para la implementación.

Entradas:

- Dirección base y credenciales desde configuración externa.
- Carpeta del cliente, por ejemplo `Barcin_Wodbar`.
- Producto o prefijo fijo, por ejemplo `Faktury`.
- Versión instalada, por ejemplo `008.000.042`.
- Carpeta local de destino.
- Posibilidad de cancelar la operación.

La versión llega como dato de entrada. No buscamos por nuestra cuenta en el registro, bases de datos o archivos de instalación.

| Resultado | Información devuelta |
| --- | --- |
| Sin actualización | Producto, versión instalada y versión disponible si existe |
| Descargado | Producto, versión disponible y ruta local del ZIP completo |
| Error | Motivo útil para Tytan; ninguna ruta presentada como descarga válida |
| Cancelado | Operación interrumpida sin entregar un paquete parcial |

Si no existe un paquete del producto, indicar expresamente que no hay paquete disponible. Un fallo de acceso o JSON inválido es un error, no ausencia de actualización.

El módulo informa de una versión descargada, no instalada. Tytan registra la versión después de aplicar el ZIP.

## 6. Selección y comparación de versiones

Los documentos muestran estos nombres, con extensión ZIP oculta en las capturas:

```text
Faktury_008.000.043.zip
FK2025_005.005.007.zip
FK2026_005.005.040.zip
```

Regla inicial: `<producto>_<versión>.zip`, con tres componentes numéricos separados por puntos.

1. Conservar entradas de tipo `file` y paquetes ZIP.
2. Filtrar por el producto solicitado, incluyendo el separador `_` para evitar coincidencias con otros productos.
3. Extraer y validar la versión.
4. Comparar sus componentes como números.
5. Seleccionar la versión más alta del mismo producto.
6. Descargarla únicamente si es superior a la instalada.

```text
008.000.043 -> (8, 0, 43)
008.002.066 -> (8, 2, 66)
(8, 2, 66) > (8, 0, 43)
```

Versiones iguales o inferiores no provocan descarga. Cada producto se compara por separado. Ignorar nombres que no cumplen el patrón con un diagnóstico; una versión de entrada inválida es un error.

La fecha `modified` es informativa. Aunque la guía ordena por fecha en un ejemplo, el requisito pide comparar versiones: copiar un paquete recientemente no lo convierte en una versión superior.

El requisito es obtener el paquete más reciente. Las reglas de instalación y cualquier necesidad de pasos intermedios corresponden a Tytan.

## 7. Flujo

```text
Recibir carpeta, producto, versión y destino desde Tytan
    |
Validar entradas y preparar HttpClient con BasicAuth
    |
Consultar api.php?dir=<carpeta>
    |
Deserializar y seleccionar el ZIP de mayor versión del producto
    |
Comparar con la versión instalada
    +-- Sin paquete o versión igual/inferior -> devolver sin actualización
    +-- Superior
         |
       Descargar con download.php?file=<path> a un archivo temporal
         |
       Comprobar descarga completa y legibilidad del ZIP
         |
       Publicar el archivo en el destino local
         |
       Devolver ruta y versión a Tytan
```

Descargar por flujo para no cargar todo el archivo en memoria. Un parcial no ocupa el nombre final ni se devuelve como paquete listo. Comprobar el tamaño si está disponible y que el ZIP se puede abrir sin extraerlo. La API documentada no ofrece un hash; no asumir verificación criptográfica del paquete.

## 8. Organización propuesta

| Componente C# | Responsabilidad |
| --- | --- |
| Configuración | Servidor, autenticación y opciones de conexión |
| Cliente API | HTTPS, lectura JSON y descarga |
| Modelo de entrada remota | Campos `name`, `type`, `size`, `modified`, `path` |
| Comparador de versiones | Interpretación numérica y selección por producto |
| Servicio de actualización | Coordinación de consulta, comparación y descarga |
| Modelos de entrada y resultado | Contrato para la llamada desde Tytan |

Usar el entorno .NET disponible y ajustar compatibilidad durante la integración. Un ejecutable de prueba puede comprobar el módulo sin la aplicación completa; el entregable es el módulo invocable desde Tytan.

## 9. Errores y manejo de archivos

- Distinguir autenticación rechazada, carpeta o archivo ausente, error del servidor, JSON inválido y fallo de conexión.
- Gestionar tiempos de espera y cancelación; cualquier reintento será limitado.
- Validar nombres y rutas: rechazar rutas absolutas y componentes `..`; mantener la descarga en la carpeta del cliente solicitado.
- Mantener los archivos locales dentro del destino configurado; detectar permisos insuficientes y falta de espacio.
- Evitar escrituras simultáneas al mismo archivo y definir qué hacer si el ZIP ya existe.
- Limpiar temporales de operaciones fallidas sin borrar paquetes completos ajenos a ellas.
- Registrar producto, versiones y resultado sin credenciales.

## 10. Fases y criterios de aceptación

| Fase | Trabajo | Criterio de aceptación |
| --- | --- | --- |
| 1. Contrato y comparación | Modelos, filtrado y versiones con listados de ejemplo | Distingue productos y versiones superiores, iguales e inferiores |
| 2. Consulta HTTPS | BasicAuth, JSON y errores | Interpreta el esquema documentado; una prueba real confirma el contrato efectivo |
| 3. Descarga y entrega | Temporal, validación y ruta local | Entrega un ZIP completo o un resultado de fallo claro; nunca presenta un parcial como listo |
| 4. Integración | Conectar la llamada y la ruta de salida con Tytan | Tytan recibe el resultado y continúa su proceso existente |

## 11. Verificación necesaria

- Comparación numérica, incluidos cambios en cualquiera de los tres componentes.
- Varios productos y varias versiones de un producto.
- Mapeo de campos JSON en minúscula y valores nulos permitidos.
- Listas vacías, nombres inválidos y versión de entrada inválida.
- Autenticación rechazada, errores HTTP y respuestas que no son JSON.
- parámetros con espacios o caracteres que requieren codificación.
- Rutas inválidas o de otro cliente.
- Descargas correctas, canceladas, interrumpidas y ZIP no válido.
- Destino existente, permisos insuficientes y escrituras simultáneas.

Se pueden probar las reglas localmente con respuestas y ZIP de ejemplo. La prueba real verifica el contrato del servidor; no requiere pruebas de carga ni pruebas de instalación de Tytan.

## 12. Detalles que se resolverán durante la implementación

- Forma concreta de invocación y compatibilidad del proyecto .NET de Tytan.
- Mecanismo de configuración de credenciales y destino local.
- Respuestas reales ante errores y nombres exactos de los paquetes.
- política ante un ZIP existente y formato del diagnóstico que consume Tytan.

Estos detalles no impiden empezar. Se parte del contrato propuesto y se ajusta al integrar y comprobar la API publicada.
