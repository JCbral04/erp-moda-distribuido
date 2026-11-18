"""Lector de QR de prendas: cada QR escaneado suma una unidad al stock.

La etiqueta de cada prenda lleva un QR cuyo contenido es el SKU de la variante
(p. ej. "CAM-NEG-M"). Al leerlo, el lector llama al backend
(POST /api/productos/escaneo-qr) y este registra la entrada de +1 unidad como
movimiento tipo 'deteccion_ia'. La salida del stock la hace el modulo de Ventas
al confirmar la venta.

Reemplaza la prueba de concepto de deteccion por sustraccion de fondo (issue #7).
"""

from __future__ import annotations

import argparse
import json
import os
import urllib.error
import urllib.request
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

import cv2
import numpy as np

BACKEND_URL = "http://localhost:5000"
RUTA_ESCANEO = "/api/productos/escaneo-qr"
# Una misma etiqueta suele quedarse varios frames frente a la camara: solo se
# registra una vez, y vuelve a contar cuando lleva este numero de frames sin verse.
FRAMES_LIBERACION = 15
TIMEOUT_SEGUNDOS = 5.0


def leer_qr(
    frame: np.ndarray, detector: cv2.QRCodeDetector
) -> tuple[str | None, np.ndarray | None]:
    """Devuelve (texto, esquinas) del QR del frame, o (None, None) si no hay uno legible."""
    texto, puntos, _ = detector.detectAndDecode(frame)
    texto = texto.strip() if texto else ""
    if not texto:
        return None, None
    return texto, puntos


class ControlEscaneos:
    """Decide cuando una lectura cuenta como un escaneo nuevo.

    Un QR cuenta al aparecer. Mientras siga en el encuadre no vuelve a contar;
    tiene que desaparecer durante `frames_liberacion` frames seguidos (o ser
    reemplazado por otro QR) para que la misma etiqueta pueda sumar otra unidad.
    """

    def __init__(self, frames_liberacion: int = FRAMES_LIBERACION) -> None:
        self.frames_liberacion = frames_liberacion
        self._actual: str | None = None
        self._frames_sin_ver = 0

    def procesar(self, texto: str | None) -> str | None:
        """Recibe la lectura del frame; devuelve el SKU si es un escaneo nuevo."""
        if texto is None:
            if self._actual is not None:
                self._frames_sin_ver += 1
                if self._frames_sin_ver >= self.frames_liberacion:
                    self._actual = None
            return None

        self._frames_sin_ver = 0
        if texto == self._actual:
            return None
        self._actual = texto
        return texto


@dataclass
class ResultadoRegistro:
    ok: bool
    estado_http: int | None
    detalle: str
    stock: int | None = None


def registrar_en_backend(
    sku: str, backend_url: str = BACKEND_URL, timeout: float = TIMEOUT_SEGUNDOS
) -> ResultadoRegistro:
    """Envia el SKU al backend. Nunca lanza: los fallos se devuelven en el resultado."""
    url = backend_url.rstrip("/") + RUTA_ESCANEO
    cuerpo = json.dumps({"sku": sku}).encode("utf-8")
    peticion = urllib.request.Request(
        url, data=cuerpo, method="POST", headers={"Content-Type": "application/json"}
    )
    try:
        with urllib.request.urlopen(peticion, timeout=timeout) as respuesta:
            datos = json.loads(respuesta.read().decode("utf-8"))
            return ResultadoRegistro(
                ok=True,
                estado_http=respuesta.status,
                detalle=f"{datos.get('producto', '')} {datos.get('talla', '')}/{datos.get('color', '')}".strip(),
                stock=datos.get("stock"),
            )
    except urllib.error.HTTPError as error:
        if error.code == 404:
            detalle = "SKU no registrado en inventario"
        elif error.code == 400:
            detalle = "contenido del QR no valido"
        else:
            detalle = f"error del backend ({error.code})"
        return ResultadoRegistro(ok=False, estado_http=error.code, detalle=detalle)
    except (urllib.error.URLError, TimeoutError, OSError) as error:
        return ResultadoRegistro(ok=False, estado_http=None, detalle=f"backend no disponible: {error}")


def anotar_frame(frame: np.ndarray, puntos: np.ndarray | None, etiqueta: str) -> np.ndarray:
    anotado = frame.copy()
    if puntos is not None:
        cv2.polylines(anotado, [puntos.astype(np.int32).reshape(-1, 1, 2)], True, (0, 255, 0), 2)
    cv2.putText(anotado, etiqueta, (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
    return anotado


def procesar_video(
    fuente: int | str,
    backend_url: str = BACKEND_URL,
    simular: bool = False,
    frames_liberacion: int = FRAMES_LIBERACION,
    directorio_salida: Path | None = None,
    max_frames: int | None = None,
) -> list[dict]:
    """Lee la fuente de video y registra cada QR nuevo. Devuelve los escaneos realizados.

    Con `simular=True` no se llama al backend: sirve para probar la lectura sin API.
    """
    captura = cv2.VideoCapture(fuente)
    if not captura.isOpened():
        raise RuntimeError(f"No se pudo abrir la fuente de video: {fuente!r}")

    if directorio_salida is not None:
        directorio_salida.mkdir(parents=True, exist_ok=True)

    detector = cv2.QRCodeDetector()
    control = ControlEscaneos(frames_liberacion)
    escaneos: list[dict] = []
    numero_frame = 0

    try:
        while max_frames is None or numero_frame < max_frames:
            leido, frame = captura.read()
            if not leido:
                break
            numero_frame += 1

            texto, puntos = leer_qr(frame, detector)
            sku = control.procesar(texto)
            if sku is None:
                continue

            marca = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            if simular:
                resultado = ResultadoRegistro(ok=True, estado_http=None, detalle="simulado")
            else:
                resultado = registrar_en_backend(sku, backend_url)

            escaneos.append(
                {
                    "frame": numero_frame,
                    "timestamp": marca,
                    "sku": sku,
                    "ok": resultado.ok,
                    "estado_http": resultado.estado_http,
                    "stock": resultado.stock,
                }
            )
            estado = "REGISTRADO" if resultado.ok else "NO REGISTRADO"
            stock = f", stock={resultado.stock}" if resultado.stock is not None else ""
            print(f"[{marca}] frame {numero_frame}: QR {sku!r} -> {estado} ({resultado.detalle}{stock})")

            if directorio_salida is not None:
                ruta = directorio_salida / f"qr_{numero_frame:05d}.jpg"
                if not cv2.imwrite(str(ruta), anotar_frame(frame, puntos, f"{sku} {estado}")):
                    print(f"  aviso: no se pudo escribir la evidencia {ruta}")
    except KeyboardInterrupt:
        print("\nInterrumpido por el usuario.")
    finally:
        captura.release()

    print(f"Frames procesados: {numero_frame}")
    return escaneos


def _parsear_fuente(valor: str) -> int | str:
    return int(valor) if valor.isdigit() else valor


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--fuente",
        default="0",
        help="Indice de camara (0, 1, ...) o ruta a un archivo de video",
    )
    parser.add_argument(
        "--backend",
        default=os.environ.get("BACKEND_URL", BACKEND_URL),
        help="URL base del backend (por defecto $BACKEND_URL o http://localhost:5000)",
    )
    parser.add_argument(
        "--simular",
        action="store_true",
        help="Solo lee los QR, sin llamar al backend",
    )
    parser.add_argument("--frames-liberacion", type=int, default=FRAMES_LIBERACION)
    parser.add_argument(
        "--salida",
        type=Path,
        default=None,
        help="Carpeta donde guardar el frame anotado de cada escaneo",
    )
    parser.add_argument("--max-frames", type=int, default=None)
    args = parser.parse_args(argv)

    escaneos = procesar_video(
        fuente=_parsear_fuente(args.fuente),
        backend_url=args.backend,
        simular=args.simular,
        frames_liberacion=args.frames_liberacion,
        directorio_salida=args.salida,
        max_frames=args.max_frames,
    )
    registrados = sum(1 for e in escaneos if e["ok"])
    print(f"Escaneos: {len(escaneos)} (registrados: {registrados})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
