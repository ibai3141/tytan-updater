# Registro de verificación

## 5 de octubre de 2026: consulta del servidor

Se realizó una petición GET con BasicAuth a:

```text
https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar
```

Resultado: **HTTP 404**. No se descargaron paquetes. La petición no siguió redirecciones y las credenciales se leyeron del documento original, sin guardarlas en el repositorio.

El usuario confirma que los endpoints están publicados; esta comprobación no permite confirmar el contrato real en esa dirección. Es necesario contrastar la ruta exacta publicada o el acceso con los responsables del servidor. Un 404 por sí solo no identifica la causa.

La implementación y las pruebas locales continúan con el contrato de la guía. No equivalen a una validación del servidor real ni de la aplicación Tytan.
