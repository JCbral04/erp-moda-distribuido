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
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request
import uuid
import weakref
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
# Persistencia por defecto: no depende del directorio de trabajo ni de --pendientes.
RUTA_PENDIENTES_PREDETERMINADA = Path.home() / ".erp_moda" / "qr_pendientes.json"
# El trabajador HTTP se despierta con esta frecuencia; nunca bloquea la captura.
INTERVALO_TRABAJADOR_SEGUNDOS = 0.05


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


def _respuesta_valida(datos: object, id_escaneo: str) -> bool:
    """Comprueba que un 200 tenga la forma de EscaneoQrResponseDto y sea de este escaneo."""
    if not isinstance(datos, dict):
        return False
    stock = datos.get("stock")
    return (
        str(datos.get("idEscaneo", "")).lower() == id_escaneo.lower()
        and isinstance(stock, int)
        and not isinstance(stock, bool)
        and isinstance(datos.get("duplicado"), bool)
    )


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
            if not _respuesta_valida(datos, id_escaneo):
                # 200 que no corresponde al contrato (proxy, version distinta...): no se
                # puede saber si se registro, asi que se reintenta con el mismo id.
                return ResultadoRegistro(
                    ok=False,
                    estado_http=respuesta.status,
                    detalle="respuesta del backend con formato inesperado",
                    reintentable=True,
                )
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


# Bloqueo de archivo del sistema operativo: el SO lo suelta si el proceso muere.
if os.name == "nt":
    import msvcrt

    def _bloquear_archivo(archivo) -> None:
        archivo.seek(0)
        msvcrt.locking(archivo.fileno(), msvcrt.LK_NBLCK, 1)

    def _desbloquear_archivo(archivo) -> None:
        archivo.seek(0)
        msvcrt.locking(archivo.fileno(), msvcrt.LK_UNLCK, 1)

else:
    import fcntl

    def _bloquear_archivo(archivo) -> None:
        fcntl.flock(archivo.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)

    def _desbloquear_archivo(archivo) -> None:
        fcntl.flock(archivo.fileno(), fcntl.LOCK_UN)


class _BloqueoAlmacenamiento:
    """Bloqueo exclusivo del JSON de pendientes entre instancias del lector.

    Usa el bloqueo de archivos del sistema operativo (msvcrt en Windows, fcntl en
    Linux/macOS) sobre `<ruta>.lock`. El SO lo suelta si el proceso termina, incluso
    de forma abrupta, asi que no quedan bloqueos huerfanos y no hace falta
    comprobar PIDs (en Windows `os.kill(pid, 0)` no comprueba nada: envia
    CTRL_C_EVENT y no falla con un PID que ya termino).
    El archivo `.lock` se deja en disco: lo que importa es el bloqueo, no su existencia.
    """

    def __init__(self, ruta: Path) -> None:
        self.ruta = ruta.with_name(ruta.name + ".lock")
        self._archivo = None
        self._finalizador: weakref.finalize | None = None

    def adquirir(self) -> None:
        self.ruta.parent.mkdir(parents=True, exist_ok=True)
        archivo = open(self.ruta, "a+b")
        try:
            _bloquear_archivo(archivo)
        except OSError as exc:
            archivo.close()
            raise RuntimeError(
                f"Otro lector está usando el almacenamiento de pendientes ({self.ruta}). "
                "Cierra esa instancia antes de iniciar otra."
            ) from exc
        self._archivo = archivo
        # Red de seguridad si nadie llama a liberar(): al recolectar el objeto (ya
        # no hay trabajador que lo use) se cierra el archivo y el SO suelta el bloqueo.
        self._finalizador = weakref.finalize(self, archivo.close)

    def liberar(self) -> None:
        if self._archivo is None:
            return
        archivo, self._archivo = self._archivo, None
        self._finalizador.detach()
        try:
            _desbloquear_archivo(archivo)
        except OSError as exc:
            # Cerrar el archivo libera el bloqueo de todas formas; se avisa para
            # no ocultar un problema del sistema de archivos.
            print(f"  aviso: no se pudo desbloquear {self.ruta} ({exc}); se libera al cerrarlo.", file=sys.stderr)
        finally:
            archivo.close()


class ColaRegistro:
    """Envia los escaneos al backend y reintenta los fallos temporales.

    Un escaneo conserva su id en todos los reintentos, asi que reintentar nunca
    suma dos veces: si el backend ya lo tenia, responde duplicado. Tras un fallo
    temporal se espera 1 s, 2 s, 4 s... (maximo `espera_maxima`) antes de volver a
    intentar ese escaneo, y en esa pasada no se intentan los demas.

    Con `ruta_pendientes` los escaneos sin confirmar se guardan en un JSON y se
    recuperan al arrancar de nuevo el lector. Las escrituras son atomicas y un
    bloqueo del sistema operativo impide dos instancias sobre el mismo archivo.

    La peticion HTTP se hace FUERA del lock para que agregar escaneos nunca espere
    al backend. `detener()` impide iniciar peticiones nuevas y `cerrar()` espera a
    que termine la que este en curso antes de soltar el almacenamiento: nadie
    escribe el JSON sin tener el bloqueo.
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
        self._ruta = Path(ruta_pendientes) if ruta_pendientes is not None else None
        self._bloqueo = threading.RLock()
        self._condicion = threading.Condition(self._bloqueo)
        self._en_curso = 0
        self._detenida = False
        self._cerrada = False
        self._archivo_bloqueo: _BloqueoAlmacenamiento | None = None
        if self._ruta is not None:
            self._archivo_bloqueo = _BloqueoAlmacenamiento(self._ruta)
            try:
                self._archivo_bloqueo.adquirir()
            except OSError as exc:
                raise RuntimeError(
                    f"No se puede acceder al almacenamiento persistente de pendientes "
                    f"({self._ruta}): {exc}. Revisa permisos o configura --pendientes."
                ) from exc
        try:
            self._pendientes: list[Escaneo] = self._cargar()
        except RuntimeError:
            self._soltar_almacenamiento()
            raise

    @property
    def pendientes(self) -> list[Escaneo]:
        with self._bloqueo:
            return list(self._pendientes)

    @property
    def en_curso(self) -> int:
        with self._bloqueo:
            return self._en_curso

    @property
    def almacenamiento_bloqueado(self) -> bool:
        return self._archivo_bloqueo is not None

    def agregar(self, escaneo: Escaneo) -> None:
        with self._bloqueo:
            if self._cerrada:
                raise RuntimeError("La cola de registro está cerrada: no se pueden agregar escaneos.")
            self._pendientes.append(escaneo)
            # Se persiste ANTES de intentar enviar: si el proceso muere, el id
            # original sigue disponible para el reinicio.
            self._guardar()

    def _espera_tras(self, intentos: int) -> float:
        # El exponente se acota antes de calcular: 2 ** 1024 ya no cabe en un float.
        exponente = min(max(intentos - 1, 0), 30)
        return min(self._espera_maxima, self._espera_inicial * (2.0 ** exponente))

    def procesar(self, limite: float | None = None) -> list[Escaneo]:
        """Intenta los escaneos pendientes cuyo turno llego. Devuelve los que terminaron.

        No inicia peticiones nuevas si la cola esta detenida o si `limite` (segun
        el reloj de la cola) ya paso.
        """
        terminados: list[Escaneo] = []
        while True:
            with self._condicion:
                ahora = self._reloj()
                if self._detenida or (limite is not None and ahora >= limite):
                    break
                escaneo = next((e for e in self._pendientes if e.proximo_intento <= ahora), None)
                if escaneo is None:
                    break
                self._en_curso += 1

            try:
                resultado = self._enviar(escaneo)  # fuera del lock: la captura no espera
                with self._condicion:
                    escaneo.intentos += 1
                    escaneo.estado_http = resultado.estado_http
                    escaneo.detalle = resultado.detalle
                    escaneo.stock = resultado.stock

                    if resultado.ok:
                        escaneo.estado = "duplicado" if resultado.duplicado else "registrado"
                    elif resultado.reintentable:
                        escaneo.proximo_intento = self._reloj() + self._espera_tras(escaneo.intentos)
                        self._guardar()
                        break  # backend lento o caido: no insistir con el resto en esta pasada
                    else:
                        escaneo.estado = "rechazado"

                    self._pendientes.remove(escaneo)
                    self._guardar()
                    terminados.append(escaneo)
            finally:
                with self._condicion:
                    self._en_curso -= 1
                    self._condicion.notify_all()
        return terminados

    def vaciar(self, espera_max: float, dormir: Callable[[float], None] = time.sleep) -> list[Escaneo]:
        """Reintenta los pendientes hasta que no quede ninguno o pasen `espera_max` segundos.

        Despues del plazo no se inicia ninguna peticion nueva.
        """
        limite = self._reloj() + espera_max
        terminados: list[Escaneo] = []
        while True:
            terminados.extend(self.procesar(limite=limite))
            with self._bloqueo:
                pendientes = list(self._pendientes)
                detenida = self._detenida
            if not pendientes or detenida or self._reloj() >= limite:
                return terminados
            siguiente = min(e.proximo_intento for e in pendientes)
            dormir(max(0.0, min(siguiente, limite) - self._reloj()))

    def detener(self) -> None:
        """Impide iniciar peticiones nuevas. La que este en curso termina normalmente."""
        with self._condicion:
            self._detenida = True
            self._condicion.notify_all()

    def esperar_en_curso(self, timeout: float | None = None) -> bool:
        """Espera a que no haya peticiones en curso. Devuelve False si vence `timeout`."""
        with self._condicion:
            return self._condicion.wait_for(lambda: self._en_curso == 0, timeout=timeout)

    def cerrar(self) -> None:
        """Detiene la cola, espera la peticion en curso y solo entonces libera el almacenamiento.

        Los pendientes quedan en disco con su id original. La espera esta acotada
        por el timeout HTTP de la peticion en vuelo.
        """
        with self._condicion:
            self._detenida = True
            self._cerrada = True
            self._condicion.wait_for(lambda: self._en_curso == 0)
            self._soltar_almacenamiento()

    def _soltar_almacenamiento(self) -> None:
        if self._archivo_bloqueo is not None:
            self._archivo_bloqueo.liberar()
            self._archivo_bloqueo = None

    def _cargar(self) -> list[Escaneo]:
        if self._ruta is None or not self._ruta.exists():
            return []
        try:
            datos = json.loads(self._ruta.read_text(encoding="utf-8"))
            return [Escaneo(**dato) for dato in datos]
        except (OSError, ValueError, TypeError, KeyError) as exc:
            raise RuntimeError(
                f"No se pudo leer el almacenamiento de pendientes ({self._ruta}): {exc}. "
                "No se descartan movimientos: revisa el archivo antes de continuar."
            ) from exc

    def _guardar(self) -> None:
        if self._ruta is None:
            return
        if self._archivo_bloqueo is None:
            raise RuntimeError("Escritura de pendientes sin el bloqueo del almacenamiento.")
        if not self._pendientes:
            self._ruta.unlink(missing_ok=True)
            return
        self._ruta.parent.mkdir(parents=True, exist_ok=True)
        # proximo_intento es del reloj monotono de este proceso: no se guarda.
        datos = [{k: v for k, v in asdict(e).items() if k != "proximo_intento"} for e in self._pendientes]
        temporal = None
        try:
            with tempfile.NamedTemporaryFile(
                mode="w", encoding="utf-8", dir=self._ruta.parent,
                prefix=self._ruta.name + ".", suffix=".tmp", delete=False
            ) as archivo:
                temporal = Path(archivo.name)
                json.dump(datos, archivo, indent=2, ensure_ascii=False)
                archivo.flush()
                os.fsync(archivo.fileno())
            os.replace(temporal, self._ruta)
        finally:
            if temporal is not None and temporal.exists():
                temporal.unlink()


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


class TrabajadorDetenidoError(RuntimeError):
    """El trabajador HTTP termino por un error inesperado: no se sigue capturando."""


class TrabajadorRegistro:
    """Un único hilo que hace las peticiones HTTP fuera del bucle de captura.

    El lector solo encola escaneos; el trabajador los envía con los reintentos
    exponenciales de ColaRegistro. No se crea más de un trabajador por cola.
    Si el hilo falla por una excepción inesperada, la guarda en `error`, detiene
    la cola y lo avisa por stderr; `procesar_frames` deja de capturar al verlo.
    """

    def __init__(
        self,
        cola: ColaRegistro,
        notificar: Callable[[Escaneo], None] = _imprimir,
        intervalo: float = INTERVALO_TRABAJADOR_SEGUNDOS,
    ) -> None:
        self.cola = cola
        self.notificar = notificar
        self.intervalo = intervalo
        self.error: BaseException | None = None
        self._alistado = threading.Event()
        self._hilo: threading.Thread | None = None
        self._bloqueo = threading.Lock()
        self._limite_cierre: float | None = None

    def iniciar(self) -> None:
        with self._bloqueo:
            existente = getattr(self.cola, "_trabajador", None)
            if existente is not None and existente is not self:
                return  # nunca dos trabajadores para la misma cola
            if self._hilo is not None:
                return
            self._alistado.clear()
            self.cola._trabajador = self
            self._hilo = threading.Thread(target=self._bucle, name="registro-qr", daemon=True)
            self._hilo.start()

    @property
    def activo(self) -> bool:
        return self._hilo is not None and self._hilo.is_alive()

    def _bucle(self) -> None:
        try:
            while not self._alistado.is_set():
                for terminado in self.cola.procesar():
                    self.notificar(terminado)
                self._alistado.wait(self.intervalo)

            # Cierre ordenado: se usa el tiempo restante para intentar los pendientes,
            # sin iniciar peticiones despues del plazo. Los que no se confirmen
            # quedan en disco con su id original.
            limite = self._limite_cierre
            if limite is not None:
                restante = limite - time.monotonic()
                if restante > 0:
                    for terminado in self.cola.vaciar(restante):
                        self.notificar(terminado)
        except Exception as exc:  # supervision: nunca terminar en silencio
            self.error = exc
            self.cola.detener()
            print(
                f"ERROR: el trabajador HTTP se detuvo por un error inesperado ({exc!r}). "
                "Los escaneos sin confirmar quedan guardados con su id original.",
                file=sys.stderr,
            )

    def cerrar(self, espera_max: float = ESPERA_FINAL_SEGUNDOS) -> bool:
        """Pide el cierre y espera al trabajador. Devuelve True si terminó dentro del plazo.

        Si el plazo vence con una petición en curso, la cola deja de iniciar
        peticiones y se espera a que termine la que está en vuelo (como mucho el
        timeout HTTP). Así el almacenamiento sigue bloqueado mientras el hilo
        pueda escribir en él.
        """
        with self._bloqueo:
            hilo = self._hilo
            self._limite_cierre = time.monotonic() + max(0.0, espera_max)
            self._alistado.set()
        if hilo is None:
            return True
        hilo.join(timeout=max(0.0, espera_max))
        if not hilo.is_alive():
            return True
        self.cola.detener()
        print(
            "  aviso: venció el plazo de cierre con una petición en curso; se espera a que "
            "termine sin iniciar otras. Los escaneos sin confirmar quedan guardados con su id original.",
            file=sys.stderr,
        )
        hilo.join()
        return False


def procesar_frames(
    frames: Iterable[np.ndarray],
    cola: ColaRegistro | None,
    frames_liberacion: int = FRAMES_LIBERACION,
    directorio_salida: Path | None = None,
    espera_final: float = ESPERA_FINAL_SEGUNDOS,
) -> list[Escaneo]:
    """Captura y detecta QR; el HTTP lo hace un trabajador aparte.

    El bucle principal solo decodifica y encola. Con `cola=None` no se llama al
    backend (modo simulado). Si el trabajador HTTP falla, se deja de capturar y se
    lanza TrabajadorDetenidoError tras guardar los pendientes.
    """
    if directorio_salida is not None:
        directorio_salida.mkdir(parents=True, exist_ok=True)

    detector = cv2.QRCodeDetector()
    control = ControlEscaneos(frames_liberacion)
    escaneos: list[Escaneo] = []
    numero_frame = 0
    trabajador = TrabajadorRegistro(cola) if cola is not None else None
    if trabajador is not None:
        trabajador.iniciar()

    try:
        for frame in frames:
            if trabajador is not None and trabajador.error is not None:
                raise TrabajadorDetenidoError(
                    f"El trabajador HTTP se detuvo ({trabajador.error!r}); se deja de capturar "
                    "para no acumular escaneos sin enviar."
                ) from trabajador.error
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
                    # Solo se encola (y se persiste): nunca se espera al backend.
                    cola.agregar(escaneo)
                if directorio_salida is not None:
                    ruta = directorio_salida / f"qr_{numero_frame:05d}_{escaneo.id_escaneo[:8]}.jpg"
                    if not cv2.imwrite(str(ruta), anotar_frame(frame, lecturas, f"{sku} {escaneo.id_escaneo[:8]}")):
                        print(f"  aviso: no se pudo escribir la evidencia {ruta}")
    except KeyboardInterrupt:
        print("\nInterrumpido por el usuario.")
    finally:
        if trabajador is not None and cola is not None:
            # Cierre ordenado: el trabajador termina (o termina su petición en curso)
            # ANTES de soltar el almacenamiento; los pendientes quedan con su UUID.
            trabajador.cerrar(espera_final)
            for pendiente in cola.pendientes:
                print(
                    f"  PENDIENTE: QR {pendiente.sku!r} (id {pendiente.id_escaneo}) sin confirmar "
                    f"tras {pendiente.intentos} intentos: {pendiente.detalle}"
                )
            cola.cerrar()

    if trabajador is not None and trabajador.error is not None:
        raise TrabajadorDetenidoError(
            f"El trabajador HTTP se detuvo ({trabajador.error!r}). Los pendientes quedan en disco."
        ) from trabajador.error

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
        # Persistencia real por defecto: sin --pendientes no se puede perder stock.
        ruta = ruta_pendientes if ruta_pendientes is not None else RUTA_PENDIENTES_PREDETERMINADA
        cola = ColaRegistro(
            enviar=lambda e: registrar_en_backend(e.sku, e.id_escaneo, backend_url),
            ruta_pendientes=ruta,
        )
        if cola.pendientes:
            print(f"Recuperados {len(cola.pendientes)} escaneos pendientes de {ruta}")

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
        if cola is not None:
            cola.cerrar()


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
        default=RUTA_PENDIENTES_PREDETERMINADA,
        help=(
            "JSON donde guardar los escaneos sin confirmar para reintentarlos al volver a arrancar "
            f"(por defecto {RUTA_PENDIENTES_PREDETERMINADA})"
        ),
    )
    parser.add_argument(
        "--espera-final",
        type=float,
        default=ESPERA_FINAL_SEGUNDOS,
        help="Segundos maximos para reintentar pendientes al terminar el video (defecto 10)",
    )
    parser.add_argument("--max-frames", type=int, default=None)
    args = parser.parse_args(argv)

    try:
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
    except RuntimeError as exc:
        # Incluye almacenamiento persistente inaccesible o corrupto: nunca se
        # continúa en un modo que pueda perder movimientos de inventario.
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    conteo = Counter(e.estado for e in escaneos)
    resumen = ", ".join(f"{estado}: {n}" for estado, n in sorted(conteo.items()))
    print(f"Escaneos: {len(escaneos)} ({resumen or 'ninguno'})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
