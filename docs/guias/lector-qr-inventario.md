# Guía: Entrada de stock por lector de QR

**Issue relacionado:** #107 · **Responsable:** Jair Enrique Polo Chamorro (Inventario + IA) · **Fecha:** 18/11/2026

## ¿Qué se hizo?

Se reemplazó la prueba de concepto de detección de prendas por sustracción de fondo (issue #7, ver `docs/guias/deteccion-prendas-ia.md`) por un **lector de QR**. Cada prenda lleva una etiqueta con un QR cuyo contenido es el **SKU** de su variante. Al escanearlo, el stock de esa variante sube en una unidad. Cuando la prenda se vende, el módulo de Ventas lo baja al confirmar la venta; ese descuento ya existía y no se modificó.

El cambio tiene dos partes:

- **IA** (`ai/deteccion.py`): lee los QR de la cámara (o de un video) con `cv2.QRCodeDetector` y, por cada etiqueta nueva, llama al backend con un id único por escaneo. Si el backend falla, reintenta con el mismo id. `ai/generar_video_prueba.py` genera un video con QR para probar sin cámara.
- **Inventario** (`POST /api/productos/escaneo-qr`): recibe `{ "sku": "...", "idEscaneo": "<uuid>" }`, suma una unidad al stock de la variante y registra el movimiento. Si recibe de nuevo un id que ya registró, no suma otra vez.

## Flujo completo

```
Etiqueta QR ("CAM-NEG-M")
   │  cámara
   ▼
ai/deteccion.py ── POST /api/productos/escaneo-qr ──────────────────────▶ Inventario
   │                {"sku":"CAM-NEG-M","idEscaneo":"6f1c…"}                 stock +1
   │                                                                       movimiento tipo='deteccion_ia', cantidad=+1,
   │                                                                       motivo='Escaneo QR 6f1c…'
   │  sin respuesta / 5xx
   └── reintenta con el MISMO idEscaneo ─────────────────────────────────▶ ya registrado → 200 duplicado=true, sin sumar

Venta confirmada (POST /api/ventas/{id}/confirmar) ─────────────────────▶ stock -1
                                                                          movimiento tipo='venta', cantidad<0
```

## Decisiones técnicas y por qué

### 1. El QR contiene solo el SKU

El SKU ya es único en la base de datos (`uq_variantes_sku`) y es legible si se imprime junto al QR. No se usó el `id` interno de la variante porque cambia si se recrea la base, ni una URL porque ataría las etiquetas impresas a un dominio.

### 2. Movimiento `deteccion_ia` con confianza 1

El esquema de `movimientos_stock` ya tenía el tipo `deteccion_ia` para entradas registradas por la IA, sin referencia a venta ni orden de compra. Se guarda `confianza = 1` porque la lectura de un QR es exacta: no aplica la revisión manual por baja confianza de RN-003 (`docs/requerimientos/ia.md`). El usuario del movimiento es `ia-qr` mientras no exista autenticación (#59).

### 3. Restricción temporal del endpoint hasta que exista autenticación

El endpoint modifica el stock. La autenticación del proyecto está planeada en #59: JWT y modelo `Usuario` en #82 (JCbral04), y `[Authorize]` con políticas por rol en Inventario en #84. Mientras eso no exista, no se creó un sistema de autenticación paralelo. En su lugar, el atributo `RedPermitidaEscaneoQr` (`Inventario/Filters/RedPermitidaEscaneoQrAttribute.cs`) limita **desde qué redes** se acepta la petición:

| Entorno | Comportamiento |
|---|---|
| `Development` | Se acepta. `docker-compose.override.yml` publica el backend solo en `127.0.0.1`. |
| Cualquier otro (`Production`) | **403** salvo que la IP de origen esté en `EscaneoQr:RedesPermitidas`. **Sin configurar, nadie pasa**; ni siquiera `127.0.0.1` es una excepción implícita, porque detrás de un proxy local todo el tráfico podría parecer local. |

- **Formato de la configuración:** IPs o redes CIDR separadas por comas, en la variable de entorno `EscaneoQr__RedesPermitidas`; por ejemplo, `172.18.0.0/16` o `10.0.0.5, 192.168.1.0/24`. Las entradas inválidas se ignoran y nunca dan acceso. Una red CIDR debe escribirse con su dirección base (`172.18.0.0/16`, no `172.18.0.5/16`).
- **IP de origen:** se usa la que deja `UseForwardedHeaders` (#93). Detrás del proxy de confianza (`ProxyTrustedIP`) es la del cliente real.
- **Dónde corre el lector:** si corre dentro de la red `erp-net`, se configura la subred de esa red (`docker network inspect erp-net`). Si corre en un PC de la tienda, se configura la IP de ese PC.
- **Logs:** el rechazo se registra como advertencia, sin la IP del cliente (#88).
- **Habilitar en producción:** el `docker-compose.yml` de producción no pasa todavía esta variable al backend, y ese archivo es de CODEOWNERS (JCbral04). En el PR se propone añadir `EscaneoQr__RedesPermitidas: ${ESCANEO_QR_REDES_PERMITIDAS:-}` al servicio `backend` y la variable a `.env.example`.
- **Cuando llegue #84:** este atributo se reemplaza por `[Authorize]` con la política que se defina para el lector, y el movimiento guardará el usuario real en lugar de `ia-qr`.

### 4. Idempotencia: cada escaneo tiene un id y reintentar no duplica

Cada vez que el lector cuenta una etiqueta nueva genera un `idEscaneo` (UUID v4) y lo envía con el SKU. El backend guarda el movimiento con `motivo = 'Escaneo QR <idEscaneo>'` y, antes de sumar, busca si ese id ya está registrado:

| Situación | Respuesta |
|---|---|
| Id nuevo | **200**, `duplicado: false`, stock +1 |
| Id ya registrado con el mismo SKU (reintento) | **200**, `duplicado: true`, sin sumar; `stock` es el stock actual |
| Id ya registrado con otro SKU | **409**, sin cambios |

Con esto el lector puede reintentar sin miedo a sumar de más. Esto cubre el caso de la review en que el backend procesa la petición pero la respuesta se pierde: el reintento recibe `duplicado: true`.

La búsqueda y el incremento van en la misma transacción `Serializable`, igual que en `VentasService`. Además, `docs/inventario/schema.sql` añade el índice único parcial `uq_mov_escaneo_qr` sobre `motivo` (solo para `tipo = 'deteccion_ia'` con motivo `Escaneo QR %`). Si dos peticiones con el mismo id llegan a la vez, la base de datos impide que ambas registren.

Se usó la columna `motivo` (texto libre, `VARCHAR(255)`) en lugar de una columna nueva para no cambiar el modelo `MovimientoStock` que también usa Ventas: una columna nueva en el modelo rompería las consultas de cualquier base que no la tenga creada. Si más adelante se quiere una columna `id_escaneo UUID UNIQUE`, el cambio queda localizado en `ProductosService`.

**En una base ya creada:** el índice hay que crearlo a mano, porque `schema.sql` se aplica manualmente:

```sql
CREATE UNIQUE INDEX uq_mov_escaneo_qr ON movimientos_stock (motivo)
    WHERE tipo = 'deteccion_ia' AND motivo LIKE 'Escaneo QR %';
```

Sin el índice la transacción `Serializable` sigue evitando el duplicado; el índice es una segunda barrera.

### 5. Recuperación ante fallos del backend en el lector

`ColaRegistro` (en `ai/deteccion.py`) envía los escaneos y decide qué hacer con cada respuesta:

| Respuesta | Qué hace el lector |
|---|---|
| 200 | Registrado. Si `duplicado: true`, ya estaba registrado. |
| Sin conexión, timeout, conexión cerrada sin respuesta, respuesta ilegible, 5xx, 408, 429 | **Reintenta con el mismo `idEscaneo`.** Espera 1 s, 2 s, 4 s… hasta 30 s entre intentos. |
| 400, 403, 404, 409 | No reintenta: el problema no se arregla repitiendo. Lo muestra como `NO REGISTRADO`. |

- **Mientras el backend no responde:** no se insiste con los demás pendientes en la misma pasada, para no bloquear la lectura de la cámara con un timeout por cada escaneo.
- **Al terminar el video:** el lector espera hasta `--espera-final` segundos (10 por defecto) a que se registren los pendientes.
- **Si el lector se cierra:** con `--pendientes RUTA.json` los escaneos sin confirmar se guardan en disco y se reintentan con su mismo id al volver a arrancar. Sin esa opción, los pendientes se muestran al salir y se pierden.

Esto también cubre la limitación conocida del backend: sin reintentos internos, un conflicto de la transacción `Serializable` devuelve 500. En la prueba contra PostgreSQL con 20 prendas distintas escaneadas a la vez, 18 peticiones fallaron con 500 en la primera ráfaga, y tras los reintentos del lector el stock subió exactamente 20 (ver Verificación).

### 6. Cuándo una etiqueta cuenta como prenda nueva, y sus limitaciones físicas

Una etiqueta se queda varios frames en el encuadre, así que no cada lectura es una prenda. `ControlEscaneos` lleva, por cada SKU, cuántas etiquetas están "activas" (ya contadas y aún en el encuadre):

- El lector usa `detectAndDecodeMulti`, que lee **varios QR en el mismo frame**. Si en un frame se ven más etiquetas de un SKU que las activas, cada etiqueta adicional cuenta como una prenda nueva. Dos prendas iguales juntas frente a la cámara suman 2.
- Si se ven menos etiquetas que las activas durante `--frames-liberacion` frames **seguidos** (15 por defecto, 0,5 s a 30 fps), las que faltan se dan por retiradas, y una etiqueta igual que aparezca después vuelve a contar.
- Si una etiqueta deja de leerse menos frames que ese umbral (desenfoque, ángulo, la mano la tapa), no se cuenta dos veces.

**Limitaciones físicas, comprobadas en las pruebas:**

- **Dos etiquetas del mismo SKU tienen exactamente el mismo QR.** El lector no puede distinguir "la misma prenda que sigue frente a la cámara" de "otra prenda igual que la reemplazó".
- **Si la segunda prenda entra antes de que la primera lleve el umbral fuera del encuadre, se cuentan como una**, sea sin ningún frame vacío entre ambas o con un hueco menor que `--frames-liberacion`. Tampoco se distingue si, con dos etiquetas iguales en pantalla, una sale y entra otra antes del umbral.
- **Lo contrario también puede pasar:** si una etiqueta deja de leerse durante el umbral o más sin haberse retirado (tapada, muy inclinada), al volver a leerse cuenta otra vez.
- **El umbral es un compromiso:** más alto evita dobles conteos por parpadeo de la lectura, pero exige más separación entre prendas iguales. Se ajusta con `--frames-liberacion` según la cámara y los fps.

Operativamente, al pasar varias prendas del mismo SKU hay que **retirar cada una del encuadre al menos medio segundo antes de pasar la siguiente**, o ponerlas juntas a la vez frente a la cámara. Si se necesitara contar unidades idénticas sin esa regla, la solución sería que cada etiqueta lleve un número de serie además del SKU (p. ej. `CAM-NEG-M#0001`). Eso cambia el formato de las etiquetas y queda fuera de este issue.

### 7. Sin dependencias nuevas ni cambios en CI

`cv2.QRCodeDetector` y `cv2.QRCodeEncoder` vienen en `opencv-python-headless`, que ya estaba en `ai/requirements.txt`. La llamada HTTP usa `urllib` y las pruebas usan `unittest` y `http.server`, todo de la librería estándar. Se conservaron los nombres de archivo (`deteccion.py`, `generar_video_prueba.py`) para que el paso `py_compile` de `ci.yml` siga igual. Las pruebas de Python no corren en CI porque `.github/` es de CODEOWNERS; en el PR se propone el paso.

### 8. El endpoint vive en `ProductosController`

Así no hace falta registrar un servicio nuevo en `Program.cs`: el método se agregó a `IProductosService`, que ya estaba registrado, y el filtro se aplica como atributo sin registrarlo en el contenedor.

## Uso

Con el backend levantado (por defecto `http://localhost:5000`, que en desarrollo publica `docker-compose.override.yml`, o la variable `BACKEND_URL`; dentro de la red `erp-net` sería `http://backend:8080`):

```bash
python ai/deteccion.py --fuente 0 --pendientes ai/evidencia/pendientes.json
```

Sin cámara ni backend, para probar solo la lectura:

```bash
python ai/generar_video_prueba.py --salida ai/evidencia/video_qr.avi --skus CAM-NEG-M CAM-BLA-L CAM-NEG-M
python ai/deteccion.py --fuente ai/evidencia/video_qr.avi --simular --salida ai/evidencia/qr
```

Opciones del lector:

| Opción | Para qué |
|---|---|
| `--fuente` | Índice de cámara (`0`, `1`…) o ruta a un video |
| `--backend` | URL base del backend; por defecto `$BACKEND_URL` o `http://localhost:5000` |
| `--simular` | Solo lee los QR, sin llamar al backend |
| `--frames-liberacion` | Frames seguidos sin ver una etiqueta para que una igual vuelva a contar (15) |
| `--pendientes` | JSON donde se guardan los escaneos sin confirmar para reintentarlos al volver a arrancar |
| `--espera-final` | Segundos máximos para reintentar pendientes al terminar el video (10) |
| `--salida` | Carpeta donde guardar el frame anotado de cada escaneo |
| `--max-frames` | Límite de frames a procesar |

Respuestas del endpoint:

| Caso | Respuesta |
|---|---|
| SKU existente, id nuevo | **200** con `idEscaneo`, `varianteId`, `sku`, `producto`, `talla`, `color`, `stock` nuevo y `duplicado: false` |
| Mismo id con el mismo SKU (reintento) | **200** con `duplicado: true`; no suma |
| Mismo id con otro SKU | **409**; no suma |
| SKU que no existe | **404**; no se registra movimiento |
| Cuerpo vacío, SKU vacío o de más de 50 caracteres, sin `idEscaneo`, `idEscaneo` inválido o vacío (`0000…`), campo extra o tipo erróneo | **400** |
| Fuera de `Development` desde una red no permitida | **403** |

## Verificación

Lo que se ejecutó realmente, el 18/11/2026:

| Prueba | Resultado |
|---|---|
| Tests de backend sin HTTP (`dotnet test`, filtro sin `HttpTests` ni `IntegrationTests`): incluye los nuevos de servicio (idempotencia, conflicto 409, id vacío, varias prendas del mismo SKU, escanear y vender, reintento después de vender) y del filtro de red | **99/99** en verde |
| Tests HTTP con `WebApplicationFactory` (`EscaneoQrHttpTests`, incluido el 403 en Production) | No se pudieron ejecutar en local: en este equipo solo está .NET 10 y todos los tests HTTP, también los de Ventas en `main`, fallan con `PipeWriter.UnflushedBytes` (500). CI usa .NET 8. Los mismos casos se probaron a mano contra la API real (fila siguiente). |
| API real (Kestrel en `Production`) + PostgreSQL 16 con `docs/inventario/schema.sql` y `docs/ventas/schema.sql` | 403 sin `RedesPermitidas`, sin sumar stock. Los 8 cuerpos inválidos dan 400 sin stack trace. 200 / duplicado / 409 / 404 según la tabla. **20 peticiones simultáneas con el mismo id → +1.** **20 ids distintos a la vez → 18 fallan con 500 en la ráfaga; con los reintentos del lector, +20 exactos**. Venta creada y confirmada descuenta 1; un reintento posterior de un escaneo ya registrado responde duplicado y no cambia el stock. En la base, ningún `motivo` de `deteccion_ia` repetido. |
| Lector de extremo a extremo contra esa API | Video `CAM-NEG-M, CAM-BLA-L, CAM-NEG-M` → 3 registrados. Con el backend caído, el escaneo queda pendiente en `--pendientes`; al volver a arrancar con el backend arriba se registra con el mismo id y el archivo se borra. |
| Tests del lector (`python -m unittest discover -s ai/tests -v`) | **44/44** en verde con Python 3.13 y OpenCV 5.0.0 (ver la siguiente tabla) |

Qué cubren los tests del lector (`ai/tests/test_deteccion.py`):

| Grupo | Casos |
|---|---|
| `ControlEscaneos` | Una etiqueta muchos frames → 1; parpadeo de 5 y de 14 frames → 1; mismo SKU con hueco de 15 y de 40 frames → 2; **sin hueco → 1** y **hueco de 5 → 1** (limitación); umbral configurable; dos iguales a la vez → 2; la segunda entra con la primera en pantalla → 2; una de dos sale y entra otra tras el umbral → 3; SKU distintos alternados y a la vez |
| Frames reales (QR generados con OpenCV) | Lectura de 1 y de 2 QR iguales en un frame; mismo SKU sin hueco, con hueco de 5 y con hueco de 15 frames; dos a la vez; la segunda entra con la primera en pantalla |
| Video MJPG generado con `generar_video_prueba.py` | Huecos de 30, de 15 y de 5 frames (3, 2 y 1 escaneos) |
| `registrar_en_backend` contra un backend simulado idempotente | Envío de SKU e id; duplicado; 400/403/404/409 no reintentables; 500, backend caído y **respuesta perdida** (el backend suma y cierra la conexión) reintentables |
| `ColaRegistro` | Reintento con el mismo id; espera creciente con máximo; sin reintento en fallos permanentes; no insiste con los demás si el backend no responde; pendientes guardados y recuperados; `vaciar` con y sin éxito dentro del límite |
| Extremo a extremo con el backend simulado | Respuesta perdida → reintento → `duplicado`, stock +1; 500 y 503 → registrado al tercer intento, stock +1; dos prendas iguales → dos ids, stock +2; backend caído → queda pendiente |

## Pendiente

- Proteger el endpoint con `[Authorize]` cuando exista autenticación (#82, #84) y registrar el usuario real en el movimiento.
- Pasar `EscaneoQr__RedesPermitidas` al backend en `docker-compose.yml` y documentarla en `.env.example` (JCbral04, CODEOWNERS).
- Añadir `python -m unittest discover -s ai/tests` al job `ia` de `ci.yml` (JCbral04, CODEOWNERS).
- Generar e imprimir las etiquetas QR desde el catálogo (hoy se generan a mano o con `generar_video_prueba.py`), y valorar el número de serie por unidad si la regla de separar prendas iguales no es práctica.
- Probar con cámara física y etiquetas impresas (tamaño mínimo legible, distancia, ajuste de `--frames-liberacion`).
- `POST /detectar` de `ai/main.py` sigue siendo un placeholder; el lector llama directamente a Inventario.
