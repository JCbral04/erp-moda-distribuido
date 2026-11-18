# Módulo de IA (visión por computador)

Microservicio independiente en Python + OpenCV.
Lee el QR de la etiqueta de cada prenda (contiene el SKU de la variante) y avisa
al backend (.NET) por HTTP para sumar una unidad al stock. La venta lo descuenta
al confirmarse.

- `deteccion.py`: lector de QR por cámara o video (`--simular` para probar sin backend).
- `generar_video_prueba.py`: genera un video con QR para probar sin cámara.

Responsable: Jair Enrique Polo Chamorro
Detalle: docs/requerimientos/ia.md · Guía: docs/guias/lector-qr-inventario.md
