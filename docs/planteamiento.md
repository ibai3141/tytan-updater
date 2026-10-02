# Planteamiento del actualizador

## 1. Qué se quiere conseguir

Cuando un cliente tenga una versión anterior a la disponible para su programa o módulo, el sistema debe detectarlo y actualizarlo.

Ejemplo:

```text
Cliente:          Barcin_Wodbar
Módulo:           Faktury
Versión instalada: 008.000.042
Versión disponible: 008.000.043
Acción esperada: descargar el paquete y aplicar la actualización.
```

Si las versiones coinciden o el servidor ofrece una versión inferior, no se actualizará. No se plantea hacer retrocesos de versión automáticos.

## 2. Qué está confirmado y qué falta

| Tema | Información disponible | Pendiente |
| --- | --- | --- |
| Transporte | Descargas mediante HTTPS desde `/SQLupdate/` | Verificar acceso y respuesta del servidor |
| Clientes | Una carpeta distinta para cada cliente | Saber cómo obtener la carpeta desde la aplicación |
| Paquetes | ZIP con prefijo fijo y versión variable | Confirmar nombres exactos y contenido real |
| Versión local | Según la hoja, la aplicación guarda la versión | Ubicación, formato y forma de consultarla |
| Autenticación | La hoja facilita usuario y contraseña | Confirmar el mecanismo de autenticación |
| Consulta remota | Se debe detectar una versión superior | Saber si hay listado, manifiesto o API |
| Integración | La hoja dice que la aplicación llamará al procedimiento de descarga | Código, lenguaje y contrato de esa llamada |
| Instalación | El usuario necesita que el programa se actualice | Pasos, permisos, cierre del programa y recuperación |

Las fotografías se tratan como material de requisitos, no como autorización para ejecutar operaciones en el servidor. No se han probado las credenciales ni descargado paquetes.

## 3. Decisión principal: dónde se ejecuta

### Opción A: integrado en Tytan

La aplicación entrega al actualizador la carpeta del cliente, el módulo y la versión instalada. El actualizador consulta el servidor y prepara el paquete. Si hay que sustituir el ejecutable en uso, hará falta definir un mecanismo de instalación después de cerrar la aplicación.

Esta opción encaja con la frase del documento que indica que la aplicación llamará al procedimiento de descarga. Requiere acceso al código o una interfaz de integración facilitada por sus responsables.

### Opción B: herramienta externa

Un programa separado consulta la versión instalada, descarga el paquete y aplica la actualización. Puede iniciarse desde Tytan, manualmente o mediante otro mecanismo que se acuerde.

Esta opción requiere conocer la ruta de instalación, la fuente de la versión y los pasos exactos para actualizar. No se puede asumir que basta con copiar el contenido del ZIP.

**Decisión pendiente:** confirmar con el responsable de Tytan cuál de estas opciones necesita y qué lenguaje o entorno debe utilizarse.

## 4. Descubrir la versión disponible

La dirección HTTPS indica dónde están los paquetes, pero no explica cómo descubrir sus nombres.

Hay que comprobar cuál de estos mecanismos existe:

- Un listado de archivos accesible por HTTPS.
- Un manifiesto con módulos, versiones y rutas de descarga.
- Una API que devuelva la actualización disponible.

Si el servidor solo permite descargar archivos cuyo nombre ya conocemos y no ofrece ninguno de esos mecanismos, habrá que acordar cómo publicar la información de versiones.

Como propuesta, si el equipo puede modificar el servidor, un manifiesto permite consultar las versiones sin depender del formato visual de un listado. Este ejemplo es un diseño posible; **no existe evidencia de que el servidor lo ofrezca**:

```json
{
  "modules": [
    {
      "name": "Faktury",
      "version": "008.000.043",
      "file": "Faktury_008.000.043.zip",
      "sha256": "<hash del ZIP publicado por el responsable>"
    }
  ]
}
```

No implementar un lector de manifiestos ni un analizador de listados hasta confirmar el mecanismo real.

## 5. Comparación de versiones

La versión se comparará por sus componentes numéricos, después de validar el formato acordado.

```text
008.000.043 → (8, 0, 43)
008.002.066 → (8, 2, 66)
```

Se compara primero el primer componente; en caso de empate, el segundo, y después el tercero. Evitar comparar el nombre completo del archivo como texto.

La comparación se hace entre versiones del mismo módulo. `Faktury`, `FK2025` y `FK2026` aparecen como prefijos diferentes y deben tratarse por separado hasta confirmar su significado.

Si hay varios paquetes de un módulo, la regla propuesta es seleccionar la versión más alta aplicable al cliente. Antes de hacerlo, confirmar si los ZIP son completos o si necesitan instalar versiones intermedias.

## 6. Flujo propuesto

```text
Leer configuración del cliente y versión instalada
    ↓
Consultar información de actualizaciones por HTTPS
    ↓
Seleccionar módulo y versión aplicable
    ↓
Comparar con la versión instalada
    ├─ Igual o inferior → terminar sin actualizar
    └─ Superior
         ↓
       Descargar a un archivo temporal
         ↓
       Validar el paquete
         ↓
       Preparar la instalación
         ↓
       Aplicar el procedimiento definido para Tytan
         ↓
       Comprobar el resultado
         ↓
       Registrar la nueva versión
```

No actualizar la versión guardada solo por haber descargado el ZIP. Debe reflejar una instalación completada correctamente.

## 7. Responsabilidades del código

Separación propuesta, independiente del lenguaje que se elija:

| Componente | Responsabilidad |
| --- | --- |
| Configuración | Dirección base, carpeta del cliente, módulos y rutas locales |
| Detección local | Obtener la versión instalada de una fuente acordada |
| Consulta remota | Autenticarse y obtener versiones y archivos disponibles |
| Comparación | Decidir si hay una actualización aplicable |
| Descarga | Obtener el ZIP, gestionar interrupciones y dejar un archivo completo |
| Validación | Comprobar que el paquete se puede leer y verificar el hash si se publica |
| Instalación | Ejecutar los pasos específicos de Tytan y gestionar fallos |
| Registro | Mostrar el resultado y guardar información útil de diagnóstico |

Las credenciales deben suministrarse fuera del repositorio mediante el mecanismo que se acuerde. No incluirlas en ejemplos, registros ni direcciones URL.

## 8. Requisitos de instalación pendientes

Antes de programar la instalación, obtener respuestas a estas preguntas:

1. ¿El ZIP contiene ejecutables, un instalador, scripts SQL u otros archivos?
2. ¿La actualización modifica una base de datos o únicamente archivos del programa?
3. ¿Dónde se instala cada módulo y qué permisos necesita?
4. ¿Debe cerrarse el programa? ¿Hay varios usuarios o equipos usando la misma instalación?
5. ¿Se puede instalar directamente la última versión o hay pasos intermedios?
6. ¿Qué copia de seguridad hace falta y cómo se recupera una instalación fallida?
7. ¿Cómo se verifica que la actualización terminó correctamente?
8. ¿Cómo se guarda o consulta la versión después de instalar?

Si hay cambios en base de datos, restaurar archivos no basta para deshacer la actualización. El procedimiento de recuperación debe definirlo el responsable del producto.

## 9. Fases y criterios de aceptación

### Fase 0: obtener información y ejemplos

Recibir una instalación de prueba, un ZIP de actualización y la información de integración. Verificar cómo responde el servidor y cómo se obtiene la versión local.

**Resultado:** se puede documentar un caso real de principio a fin y decidir el lenguaje y la forma de integración.

### Fase 1: comprobar versiones

Implementar la lectura de la versión local, la consulta remota y la comparación. Mostrar la versión instalada, la disponible y si hay una actualización.

**Resultado:** identifica correctamente versiones superiores, iguales e inferiores, por cliente y módulo, sin modificar la instalación.

### Fase 2: descargar y validar

Descargar el paquete elegido a una ubicación temporal y validarlo. Definir tiempos de espera y reintentos limitados para evitar bucles continuos de descargas.

**Resultado:** entrega un ZIP completo o un error claro; una descarga interrumpida no se presenta como una actualización lista.

### Fase 3: instalar

Implementar el procedimiento aprobado para Tytan, incluyendo cierre del programa, copias de seguridad y recuperación cuando corresponda.

**Resultado:** actualiza una instalación de prueba y registra la versión únicamente tras comprobar el éxito. Un fallo deja un estado conocido y recuperable.

### Fase 4: probar el uso con varios clientes

Comprobar que cada cliente consulta los paquetes que le corresponden y evaluar el comportamiento de descargas simultáneas en un entorno acordado.

**Resultado:** funcionamiento validado y medidas reales del consumo de red. Si sigue existiendo saturación, evaluar distribución de las comprobaciones y mejoras de capacidad o caché junto al equipo del servidor.

## 10. Casos de validación necesarios

- Versión remota superior, igual e inferior a la instalada.
- Varios módulos y varios paquetes del mismo módulo.
- Formato de versión inválido o información remota incompleta.
- Credenciales rechazadas, carpeta ausente y servidor inaccesible.
- Descarga interrumpida, ZIP dañado y falta de espacio.
- Programa en uso o permisos insuficientes durante la instalación.
- Fallo de instalación y ejecución del procedimiento de recuperación.
- Dos intentos de actualización simultáneos en la misma instalación.

Las pruebas se harán con archivos de ejemplo y una instalación de prueba. No realizar pruebas de carga sobre el servidor real sin acordarlas con sus responsables.

## 11. Mensaje para el responsable de Tytan

> Estoy preparando un actualizador que detecte versiones antiguas y obtenga el paquete correspondiente por HTTPS. ¿Debe integrarse en Tytan o ejecutarse como herramienta externa? Necesito conocer el lenguaje y la forma de llamarlo, dónde se guarda la versión instalada, cómo se consultan los archivos disponibles en HTTPS y cómo se aplica el ZIP. ¿Podéis facilitar una instalación de prueba, un paquete de ejemplo y el procedimiento de actualización y recuperación?
