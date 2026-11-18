# Módulo de IA (visión por computador)

Microservicio independiente en Python + OpenCV.
Lee el QR de la etiqueta de cada prenda (contiene el SKU de la variante) y avisa
al backend (.NET) por HTTP para sumar una unidad al stock. La venta lo descuenta
al confirmarse.

- `deteccion.py`: lector de QR por cámara o video (`--simular` para probar sin backend).
  Cada escaneo lleva un id único y se reintenta con ese mismo id si el backend falla,
  así no se suma dos veces (`--pendientes` guarda los no confirmados en disco).
- `generar_video_prueba.py`: genera un video con QR para probar sin cámara.
- `tests/`: pruebas del lector (`python -m unittest discover -s ai/tests -v` desde la raíz).

Responsable: Jair Enrique Polo Chamorro
Detalle: docs/requerimientos/ia.md · Guía: docs/guias/lector-qr-inventario.md
