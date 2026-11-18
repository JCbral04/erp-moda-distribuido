"""Genera un video sintetico con etiquetas QR para probar deteccion.py sin camara.

Muestra cada SKU de la lista como un QR durante unos frames, separados por
frames sin etiqueta. Repetir un SKU en la lista simula escanear dos prendas
iguales. El video no se versiona; se regenera cuando haga falta.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import cv2
import numpy as np

NIVEL_FONDO = 40
RUIDO_FONDO = 4


def _frame_fondo(ancho: int, alto: int, generador: np.random.Generator) -> np.ndarray:
    ruido = generador.normal(NIVEL_FONDO, RUIDO_FONDO, (alto, ancho, 3))
    return np.clip(ruido, 0, 255).astype(np.uint8)


def _imagen_qr(texto: str, lado: int) -> np.ndarray:
    """QR de `texto` en BGR, escalado a `lado` px con su margen blanco."""
    qr = cv2.QRCodeEncoder.create().encode(texto)
    qr = cv2.resize(qr, (lado, lado), interpolation=cv2.INTER_NEAREST)
    return cv2.cvtColor(qr, cv2.COLOR_GRAY2BGR)


def generar_video_sintetico(
    ruta_salida: Path,
    skus: list[str],
    ancho: int = 480,
    alto: int = 360,
    fps: int = 20,
    frames_qr: int = 20,
    frames_vacio: int = 30,
) -> Path:
    ruta_salida.parent.mkdir(parents=True, exist_ok=True)
    escritor = cv2.VideoWriter(
        str(ruta_salida), cv2.VideoWriter_fourcc(*"MJPG"), fps, (ancho, alto)
    )
    if not escritor.isOpened():
        raise RuntimeError(f"No se pudo crear el video en {ruta_salida}")

    generador = np.random.default_rng(seed=7)
    lado = int(min(ancho, alto) * 0.6)
    x, y = (ancho - lado) // 2, (alto - lado) // 2

    try:
        for _ in range(frames_vacio):
            escritor.write(_frame_fondo(ancho, alto, generador))

        for sku in skus:
            etiqueta = _imagen_qr(sku, lado)
            for _ in range(frames_qr):
                frame = _frame_fondo(ancho, alto, generador)
                frame[y : y + lado, x : x + lado] = etiqueta
                escritor.write(frame)
            for _ in range(frames_vacio):
                escritor.write(_frame_fondo(ancho, alto, generador))
    finally:
        escritor.release()

    return ruta_salida


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--salida", type=Path, default=Path("ai/evidencia/video_qr.avi"))
    parser.add_argument(
        "--skus",
        nargs="+",
        default=["CAM-NEG-M", "CAM-BLA-L", "CAM-NEG-M"],
        help="SKUs a mostrar en orden (repetir uno simula dos prendas iguales)",
    )
    parser.add_argument("--ancho", type=int, default=480)
    parser.add_argument("--alto", type=int, default=360)
    parser.add_argument("--fps", type=int, default=20)
    parser.add_argument("--frames-qr", type=int, default=20)
    parser.add_argument("--frames-vacio", type=int, default=30)
    args = parser.parse_args(argv)

    ruta = generar_video_sintetico(
        ruta_salida=args.salida,
        skus=args.skus,
        ancho=args.ancho,
        alto=args.alto,
        fps=args.fps,
        frames_qr=args.frames_qr,
        frames_vacio=args.frames_vacio,
    )
    print(f"Video generado: {ruta}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
