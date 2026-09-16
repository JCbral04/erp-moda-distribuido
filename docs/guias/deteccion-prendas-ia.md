# Guía: Detección de prendas con cámara (prueba de concepto)

**Issue relacionado:** #7 · **Responsable:** Jair Enrique Polo Chamorro (Inventario + IA) · **Fecha:** 15/09/2026

## ¿Qué se hizo?

Se construyó una prueba de concepto de visión por computador para el microservicio de IA: detectar que hay una prenda frente a la cámara. El issue pedía **viabilidad**, no un detector de producción, así que se resolvió con visión clásica de OpenCV sin ningún modelo de deep learning y sin agregar dependencias nuevas al `ai/requirements.txt`: la sustracción de fondo MOG2 detecta la entrada de la prenda y un tracker MIL la sigue mientras permanece en el encuadre. Se agregaron dos scripts: `ai/deteccion.py` (el detector, usable con webcam o con un archivo de video) y `ai/generar_video_prueba.py` (genera un video sintético para poder verificar el detector sin cámara física ni fixtures versionados).

## Decisiones técnicas y por qué

### 1. Modelos y librerías evaluados

**Detección:**

| Enfoque | Librería | Resultado |
|:---|:---|:---|
| Sustracción de fondo MOG2 + contornos | `cv2` (ya en el repo) | **Elegido** |
| Diferencia entre frames consecutivos | `cv2` (ya en el repo) | Descartado |
| Umbral de color HSV + contornos | `cv2` (ya en el repo) | Descartado |
| YOLO / EfficientDet | `ultralytics`, `torch` | Descartado |
| Modelo pre-entrenado vía `cv2.dnn` | `cv2` + archivo de pesos | Descartado |

**Seguimiento:**

| Tracker | Disponibilidad en el repo | Resultado |
|:---|:---|:---|
| `cv2.TrackerMIL` | Incluido en `opencv-python-headless` | **Elegido** |
| `cv2.TrackerCSRT` / `TrackerKCF` | Solo en `opencv-contrib-python` | Descartado |
| `TrackerDaSiamRPN` / `TrackerNano` / `TrackerVit` | Requieren archivo de pesos | Descartado |

- **MOG2 + contornos (elegido):** construye un modelo estadístico del fondo, así que basta con dejar el encuadre vacío unos segundos para que cualquier objeto nuevo quede marcado como primer plano. Filtrando por el área del contorno más grande se distingue una prenda de ruido puntual. Solo usa `cv2` y `numpy`, que ya estaban declarados en `ai/requirements.txt`.
- **Diferencia entre frames:** es lo más simple, pero solo reacciona al *movimiento*. Una prenda colgada quieta frente a la cámara deja de producir diferencia y el detector la "pierde" a los pocos frames. No sirve para detectar presencia sostenida.
- **Umbral de color HSV:** exige conocer de antemano el rango de color de la prenda. Una tienda de moda vende prendas de cualquier color, así que habría que recalibrar por prenda. Descartado por poco general.
- **YOLO / EfficientDet:** son la opción correcta cuando haya que **clasificar** el tipo de prenda, pero requieren `torch`/`ultralytics`, que no están en `ai/requirements.txt`; agregarlos contradice el alcance de "solo viabilidad" y multiplica el tamaño de la imagen Docker del microservicio.
- **`cv2.dnn` con pesos pre-entrenados:** evitaría `torch`, pero necesita un archivo de modelo (`.onnx`/`.weights`) que no existe en el repo y para el que no hay todavía ninguna convención de almacenamiento ni de descarga. Queda para cuando se decida el modelo de producción (`docs/requerimientos/ia.md` §7).
- **`cv2.TrackerMIL` (elegido para seguimiento):** en OpenCV 5, CSRT y KCF — que serían mejores — se movieron al paquete `opencv-contrib-python`, que no está en `ai/requirements.txt`; y los trackers basados en redes (`DaSiamRPN`, `Nano`, `Vit`) exigen un archivo de pesos inexistente en el repo. MIL es el único que viene en el paquete headless ya declarado y funciona sin descargar nada. Es más lento y deriva más que CSRT, algo aceptable para una PoC.

### 2. El fondo se congela después del calentamiento

Los primeros `--historial` frames (30 por defecto) alimentan el modelo de fondo con el encuadre vacío; a partir de ahí el modelo deja de aprender (`--tasa-aprendizaje 0`). Sin esto, MOG2 absorbe la prenda quieta dentro del fondo y la detección se apaga sola a los pocos frames (ver "Incidencia"). La contrapartida es que el detector asume iluminación estable y encuadre vacío al arrancar; por eso la tasa quedó como parámetro de CLI y no fija en el código.

### 3. `area_ratio` es una heurística, no la "confianza" de RN-003

El script reporta `area_ratio` = área del contorno más grande / área del frame. Sirve para ordenar detecciones de mayor a menor tamaño, pero **no** es la confianza calibrada que RN-003 (`docs/requerimientos/ia.md`) exige para decidir si una detección necesita confirmación manual, ni la columna `confianza` de `movimientos_stock` (`docs/inventario/schema.sql`). Esa métrica llega cuando exista un modelo real que entregue probabilidades.

### 4. Evidencia por consola e imágenes, no por ventana

`ai/requirements.txt` fija `opencv-python-headless`, que no incluye GUI: `cv2.imshow` no existe. En vez de una ventana en vivo, el script imprime una línea con timestamp en cada **transición** de estado (no en cada frame, para no inundar el log en una sesión larga de webcam) y guarda con `cv2.imwrite` los frames anotados en la carpeta que se pase por `--salida`. Además de las transiciones, mientras la prenda está presente se guarda un frame de seguimiento cada `--intervalo-evidencia` frames (15 por defecto, `0` lo desactiva): así se ve la prenda a lo largo de toda su permanencia y no solo en el instante en que entró.

### 5. Tres frames de confirmación, histéresis y tracker para no oscilar

Tres mecanismos evitan que el estado alterne con el ruido:

1. **Confirmación:** el cambio de estado necesita tres frames consecutivos coincidentes, por eso en los logs la detección aparece dos frames después del frame real en que entra la prenda.
2. **Histéresis:** entrar exige `--area-minima-ratio` (0.03) pero salir exige caer por debajo de `--area-salida-ratio` (0.015). Sin esto, un `area_ratio` que se queda rondando el umbral único produce una ráfaga de transiciones; es exactamente lo que pasó en la primera prueba con cámara real.
3. **Tracker:** una vez detectada la prenda, `TrackerMIL` la sigue frame a frame, de modo que la presencia se sostiene aunque MOG2 pierda momentáneamente el contorno. La detección termina cuando el tracker pierde el objeto **o** el primer plano cae bajo el umbral de salida.

### 6. El video sintético no se versiona

`ai/generar_video_prueba.py` regenera el video de prueba cuando haga falta, y `ai/evidencia/` está en `.gitignore`. El repo no tiene precedente de fixtures binarios y un video versionado solo inflaría el historial.

### 7. `POST /detectar` queda sin tocar

El criterio de aceptación pide un *script*, no un endpoint. Exponer esto por HTTP obliga a decisiones que el issue no pide (cómo se sube el video o el frame, cómo evitar `python-multipart` — que tampoco está en `requirements.txt` —, cómo cubrirlo en CI). El stub de `ai/main.py` se deja igual y la integración real queda listada en "Pendiente".

## Incidencia durante la implementación

**Síntoma:** en la primera corrida contra el video sintético, la prenda entraba en el frame 61 y salía en el 100, pero el log marcaba `PRENDA DETECTADA` en el frame 63 y `sin prenda` ya en el frame 66 — la detección duraba 3 frames en vez de 40.

**Causa raíz:** `cv2.createBackgroundSubtractorMOG2` sigue aprendiendo en cada `apply()`. Con `history=30`, el rectángulo que simula la prenda se incorporaba al modelo de fondo casi de inmediato y dejaba de contar como primer plano.

**Solución:** separar explícitamente el calentamiento de la operación. Durante los primeros `historial` frames se llama a `apply()` con `learningRate=-1` (aprendizaje automático); después, con `learningRate=0`, que congela el fondo. Tras el cambio, la misma corrida reporta detección en el frame 63 y fin en el 103, que es exactamente el segmento de la prenda más los 3 frames de confirmación.

**Lección:** en MOG2 "adaptativo" no significa "recuerda lo que entró"; para detectar *presencia sostenida* (y no solo movimiento) hay que controlar la tasa de aprendizaje a mano.

## Verificación

Entorno: Python 3.13 local con las versiones exactas de `ai/requirements.txt` instaladas en un entorno virtual (`cv2 5.0.0`, `numpy 2.5.3`). CI usa Python 3.12; el único chequeo del job `ia` es de sintaxis.

Sintaxis, igual que en CI:

```bash
python -m py_compile ai/main.py ai/deteccion.py ai/generar_video_prueba.py
```
→ `py_compile OK`.

Generación del video sintético (60 frames de fondo → 40 frames con la "prenda" cruzando → 60 frames de fondo):

```bash
python ai/generar_video_prueba.py --salida ai/evidencia/video_prueba.avi
```
→ `Video generado: ai\evidencia\video_prueba.avi`.

Detección sobre ese video:

```bash
python ai/deteccion.py --fuente ai/evidencia/video_prueba.avi --salida ai/evidencia/
```
→
```
[2026-09-15 22:01:31] frame 63: PRENDA DETECTADA (area_ratio=0.1988)
[2026-09-15 22:01:34] frame 103: sin prenda (area_ratio=0.0000)
Transiciones registradas: 2
```

| Tramo del video | Frames | Resultado esperado | Resultado obtenido |
|:---|:---|:---|:---|
| Calentamiento del fondo | 1–30 | Sin reporte | Sin reporte |
| Fondo vacío | 31–60 | Sin detección (sin falsos positivos) | Sin detección |
| Prenda en el encuadre | 61–100 | Detección sostenida | Detectada en frame 63, sostenida |
| Fondo vacío otra vez | 101–160 | Fin de la detección | Fin en frame 103 |

Se generaron en `ai/evidencia/` los frames anotados `frame_00063_deteccion.jpg` (bounding box verde sobre la prenda + `area_ratio=0.1988`), `frame_00078_seguimiento.jpg` y `frame_00093_seguimiento.jpg` (el tracker acompaña al rectángulo mientras cruza el encuadre) y `frame_00103_fin.jpg`.

### Cámara real, primera versión (solo MOG2 + umbral único)

Prueba con cámara USB real (webcam del equipo del responsable), 300 frames. Es la corrida que motivó el tracking y la histéresis, y queda registrada como punto de comparación:

```bash
python ai/deteccion.py --fuente 0 --salida ai/evidencia/ --max-frames 300
```
→
```
[2026-09-15 21:53:05] frame 33: PRENDA DETECTADA (area_ratio=0.2929)
[2026-09-15 21:53:07] frame 59: sin prenda (area_ratio=0.0078)
[2026-09-15 21:53:08] frame 77: PRENDA DETECTADA (area_ratio=0.1200)
[2026-09-15 21:53:11] frame 123: sin prenda (area_ratio=0.0277)
[2026-09-15 21:53:12] frame 127: PRENDA DETECTADA (area_ratio=0.0343)
[2026-09-15 21:53:13] frame 151: sin prenda (area_ratio=0.0294)
[2026-09-15 21:53:14] frame 169: PRENDA DETECTADA (area_ratio=0.1888)
[2026-09-15 21:53:16] frame 197: sin prenda (area_ratio=0.0150)
[2026-09-15 21:53:19] frame 232: PRENDA DETECTADA (area_ratio=0.2208)
[2026-09-15 21:53:21] frame 264: sin prenda (area_ratio=0.0275)
[2026-09-15 21:53:22] frame 287: PRENDA DETECTADA (area_ratio=0.0917)
Transiciones registradas: 11
```

La viabilidad queda demostrada: con cámara real el detector reacciona a la prenda entrando y saliendo del encuadre, y los frames anotados de `ai/evidencia/` muestran el bounding box sobre la región correcta. Tres observaciones honestas de esta corrida, que son justamente el trabajo de calibración que queda pendiente:

1. **La detección disparó en el frame 33**, apenas terminado el calentamiento (frames 1–30) y con `area_ratio=0.2929`. El encuadre no estaba vacío ni quieto durante el calentamiento, así que parte de la escena real entró al modelo de fondo mal aprendida. En operación hay que garantizar unos segundos de encuadre vacío al arrancar.
2. **Oscilaba cerca del umbral.** Varias transiciones a "sin prenda" traen `area_ratio` de 0.0275–0.0294 contra un `--area-minima-ratio` de 0.03: el detector estaba decidiendo justo en el borde, y de ahí salen 11 transiciones en 300 frames. Los 3 frames de confirmación amortiguan el ruido de un frame suelto, pero no un valor que se queda rondando el umbral. **Corregido** con la histéresis (`--area-salida-ratio`) y el tracker descritos en la decisión 5.
3. **Detecta presencia de cualquier objeto, no solo de prendas.** En varios frames el bounding box encuadró el brazo de quien sostenía la prenda en vez de la prenda misma. Es el comportamiento esperado de un detector de presencia sin clasificación —el contorno más grande del primer plano puede ser la persona— y es exactamente lo que resolvería un modelo entrenado o el filtrado con Haar cascades (ver "Pendiente").

### Cámara real, versión final (tracking MIL + histéresis)

Misma cámara y mismo comando, ya con el código definitivo:

```bash
python ai/deteccion.py --fuente 0 --salida ai/evidencia/ --max-frames 300
```
→
```
[2026-09-15 22:13:11] frame 48: PRENDA DETECTADA (area_ratio=0.1337)
[2026-09-15 22:13:15] frame 107: sin prenda (area_ratio=0.0101)
[2026-09-15 22:13:17] frame 141: PRENDA DETECTADA (area_ratio=0.0719)
[2026-09-15 22:13:24] frame 253: sin prenda (area_ratio=0.0108)
[2026-09-15 22:13:26] frame 274: PRENDA DETECTADA (area_ratio=0.1069)
Frames procesados: 300
Transiciones registradas: 5
```

| Métrica | Solo MOG2 | Con tracking + histéresis |
|:---|:---|:---|
| Transiciones en 300 frames | 11 | 5 |
| `area_ratio` en las salidas | 0.0150–0.0294 (rozando el umbral) | 0.0101–0.0108 (encuadre realmente despejado) |
| Detección sostenida más larga | 46 frames (169→197... con cortes) | 112 frames (141→253, seguidos) |

Las salidas ya no ocurren por oscilación alrededor del umbral sino porque la prenda efectivamente sale del encuadre, y el tramo 141→253 muestra al tracker sosteniendo la prenda durante ~7 segundos continuos. Se generaron en `ai/evidencia/` los frames anotados de las transiciones y de seguimiento (`frame_00048_deteccion.jpg`, `frame_00063/78/93_seguimiento.jpg`, `frame_00107_fin.jpg`, `frame_00141_deteccion.jpg`, …).

Confirmado el 15/09/2026. Criterios de aceptación del issue #7 cumplidos.

## Pendiente

- Elegir el modelo de producción (YOLO / EfficientDet / custom) y resolver dónde se almacenan los pesos — `docs/requerimientos/ia.md` §7.
- Clasificar el **tipo** de prenda (HU-001); esta PoC solo detecta presencia.
- Integración HTTP real con Inventario (ID de producto, acción +1/-1, confianza) y wiring del endpoint `POST /detectar`, una vez Inventario tenga persistencia y un endpoint de ingestión.
- Confianza calibrada que cumpla RN-003, en reemplazo de `area_ratio`.
- Calibrar `--area-minima-ratio`, `--area-salida-ratio`, `--historial` y `--tasa-aprendizaje` contra la cámara y la iluminación reales de la tienda; los valores actuales se ajustaron contra el video sintético.
- Distinguir prenda de persona u otros objetos; hoy cualquier cambio suficientemente grande en el encuadre cuenta como detección. Los Haar cascades incluidos en OpenCV permitirían excluir a la persona sin dependencias nuevas.
- Evaluar `opencv-contrib-python` si el seguimiento con MIL resulta insuficiente: daría acceso a CSRT/KCF, más precisos, a cambio de una dependencia nueva.
- Manejar cambios de iluminación con el fondo congelado (hoy provocarían falsos positivos hasta reiniciar el script).
