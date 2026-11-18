"""Lector de QR de prendas: cada QR escaneado suma una unidad al stock.

La etiqueta de cada prenda lleva un QR cuyo contenido es el SKU de la variante
(p. ej. "CAM-NEG-M"). Al leerlo, el lector llama al backend
(POST /api/productos/escaneo-qr) y este registra la entrada de +1 unidad como
movimiento tipo 'deteccion_ia'. La salida del stock la hace el modulo de Ventas
al confirmar la venta.

Cada escaneo lleva un id unico (UUID) que se repite en los reintentos: si el
backend ya lo registro (por ejemplo, proceso la peticion pero la respuesta se
perdio), responde duplicado=true y no suma otra unidad.

Reemplaza la prueba de concepto de deteccion por sustraccion de fondo (issue #7).
"""

from __future__ import annotations

import argparse
import http.client
import json
import os
import time
import urllib.error
import urllib.request
import uuid
from collections import Counter
from dataclasses import asdict, dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Callable, Iterable, Iterator

import cv2
import numpy as np

BACKEND_URL = "http://localhost:5000"
RUTA_ESCANEO = "/api/productos/escaneo-qr"
# Una misma etiqueta suele quedarse varios frames frente a la camara: solo se
# registra una vez, y vuelve a contar cuando lleva este numero de frames seguidos
# sin verse (15 frames = 0,5 s a 30 fps).
FRAMES_LIBERACION = 15
TIMEOUT_SEGUNDOS = 5.0
# Espera entre reintentos de un registro fallido: 1 s, 2 s, 4 s... hasta 30 s.
ESPERA_INICIAL_SEGUNDOS = 1.0
ESPERA_MAXIMA_SEGUNDOS = 30.0
# Al terminar el video, cuanto se espera como maximo a que se registren los pendientes.
ESPERA_FINAL_SEGUNDOS = 10.0


# --------------------------------------------------------------------------- lectura


def leer_qrs(frame: np.ndarray, detector: cv2.QRCodeDetector) -> list[tuple[str, np.ndarray | None]]:
    """Devuelve (texto, esquinas) de cada QR legible del frame.

    Lee varios QR a la vez, asi dos etiquetas iguales en el mismo frame cuentan
    como dos prendas. Si la lectura multiple no encuentra ninguno, intenta la
    lectura simple, que en algunos frames es mas tolerante.
    """
    lecturas: list[tuple[str, np.ndarray | None]] = []
    ok, textos, puntos, _ = detector.detectAndDecodeMulti(frame)
    if ok:
        for indice, texto in enumerate(textos):
            texto = (texto or "").strip()
            if texto:
                lecturas.append((texto, None if puntos is None else puntos[indice]))
    if not lecturas:
        texto, puntos_simple, _ = detector.detectAndDecode(frame)
        texto = (texto or "").strip()
        if texto:
            lecturas.append((texto, puntos_simple))
    return lecturas


@dataclass
class _EstadoSku:
    activos: int = 0
    frames_por_debajo: int = 0


class ControlEscaneos:
    """Decide cuando las lecturas de un frame cuentan como escaneos nuevos.

    Para cada SKU lleva cuantas etiquetas estan "activas" (ya contadas y aun en
    el encuadre). Si en un frame se ven mas etiquetas de ese SKU que las activas,
    cada etiqueta adicional cuenta como un escaneo nuevo. Si se ven menos durante
    `frames_liberacion` frames seguidos, las que faltan se dan por retiradas y una
    etiqueta igual que aparezca despues vuelve a contar.

    Limitacion fisica: dos etiquetas del mismo SKU tienen exactamente el mismo QR.
    Si la segunda prenda entra en el encuadre antes de que la primera lleve
    `frames_liberacion` frames fuera, son indistinguibles y se cuentan como una.
    Ver docs/guias/lector-qr-inventario.md.
    """

    def __init__(self, frames_liberacion: int = FRAMES_LIBERACION) -> None:
        if frames_liberacion < 1:
            raise ValueError("frames_liberacion debe ser al menos 1")
        self.frames_liberacion = frames_liberacion
        self._estado: dict[str, _EstadoSku] = {}

    def procesar(self, skus: Iterable[str]) -> list[str]:
        """Recibe los SKU leidos en el frame; devuelve los escaneos nuevos (con repeticion)."""
        visibles = Counter(skus)
        nuevos: list[str] = []

        for sku, cantidad in visibles.items():
            estado = self._estado.setdefault(sku, _EstadoSku())
            if cantidad > estado.activos:
                nuevos.extend([sku] * (cantidad - estado.activos))
                estado.activos = cantidad

        for sku in list(self._estado):
            estado = self._estado[sku]
            cantidad = visibles.get(sku, 0)
            if cantidad >= estado.activos:
                estado.frames_por_debajo = 0
                continue
            estado.frames_por_debajo += 1
            if estado.frames_por_debajo >= self.frames_liberacion:
                estado.activos = cantidad
                estado.frames_por_debajo = 0
                if cantidad == 0:
                    del self._estado[sku]

        return nuevos


# --------------------------------------------------------------------------- registro


@dataclass
class ResultadoRegistro:
    ok: bool
    estado_http: int | None
    detalle: str
    stock: int | None = None
    duplicado: bool = False
    # True cuando el fallo puede ser temporal (backend caido, timeout, 5xx):
    # se reintenta con el mismo id. Los 4xx no se reintentan.
    reintentable: bool = False


_DETALLE_HTTP = {
    400: "contenido del QR no valido",
    403: "origen no permitido por el backend (configurar EscaneoQr__RedesPermitidas)",
    404: "SKU no registrado en inventario",
    409: "el id del escaneo ya se uso con otro SKU",
}


def registrar_en_backend(
    sku: str,
    id_escaneo: str,
    backend_url: str = BACKEND_URL,
    timeout: float = TIMEOUT_SEGUNDOS,
) -> ResultadoRegistro:
    """Envia el escaneo al backend. Nunca lanza: los fallos se devuelven en el resultado."""
    url = backend_url.rstrip("/") + RUTA_ESCANEO
    cuerpo = json.dumps({"sku": sku, "idEscaneo": id_escaneo}).encode("utf-8")
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
                duplicado=bool(datos.get("duplicado", False)),
            )
    except urllib.error.HTTPError as error:
        reintentable = error.code >= 500 or error.code in (408, 429)
        detalle = _DETALLE_HTTP.get(error.code, f"error del backend ({error.code})")
        return ResultadoRegistro(
            ok=False, estado_http=error.code, detalle=detalle, reintentable=reintentable
        )
    except (urllib.error.URLError, http.client.HTTPException, TimeoutError, OSError, ValueError) as error:
        # Incluye la respuesta perdida (conexion cerrada o cuerpo ilegible): el
        # backend pudo haber registrado el escaneo, por eso se reintenta con el mismo id.
        return ResultadoRegistro(
            ok=False,
            estado_http=None,
            detalle=f"backend no disponible o respuesta perdida: {error}",
            reintentable=True,
        )


@dataclass
class Escaneo:
    sku: str
    frame: int
    timestamp: str
    id_escaneo: str = field(default_factory=lambda: str(uuid.uuid4()))
    estado: str = "pendiente"  # pendiente | registrado | duplicado | rechazado | simulado
    intentos: int = 0
    estado_http: int | None = None
    detalle: str = ""
    stock: int | None = None
    proximo_intento: float = 0.0


Enviar = Callable[[Escaneo], ResultadoRegistro]


class ColaRegistro:
    """Envia los escaneos al backend y reintenta los fallos temporales.

    Un escaneo conserva su id en todos los reintentos, asi que reintentar nunca
    suma dos veces: si el backend ya lo tenia, responde duplicado. Tras un fallo
    temporal se espera 1 s, 2 s, 4 s... (maximo `espera_maxima`) antes de volver a
    intentar ese escaneo, y en esa pasada no se intentan los demas.

    Con `ruta_pendientes` los escaneos sin confirmar se guardan en un JSON y se
    recuperan al arrancar de nuevo el lector.
    """

    def __init__(
        self,
        enviar: Enviar,
        reloj: Callable[[], float] = time.monotonic,
        espera_inicial: float = ESPERA_INICIAL_SEGUNDOS,
        espera_maxima: float = ESPERA_MAXIMA_SEGUNDOS,
        ruta_pendientes: Path | None = None,
    ) -> None:
        self._enviar = enviar
        self._reloj = reloj
        self._espera_inicial = espera_inicial
        self._espera_maxima = espera_maxima
        self._ruta = ruta_pendientes
        self._pendientes: list[Escaneo] = self._cargar()

    @property
    def pendientes(self) -> list[Escaneo]:
        return list(self._pendientes)

    def agregar(self, escaneo: Escaneo) -> None:
        self._pendientes.append(escaneo)
        self._guardar()

    def procesar(self) -> list[Escaneo]:
        """Intenta los escaneos pendientes cuyo turno llego. Devuelve los que terminaron."""
        terminados: list[Escaneo] = []
        for escaneo in list(self._pendientes):
            ahora = self._reloj()
            if escaneo.proximo_intento > ahora:
                continue

            resultado = self._enviar(escaneo)
            escaneo.intentos += 1
            escaneo.estado_http = resultado.estado_http
            escaneo.detalle = resultado.detalle
            escaneo.stock = resultado.stock

            if resultado.ok:
                escaneo.estado = "duplicado" if resultado.duplicado else "registrado"
            elif resultado.reintentable:
                espera = min(self._espera_maxima, self._espera_inicial * 2 ** (escaneo.intentos - 1))
                escaneo.proximo_intento = ahora + espera
                break  # el backend no responde: no insistir con el resto en esta pasada
            else:
                escaneo.estado = "rechazado"

            self._pendientes.remove(escaneo)
            terminados.append(escaneo)

        self._guardar()
        return terminados

    def vaciar(self, espera_max: float, dormir: Callable[[float], None] = time.sleep) -> list[Escaneo]:
        """Reintenta los pendientes hasta que no quede ninguno o pasen `espera_max` segundos."""
        limite = self._reloj() + espera_max
        terminados = self.procesar()
        while self._pendientes and self._reloj() < limite:
            siguiente = min(e.proximo_intento for e in self._pendientes)
            dormir(max(0.0, min(siguiente, limite) - self._reloj()))
            terminados.extend(self.procesar())
        return terminados

    def _cargar(self) -> list[Escaneo]:
        if self._ruta is None or not self._ruta.exists():
            return []
        datos = json.loads(self._ruta.read_text(encoding="utf-8"))
        return [Escaneo(**d) for d in datos]

    def _guardar(self) -> None:
        if self._ruta is None:
            return
        if not self._pendientes:
            self._ruta.unlink(missing_ok=True)
            return
        self._ruta.parent.mkdir(parents=True, exist_ok=True)
        temporal = self._ruta.with_suffix(self._ruta.suffix + ".tmp")
        # proximo_intento es del reloj monotono de este proceso: no se guarda.
        datos = [{k: v for k, v in asdict(e).items() if k != "proximo_intento"} for e in self._pendientes]
        temporal.write_text(json.dumps(datos, indent=2, ensure_ascii=False), encoding="utf-8")
        temporal.replace(self._ruta)


# --------------------------------------------------------------------------- video


def anotar_frame(frame: np.ndarray, lecturas: list[tuple[str, np.ndarray | None]], etiqueta: str) -> np.ndarray:
    anotado = frame.copy()
    for _, puntos in lecturas:
        if puntos is not None:
            cv2.polylines(anotado, [puntos.astype(np.int32).reshape(-1, 1, 2)], True, (0, 255, 0), 2)
    cv2.putText(anotado, etiqueta, (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
    return anotado


def _imprimir(escaneo: Escaneo) -> None:
    textos = {
        "registrado": "REGISTRADO",
        "duplicado": "YA REGISTRADO (reintento)",
        "rechazado": "NO REGISTRADO",
        "simulado": "SIMULADO",
    }
    stock = f", stock={escaneo.stock}" if escaneo.stock is not None else ""
    intentos = f", intentos={escaneo.intentos}" if escaneo.intentos > 1 else ""
    print(
        f"[{escaneo.timestamp}] frame {escaneo.frame}: QR {escaneo.sku!r} -> "
        f"{textos.get(escaneo.estado, escaneo.estado)} ({escaneo.detalle}{stock}{intentos})"
    )


def procesar_frames(
    frames: Iterable[np.ndarray],
    cola: ColaRegistro | None,
    frames_liberacion: int = FRAMES_LIBERACION,
    directorio_salida: Path | None = None,
    espera_final: float = ESPERA_FINAL_SEGUNDOS,
) -> list[Escaneo]:
    """Lee los QR de los frames y registra cada escaneo nuevo. Devuelve todos los escaneos.

    Con `cola=None` no se llama al backend (modo simulado).
    """
    if directorio_salida is not None:
        directorio_salida.mkdir(parents=True, exist_ok=True)

    detector = cv2.QRCodeDetector()
    control = ControlEscaneos(frames_liberacion)
    escaneos: list[Escaneo] = []
    numero_frame = 0

    try:
        for frame in frames:
            numero_frame += 1
            lecturas = leer_qrs(frame, detector)
            for sku in control.procesar(texto for texto, _ in lecturas):
                escaneo = Escaneo(
                    sku=sku,
                    frame=numero_frame,
                    timestamp=datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
                )
                escaneos.append(escaneo)
                if cola is None:
                    escaneo.estado, escaneo.detalle = "simulado", "sin backend"
                    _imprimir(escaneo)
                else:
                    cola.agregar(escaneo)
                if directorio_salida is not None:
                    ruta = directorio_salida / f"qr_{numero_frame:05d}_{escaneo.id_escaneo[:8]}.jpg"
                    if not cv2.imwrite(str(ruta), anotar_frame(frame, lecturas, f"{sku} {escaneo.id_escaneo[:8]}")):
                        print(f"  aviso: no se pudo escribir la evidencia {ruta}")
            if cola is not None:
                for terminado in cola.procesar():
                    _imprimir(terminado)
    except KeyboardInterrupt:
        print("\nInterrumpido por el usuario.")

    if cola is not None:
        for terminado in cola.vaciar(espera_final):
            _imprimir(terminado)
        for pendiente in cola.pendientes:
            print(
                f"  PENDIENTE: QR {pendiente.sku!r} (id {pendiente.id_escaneo}) sin confirmar "
                f"tras {pendiente.intentos} intentos: {pendiente.detalle}"
            )

    print(f"Frames procesados: {numero_frame}")
    return escaneos


def _frames_de(captura: cv2.VideoCapture, max_frames: int | None) -> Iterator[np.ndarray]:
    leidos = 0
    while max_frames is None or leidos < max_frames:
        ok, frame = captura.read()
        if not ok:
            return
        leidos += 1
        yield frame


def procesar_video(
    fuente: int | str,
    backend_url: str = BACKEND_URL,
    simular: bool = False,
    frames_liberacion: int = FRAMES_LIBERACION,
    directorio_salida: Path | None = None,
    max_frames: int | None = None,
    ruta_pendientes: Path | None = None,
    espera_final: float = ESPERA_FINAL_SEGUNDOS,
) -> list[Escaneo]:
    """Lee la fuente de video (camara o archivo) y registra cada QR nuevo."""
    captura = cv2.VideoCapture(fuente)
    if not captura.isOpened():
        raise RuntimeError(f"No se pudo abrir la fuente de video: {fuente!r}")

    cola = None
    if not simular:
        cola = ColaRegistro(
            enviar=lambda e: registrar_en_backend(e.sku, e.id_escaneo, backend_url),
            ruta_pendientes=ruta_pendientes,
        )
        if cola.pendientes:
            print(f"Recuperados {len(cola.pendientes)} escaneos pendientes de {ruta_pendientes}")

    try:
        return procesar_frames(
            _frames_de(captura, max_frames),
            cola,
            frames_liberacion=frames_liberacion,
            directorio_salida=directorio_salida,
            espera_final=espera_final,
        )
    finally:
        captura.release()


def _parsear_fuente(valor: str) -> int | str:
    return int(valor) if valor.isdigit() else valor


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
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
    parser.add_argument(
        "--frames-liberacion",
        type=int,
        default=FRAMES_LIBERACION,
        help="Frames seguidos sin ver una etiqueta para que una igual vuelva a contar (defecto 15)",
    )
    parser.add_argument(
        "--salida",
        type=Path,
        default=None,
        help="Carpeta donde guardar el frame anotado de cada escaneo",
    )
    parser.add_argument(
        "--pendientes",
        type=Path,
        default=None,
        help="JSON donde guardar los escaneos sin confirmar para reintentarlos al volver a arrancar",
    )
    parser.add_argument(
        "--espera-final",
        type=float,
        default=ESPERA_FINAL_SEGUNDOS,
        help="Segundos maximos para reintentar pendientes al terminar el video (defecto 10)",
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
        ruta_pendientes=args.pendientes,
        espera_final=args.espera_final,
    )
    conteo = Counter(e.estado for e in escaneos)
    resumen = ", ".join(f"{estado}: {n}" for estado, n in sorted(conteo.items()))
    print(f"Escaneos: {len(escaneos)} ({resumen or 'ninguno'})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
