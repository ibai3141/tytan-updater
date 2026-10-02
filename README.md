# tytan-updater

Proyecto para comprobar y descargar actualizaciones de Tytan mediante HTTPS y, cuando se defina el procedimiento de instalación, aplicarlas en el equipo del cliente.

Este repositorio contiene documentación inicial. Todavía no hay código ni se ha elegido un lenguaje de programación.

## Objetivo

Detectar si el cliente utiliza una versión antigua de un programa o módulo, localizar una versión más reciente en su carpeta del servidor y actualizarlo siguiendo el procedimiento que indique el responsable de Tytan.

La documentación recibida describe la consulta y descarga de ZIP. La petición de actualizar automáticamente amplía ese alcance: hay que definir también cómo instalar esos ZIP.

## Información disponible

- Dirección del servidor: `https://tytan.poznan.pl/SQLupdate/`.
- Cada cliente tiene una carpeta propia, como `Barcin_Wodbar` o `barczewo_zwik`.
- Las actualizaciones se distribuyen como ZIP con el módulo y su versión en el nombre.
- La aplicación del cliente guarda su carpeta de actualizaciones y la versión actual, según el documento recibido.
- Si hay una versión superior a la instalada, se debe descargar la actualización.

Ejemplo de nombre, con la extensión inferida del texto del documento:

```text
Faktury_008.000.043.zip
└ módulo └ versión
```

El documento incluye credenciales de acceso. No se copian al repositorio.

## Cómo plantearlo

1. Confirmar cómo obtener la versión instalada y cómo consultar los archivos por HTTPS.
2. Implementar una comprobación que indique si existe una actualización.
3. Añadir la descarga y validación del paquete.
4. Implementar la instalación cuando se conozca el procedimiento real de Tytan.
5. Probar el flujo completo con una instalación de prueba antes de utilizarlo con clientes.

El actualizador podría integrarse en la aplicación o ejecutarse como una herramienta externa. Esa decisión sigue pendiente.

El detalle de requisitos, arquitectura propuesta, preguntas y fases está en [docs/planteamiento.md](docs/planteamiento.md).

## Estado actual

- Documentación preparada a partir de dos fotografías del mismo documento y la conversación inicial.
- Acceso al servidor sin verificar: el intento de abrir la URL con la herramienta web no permitió consultar su contenido.
- Pendientes: aplicación de prueba, ZIP de ejemplo, lenguaje de integración y procedimiento de instalación.

El cambio de FTP a HTTPS define cómo se transportan los archivos. Por sí solo no demuestra que se haya resuelto la saturación de la red; ese resultado deberá medirse.
