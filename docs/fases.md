# Plan de implementación por fases

Cada fase termina con compilación, las comprobaciones que correspondan y un commit en español. El usuario realiza el push.

| Fase | Entregable | Comprobación | Commit previsto |
| --- | --- | --- | --- |
| 1. Base y versiones | Biblioteca C#, modelos y selección numérica por producto | Versiones inválidas, distintos productos y selección por versión | `Crear la base del módulo y la comparación de versiones` |
| 2. Consulta HTTPS | Cliente API con BasicAuth y JSON | Peticiones, campos, rutas y errores HTTP con servidor simulado | `Añadir la consulta HTTPS de actualizaciones` |
| 3. Descarga | ZIP temporal, validación y resultado para Tytan | ZIP real de prueba, interrupciones, cancelación y destinos existentes | `Implementar la descarga y entrega de paquetes` |
| 4. Uso e integración | Herramienta de prueba y ejemplo de llamada C# | Ejecución local completa y documentación de uso | `Documentar la integración y añadir la herramienta de prueba` |

Se comienza con .NET 9, disponible en el equipo, y sin dependencias externas. Las pruebas son un ejecutable local que devuelve un código distinto de cero si falla algún caso. No necesitan acceder al servidor de producción.

La integración con el código de Tytan se entrega como ejemplo y contrato: su aplicación no está en este repositorio. La comprobación real de los endpoints se registrará por separado de las pruebas locales; no se dará por realizada si no se ejecuta.

## Entregables realizados

- Fase 1: biblioteca, modelos, comparación numérica y selección por producto.
- Fase 2: consulta HTTPS y pruebas de autenticación, JSON y errores. La consulta real obtuvo HTTP 404; véase [verificacion.md](verificacion.md).
- Fase 3: descarga temporal, comprobación de tamaño y lectura del ZIP, entrega a Tytan y pruebas de fallos y concurrencia.
- Fase 4: herramienta con comandos `list`, `download` y `demo`, instrucciones de uso y ejemplo de integración. La conexión con el código real de Tytan sigue pendiente en su aplicación.
