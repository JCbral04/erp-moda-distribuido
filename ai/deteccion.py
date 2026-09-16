"""Prueba de concepto: deteccion de presencia de prendas por camara (issue #7).

Vision clasica con OpenCV: sustraccion de fondo MOG2 para detectar la entrada
de la prenda y un tracker MIL para seguirla mientras permanece en el encuadre.
No clasifica el tipo de prenda, solo detecta presencia.
"""

from __future__ import annotations

import argparse
from datetime import datetime
from pathlib import Path

import cv2
import numpy as np

AREA_MINIMA_RATIO = 0.03
# Salir de la deteccion cuesta mas que entrar: sin esta histeresis el estado
# oscila cuando area_ratio se queda rondando el umbral de entrada.
AREA_SALIDA_RATIO = 0.015
HISTORIAL = 30
UMBRAL_VARIANZA = 16.0
# Tras el calentamiento el fondo se congela (tasa 0): si MOG2 sigue aprendiendo,
# absorbe la prenda quieta en el modelo y la deteccion se apaga sola.
TASA_APRENDIZAJE = 0.0
INTERVALO_EVIDENCIA = 15


def crear_subtractor(
    historial: int = HISTORIAL, umbral_varianza: float = UMBRAL_VARIANZA
) -> cv2.BackgroundSubtractorMOG2:
    return cv2.createBackgroundSubtractorMOG2(
        history=historial, varThreshold=umbral_varianza, detectShadows=False
    )


def crear_tracker() -> cv2.Tracker:
    """MIL es el unico tracker de OpenCV 5 que no necesita pesos externos.

    CSRT y KCF viven en opencv-contrib y los demas (DaSiamRPN, Nano, Vit) exigen
    un archivo de modelo; ninguno de los dos esta disponible en este repo.
    """
    return cv2.TrackerMIL.create()


def detectar_prenda_en_frame(
    frame: np.ndarray,
    subtractor: cv2.BackgroundSubtractorMOG2,
    area_minima_ratio: float = AREA_MINIMA_RATIO,
    tasa_aprendizaje: float = -1.0,
) -> tuple[bool, float, tuple[int, int, int, int] | None]:
    """Devuelve (detectado, area_ratio, bbox).

    area_ratio es el area del contorno mas grande dividida por el area del frame.
    Es una heuristica geometrica, no la "confianza" calibrada de RN-003.
    """
    mascara = subtractor.apply(frame, learningRate=tasa_aprendizaje)
    kernel = np.ones((5, 5), np.uint8)
    mascara = cv2.morphologyEx(mascara, cv2.MORPH_OPEN, kernel)
    mascara = cv2.morphologyEx(mascara, cv2.MORPH_CLOSE, kernel)

    contornos, _ = cv2.findContours(mascara, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contornos:
        return False, 0.0, None

    mayor = max(contornos, key=cv2.contourArea)
    alto, ancho = frame.shape[:2]
    area_ratio = cv2.contourArea(mayor) / float(alto * ancho)
    if area_ratio < area_minima_ratio:
        return False, area_ratio, None

    x, y, w, h = cv2.boundingRect(mayor)
    return True, area_ratio, (x, y, w, h)


def anotar_frame(
    frame: np.ndarray,
    bbox: tuple[int, int, int, int] | None,
    area_ratio: float,
    etiqueta: str = "",
) -> np.ndarray:
    anotado = frame.copy()
    if bbox is not None:
        x, y, w, h = bbox
        cv2.rectangle(anotado, (x, y), (x + w, y + h), (0, 255, 0), 2)
    texto = f"{etiqueta} area_ratio={area_ratio:.4f}".strip()
    cv2.putText(
        anotado, texto, (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2
    )
    return anotado


def procesar_video(
    fuente: int | str,
    area_minima_ratio: float = AREA_MINIMA_RATIO,
    area_salida_ratio: float = AREA_SALIDA_RATIO,
    historial: int = HISTORIAL,
    umbral_varianza: float = UMBRAL_VARIANZA,
    tasa_aprendizaje: float = TASA_APRENDIZAJE,
    directorio_salida: Path | None = None,
    max_frames: int | None = None,
    frames_confirmacion: int = 3,
    intervalo_evidencia: int = INTERVALO_EVIDENCIA,
) -> list[dict]:
    """Recorre la fuente de video y registra la presencia de una prenda.

    Los primeros `historial` frames aprenden el fondo (el encuadre debe estar
    vacio). Despues, MOG2 detecta la entrada de la prenda y el tracker la sigue
    hasta que la pierde o el primer plano cae por debajo de `area_salida_ratio`.
    Se imprime una linea por transicion y, mientras hay prenda, se guarda un
    frame anotado cada `intervalo_evidencia` frames.
    """
    captura = cv2.VideoCapture(fuente)
    if not captura.isOpened():
        raise RuntimeError(f"No se pudo abrir la fuente de video: {fuente!r}")

    if directorio_salida is not None:
        directorio_salida.mkdir(parents=True, exist_ok=True)

    subtractor = crear_subtractor(historial, umbral_varianza)
    eventos: list[dict] = []
    presente = False
    tracker: cv2.Tracker | None = None
    candidatos = 0
    numero_frame = 0
    ultima_evidencia = 0

    def guardar_evidencia(frame, bbox, area_ratio, sufijo) -> None:
        if directorio_salida is None:
            return
        ruta = directorio_salida / f"frame_{numero_frame:05d}_{sufijo}.jpg"
        if not cv2.imwrite(str(ruta), anotar_frame(frame, bbox, area_ratio, sufijo)):
            print(f"  aviso: no se pudo escribir la evidencia {ruta}")

    def registrar_transicion(detectado, area_ratio, bbox) -> None:
        marca = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        eventos.append(
            {
                "frame": numero_frame,
                "timestamp": marca,
                "detectado": detectado,
                "area_ratio": round(area_ratio, 4),
                "bbox": bbox,
            }
        )
        etiqueta = "PRENDA DETECTADA" if detectado else "sin prenda"
        print(
            f"[{marca}] frame {numero_frame}: {etiqueta} "
            f"(area_ratio={area_ratio:.4f})"
        )

    try:
        while max_frames is None or numero_frame < max_frames:
            leido, frame = captura.read()
            if not leido:
                break
            numero_frame += 1

            calentando = numero_frame <= historial
            detectado, area_ratio, bbox = detectar_prenda_en_frame(
                frame,
                subtractor,
                area_minima_ratio,
                tasa_aprendizaje=-1.0 if calentando else tasa_aprendizaje,
            )
            if calentando:
                continue

            if not presente:
                # Se exige ver la prenda varias veces seguidas para no entrar en
                # deteccion por el ruido de un frame suelto.
                candidatos = candidatos + 1 if detectado else 0
                if candidatos < frames_confirmacion:
                    continue
                tracker = crear_tracker()
                tracker.init(frame, bbox)
                presente = True
                candidatos = 0
                ultima_evidencia = numero_frame
                registrar_transicion(True, area_ratio, bbox)
                guardar_evidencia(frame, bbox, area_ratio, "deteccion")
                continue

            # Con la prenda ya detectada manda el tracker: MOG2 solo decide
            # cuando se considera que la prenda salio del encuadre.
            seguido, bbox_seguido = tracker.update(frame)
            if seguido:
                bbox = tuple(int(valor) for valor in bbox_seguido)

            if not seguido or area_ratio < area_salida_ratio:
                candidatos += 1
            else:
                candidatos = 0

            if candidatos >= frames_confirmacion:
                presente = False
                tracker = None
                candidatos = 0
                registrar_transicion(False, area_ratio, None)
                guardar_evidencia(frame, None, area_ratio, "fin")
                continue

            if intervalo_evidencia and numero_frame - ultima_evidencia >= intervalo_evidencia:
                ultima_evidencia = numero_frame
                guardar_evidencia(frame, bbox, area_ratio, "seguimiento")
    except KeyboardInterrupt:
        print("\nInterrumpido por el usuario.")
    finally:
        captura.release()

    print(f"Frames procesados: {numero_frame}")
    return eventos


def _parsear_fuente(valor: str) -> int | str:
    return int(valor) if valor.isdigit() else valor


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--fuente",
        default="0",
        help="Indice de camara (0, 1, ...) o ruta a un archivo de video",
    )
    parser.add_argument("--area-minima-ratio", type=float, default=AREA_MINIMA_RATIO)
    parser.add_argument("--area-salida-ratio", type=float, default=AREA_SALIDA_RATIO)
    parser.add_argument("--historial", type=int, default=HISTORIAL)
    parser.add_argument("--umbral-varianza", type=float, default=UMBRAL_VARIANZA)
    parser.add_argument(
        "--tasa-aprendizaje",
        type=float,
        default=TASA_APRENDIZAJE,
        help="Adaptacion del fondo despues del calentamiento (0 = congelado)",
    )
    parser.add_argument(
        "--salida",
        type=Path,
        default=None,
        help="Carpeta donde guardar los frames anotados",
    )
    parser.add_argument(
        "--intervalo-evidencia",
        type=int,
        default=INTERVALO_EVIDENCIA,
        help="Cada cuantos frames guardar evidencia mientras hay prenda (0 = solo transiciones)",
    )
    parser.add_argument("--max-frames", type=int, default=None)
    args = parser.parse_args(argv)

    eventos = procesar_video(
        fuente=_parsear_fuente(args.fuente),
        area_minima_ratio=args.area_minima_ratio,
        area_salida_ratio=args.area_salida_ratio,
        historial=args.historial,
        umbral_varianza=args.umbral_varianza,
        tasa_aprendizaje=args.tasa_aprendizaje,
        directorio_salida=args.salida,
        max_frames=args.max_frames,
        intervalo_evidencia=args.intervalo_evidencia,
    )
    print(f"Transiciones registradas: {len(eventos)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
