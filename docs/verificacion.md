# Registro de verificación

## 5 de octubre de 2026: consulta del servidor

Se realizó una petición GET con BasicAuth a:

```text
https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar
```

Resultado: **HTTP 404**. No se descargaron paquetes. La petición no siguió redirecciones y las credenciales se leyeron del documento original, sin guardarlas en el repositorio.

El usuario confirma que los endpoints están publicados; esta comprobación no permite confirmar el contrato real en esa dirección. Es necesario contrastar la ruta exacta publicada o el acceso con los responsables del servidor. Un 404 por sí solo no identifica la causa.

La implementación y las pruebas locales continúan con el contrato de la guía. No equivalen a una validación del servidor real ni de la aplicación Tytan.

## Validación local de la implementación

- Solución compilada en Release con .NET SDK 9.0.304: cero errores y cero avisos.
- Ejecutable de pruebas: 15 de 15 casos aprobados. Incluye comparación, JSON, errores HTTP, descarga completa, ZIP inválido, tamaño incorrecto, interrupción, cancelación, rutas de otro cliente, conservación del destino y concurrencia.
- CLI: demostración completa con resultado `Downloaded`; repetición con resultado `Error` y código 1, conservando el ZIP existente; ayuda con código 0.
- La demostración se ejecutó en una carpeta temporal y se retiró su paquete de ejemplo al terminar.
- Comprobación de espacios y formato de los cambios con `git diff --check`.

La aplicación real de Tytan no está en el repositorio: se ha entregado un ejemplo de integración, no una integración ejecutada en su programa. No se ha probado una descarga real porque la consulta del endpoint documentado respondió 404.
