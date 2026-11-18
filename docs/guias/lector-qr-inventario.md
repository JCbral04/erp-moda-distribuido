# Guía: Entrada de stock por lector de QR

**Issue relacionado:** #107 · **Responsable:** Jair Enrique Polo Chamorro (Inventario + IA) · **Fecha:** 18/11/2026

## ¿Qué se hizo?

Se reemplazó la prueba de concepto de detección de prendas por sustracción de fondo (issue #7, ver `docs/guias/deteccion-prendas-ia.md`) por un **lector de QR**. Cada prenda lleva una etiqueta con un QR cuyo contenido es el **SKU** de su variante. Al escanearlo, el stock de esa variante sube en una unidad; cuando la prenda se vende, el módulo de Ventas lo baja al confirmar la venta (ese descuento ya existía).

El cambio tiene dos partes:

- **IA** (`ai/deteccion.py`): lee los QR de la cámara (o de un video) con `cv2.QRCodeDetector` y, por cada etiqueta nueva, llama al backend. `ai/generar_video_prueba.py` ahora genera un video con QR para probar sin cámara.
- **Inventario** (`POST /api/productos/escaneo-qr`): recibe `{ "sku": "..." }`, suma una unidad al stock de la variante y registra el movimiento.

## Flujo completo

```
Etiqueta QR ("CAM-NEG-M")
   │  cámara
   ▼
ai/deteccion.py ── POST /api/productos/escaneo-qr {"sku":"CAM-NEG-M"} ──▶ Inventario
                                                                          stock +1
                                                                          movimiento tipo='deteccion_ia', cantidad=+1
Venta confirmada (POST /api/ventas/{id}/confirmar) ─────────────────────▶ stock -1
                                                                          movimiento tipo='venta', cantidad<0
```

## Decisiones técnicas y por qué

### 1. El QR contiene solo el SKU

El SKU ya es único en la base de datos (`uq_variantes_sku`) y es legible si se imprime junto al QR. No se usó el `id` interno de la variante porque cambia si se recrea la base, ni una URL porque ataría las etiquetas impresas a un dominio.

### 2. Movimiento `deteccion_ia` con confianza 1

El esquema de `movimientos_stock` ya tenía el tipo `deteccion_ia` para entradas registradas por la IA, sin referencia a venta ni orden de compra. Se guarda `confianza = 1` porque la lectura de un QR es exacta: no aplica la revisión manual por baja confianza de RN-003 (`docs/requerimientos/ia.md`). El usuario del movimiento es `ia-qr` mientras no exista autenticación (#59).

### 3. Una etiqueta cuenta una sola vez mientras siga frente a la cámara

Un QR se queda varios frames en el encuadre. `ControlEscaneos` registra el SKU cuando aparece y no vuelve a contarlo hasta que lleva `--frames-liberacion` frames seguidos sin verse (15 por defecto) o aparece otro QR. Así, dos prendas iguales se cuentan pasando una y luego la otra.

### 4. Transacción Serializable en el backend

La lectura y el incremento del stock van en una transacción `Serializable` en PostgreSQL, igual que en `VentasService`, para que dos escaneos simultáneos del mismo SKU no pierdan una unidad. La limitación es la misma que en Ventas: sin reintentos, un conflicto devuelve 500.

### 5. Sin dependencias nuevas ni cambios en CI

`cv2.QRCodeDetector` y `cv2.QRCodeEncoder` vienen en `opencv-python-headless`, que ya estaba en `ai/requirements.txt`; la llamada HTTP usa `urllib` de la librería estándar. Se conservaron los nombres de archivo (`deteccion.py`, `generar_video_prueba.py`) para que el paso `py_compile` de `ci.yml` siga igual.

### 6. El endpoint vive en `ProductosController`

Así no hace falta registrar un servicio nuevo en `Program.cs`: el método se agregó a `IProductosService`, que ya estaba registrado.

## Uso

Con el backend levantado (por defecto `http://localhost:5000`, que en desarrollo publica `docker-compose.override.yml`, o la variable `BACKEND_URL`; dentro de la red `erp-net` sería `http://backend:8080`):

```bash
python ai/deteccion.py --fuente 0
```

Sin cámara ni backend, para probar solo la lectura:

```bash
python ai/generar_video_prueba.py --salida ai/evidencia/video_qr.avi --skus CAM-NEG-M CAM-BLA-L CAM-NEG-M
python ai/deteccion.py --fuente ai/evidencia/video_qr.avi --simular --salida ai/evidencia/qr
```

Respuestas del endpoint:

| Caso | Respuesta |
|---|---|
| SKU existente | **200** con `varianteId`, `sku`, `producto`, `talla`, `color` y `stock` nuevo |
| SKU que no existe | **404**; no se registra movimiento |
| Cuerpo vacío, SKU vacío o de más de 50 caracteres, campo extra o tipo erróneo | **400** |

## Verificación

| Prueba | Resultado |
|---|---|
| Tests de servicio (`EscaneoQrServiceTests`): +1 por escaneo, movimiento `deteccion_ia`, SKU con espacios, SKU inexistente, SKU vacío, escanear dos veces y vender una deja stock 1 | 8/8 en verde |
| Tests HTTP (`EscaneoQrHttpTests`): 5 cuerpos inválidos → 400 | Verificados también a mano contra la API en Kestrel: los 5 responden 400 sin stack trace |
| Video sintético con `CAM-NEG-M`, `CAM-BLA-L`, `CAM-NEG-M` | 3 escaneos (frames 31, 81 y 131), sin repeticiones mientras cada QR sigue en pantalla |
| `registrar_en_backend` contra un backend simulado | 200 → registrado con stock; 404 → "SKU no registrado"; backend caído → aviso y el lector sigue |

## Pendiente

- Generar e imprimir las etiquetas QR desde el catálogo (hoy se generan a mano o con `generar_video_prueba.py`).
- Probar con cámara física y etiquetas impresas (tamaño mínimo legible, distancia).
- Proteger el endpoint cuando exista autenticación (#59, #84) y registrar el usuario real en el movimiento.
- `POST /detectar` de `ai/main.py` sigue siendo un placeholder; el lector llama directamente a Inventario.
