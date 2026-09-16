"""Genera un video sintetico para probar deteccion.py sin camara fisica (issue #7).

Produce: fondo liso -> un rectangulo claro que cruza el encuadre (simula la
prenda) -> fondo liso. El video no se versiona; se regenera cuando haga falta.
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


def generar_video_sintetico(
    ruta_salida: Path,
    ancho: int = 320,
    alto: int = 240,
    fps: int = 20,
    frames_fondo: int = 60,
    frames_objeto: int = 40,
) -> Path:
    ruta_salida.parent.mkdir(parents=True, exist_ok=True)
    escritor = cv2.VideoWriter(
        str(ruta_salida), cv2.VideoWriter_fourcc(*"MJPG"), fps, (ancho, alto)
    )
    if not escritor.isOpened():
        raise RuntimeError(f"No se pudo crear el video en {ruta_salida}")

    generador = np.random.default_rng(seed=7)
    ancho_objeto, alto_objeto = ancho // 3, int(alto * 0.6)
    y = (alto - alto_objeto) // 2

    try:
        for _ in range(frames_fondo):
            escritor.write(_frame_fondo(ancho, alto, generador))

        for i in range(frames_objeto):
            frame = _frame_fondo(ancho, alto, generador)
            avance = i / max(frames_objeto - 1, 1)
            x = int((ancho - ancho_objeto) * avance)
            cv2.rectangle(
                frame,
                (x, y),
                (x + ancho_objeto, y + alto_objeto),
                (200, 205, 210),
                thickness=-1,
            )
            escritor.write(frame)

        for _ in range(frames_fondo):
            escritor.write(_frame_fondo(ancho, alto, generador))
    finally:
        escritor.release()

    return ruta_salida


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--salida", type=Path, default=Path("ai/evidencia/video_prueba.avi"))
    parser.add_argument("--ancho", type=int, default=320)
    parser.add_argument("--alto", type=int, default=240)
    parser.add_argument("--fps", type=int, default=20)
    parser.add_argument("--frames-fondo", type=int, default=60)
    parser.add_argument("--frames-objeto", type=int, default=40)
    args = parser.parse_args(argv)

    ruta = generar_video_sintetico(
        ruta_salida=args.salida,
        ancho=args.ancho,
        alto=args.alto,
        fps=args.fps,
        frames_fondo=args.frames_fondo,
        frames_objeto=args.frames_objeto,
    )
    print(f"Video generado: {ruta}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
