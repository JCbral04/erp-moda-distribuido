"""Pruebas del lector de QR (ai/deteccion.py).

Ejecutar desde la raiz del repo:
    python -m unittest discover -s ai/tests -v

Solo usan la libreria estandar, OpenCV y NumPy (ai/requirements.txt). El backend
se reemplaza por un servidor HTTP local que imita el contrato de
POST /api/productos/escaneo-qr, incluida la idempotencia por idEscaneo.
"""

from __future__ import annotations

import json
import socket
import sys
import tempfile
import threading
import time
import unittest
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from unittest.mock import patch

import cv2
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from deteccion import (  # noqa: E402
    ColaRegistro,
    ControlEscaneos,
    Escaneo,
    ResultadoRegistro,
    RUTA_PENDIENTES_PREDETERMINADA,
    TrabajadorDetenidoError,
    TrabajadorRegistro,
    leer_qrs,
    main,
    procesar_frames,
    procesar_video,
    registrar_en_backend,
)
from generar_video_prueba import generar_video_sintetico  # noqa: E402

A = "CAM-NEG-M"
B = "CAM-BLA-L"
C = "CAM-AZUL-S"
LIBERACION = 15


def contar(secuencia: list[list[str]], frames_liberacion: int = LIBERACION) -> list[str]:
    """Pasa una secuencia de frames (lista de SKU visibles por frame) por ControlEscaneos."""
    control = ControlEscaneos(frames_liberacion)
    nuevos: list[str] = []
    for visibles in secuencia:
        nuevos.extend(control.procesar(visibles))
    return nuevos


def presente(sku: str, frames: int, cantidad: int = 1) -> list[list[str]]:
    return [[sku] * cantidad for _ in range(frames)]


def vacio(frames: int) -> list[list[str]]:
    return [[] for _ in range(frames)]


class ControlEscaneosTests(unittest.TestCase):
    def test_una_etiqueta_durante_muchos_frames_cuenta_una(self):
        self.assertEqual(contar(presente(A, 60)), [A])

    def test_parpadeo_de_la_lectura_no_duplica(self):
        # La etiqueta deja de leerse 5 frames (desenfoque) pero sigue en el encuadre.
        secuencia = presente(A, 10) + vacio(5) + presente(A, 10)
        self.assertEqual(contar(secuencia), [A])

    def test_parpadeo_justo_por_debajo_del_umbral_no_duplica(self):
        secuencia = presente(A, 10) + vacio(LIBERACION - 1) + presente(A, 10)
        self.assertEqual(contar(secuencia), [A])

    # --- dos prendas con el mismo SKU, una detras de otra ---

    def test_mismo_sku_con_hueco_igual_al_umbral_cuenta_dos(self):
        secuencia = presente(A, 10) + vacio(LIBERACION) + presente(A, 10)
        self.assertEqual(contar(secuencia), [A, A])

    def test_mismo_sku_con_hueco_mayor_al_umbral_cuenta_dos(self):
        secuencia = presente(A, 10) + vacio(40) + presente(A, 10)
        self.assertEqual(contar(secuencia), [A, A])

    def test_limitacion_mismo_sku_sin_hueco_cuenta_una(self):
        # La segunda etiqueta reemplaza a la primera sin ningun frame vacio: el QR
        # es identico, no hay forma de distinguirlas. Limitacion documentada.
        secuencia = presente(A, 10) + presente(A, 10)
        self.assertEqual(contar(secuencia), [A])

    def test_limitacion_mismo_sku_con_hueco_menor_al_umbral_cuenta_una(self):
        secuencia = presente(A, 10) + vacio(5) + presente(A, 10)
        self.assertEqual(contar(secuencia), [A])

    def test_umbral_configurable_permite_huecos_mas_cortos(self):
        secuencia = presente(A, 10) + vacio(5) + presente(A, 10)
        self.assertEqual(contar(secuencia, frames_liberacion=5), [A, A])

    # --- dos prendas con el mismo SKU a la vez ---

    def test_dos_etiquetas_iguales_en_el_mismo_frame_cuentan_dos(self):
        self.assertEqual(contar(presente(A, 10, cantidad=2)), [A, A])

    def test_segunda_etiqueta_igual_entra_mientras_la_primera_sigue_cuenta_dos(self):
        secuencia = presente(A, 10) + presente(A, 10, cantidad=2) + presente(A, 10)
        self.assertEqual(contar(secuencia), [A, A])

    def test_una_de_dos_sale_y_entra_otra_tras_el_umbral_cuenta_tres(self):
        secuencia = (
            presente(A, 10, cantidad=2)
            + presente(A, LIBERACION)  # una de las dos se retira
            + presente(A, 10, cantidad=2)  # entra una tercera prenda igual
        )
        self.assertEqual(contar(secuencia), [A, A, A])

    # --- SKU distintos ---

    def test_skus_distintos_alternados_cuentan_cada_uno(self):
        secuencia = presente(A, 10) + presente(B, 20) + presente(A, 10)
        self.assertEqual(contar(secuencia), [A, B, A])

    def test_skus_distintos_en_el_mismo_frame_cuentan_ambos(self):
        self.assertEqual(sorted(contar([[A, B]] * 5)), sorted([A, B]))

    def test_frames_liberacion_invalido(self):
        with self.assertRaises(ValueError):
            ControlEscaneos(0)


# --------------------------------------------------------------------------- frames reales


def imagen_qr(texto: str, lado: int = 150) -> np.ndarray:
    qr = cv2.QRCodeEncoder.create().encode(texto)
    qr = cv2.resize(qr, (lado, lado), interpolation=cv2.INTER_NEAREST)
    return cv2.cvtColor(qr, cv2.COLOR_GRAY2BGR)


def frame_con(*etiquetas: str) -> np.ndarray:
    """Frame 480x360 con las etiquetas una al lado de otra (maximo 2)."""
    frame = np.full((360, 480, 3), 40, np.uint8)
    posiciones = [(165,)] if len(etiquetas) == 1 else [(40,), (290,)]
    for texto, (x,) in zip(etiquetas, posiciones):
        frame[100:250, x : x + 150] = imagen_qr(texto)
    return frame


def frames_secuencia(*tramos: tuple[tuple[str, ...], int]) -> list[np.ndarray]:
    frames: list[np.ndarray] = []
    for etiquetas, cantidad in tramos:
        frames.extend([frame_con(*etiquetas) if etiquetas else frame_con()] * cantidad)
    return frames


class LecturaQrTests(unittest.TestCase):
    def setUp(self):
        self.detector = cv2.QRCodeDetector()

    def test_lee_un_qr(self):
        self.assertEqual([t for t, _ in leer_qrs(frame_con(A), self.detector)], [A])

    def test_lee_dos_qr_iguales_en_el_mismo_frame(self):
        self.assertEqual([t for t, _ in leer_qrs(frame_con(A, A), self.detector)], [A, A])

    def test_frame_sin_qr(self):
        self.assertEqual(leer_qrs(frame_con(), self.detector), [])


class ProcesarFramesTests(unittest.TestCase):
    """Lectura de frames reales (QR generados con OpenCV) en modo simulado."""

    def contar(self, frames: list[np.ndarray]) -> list[str]:
        return [e.sku for e in procesar_frames(frames, cola=None, frames_liberacion=LIBERACION)]

    def test_mismo_sku_sin_hueco_cuenta_uno(self):
        self.assertEqual(self.contar(frames_secuencia(((A,), 10), ((A,), 10))), [A])

    def test_mismo_sku_hueco_de_5_frames_cuenta_uno(self):
        self.assertEqual(self.contar(frames_secuencia(((A,), 10), ((), 5), ((A,), 10))), [A])

    def test_mismo_sku_hueco_igual_al_umbral_cuenta_dos(self):
        self.assertEqual(self.contar(frames_secuencia(((A,), 10), ((), LIBERACION), ((A,), 10))), [A, A])

    def test_dos_etiquetas_iguales_a_la_vez_cuentan_dos(self):
        self.assertEqual(self.contar(frames_secuencia(((A, A), 10))), [A, A])

    def test_segunda_etiqueta_entra_con_la_primera_en_pantalla(self):
        frames = frames_secuencia(((A,), 10), ((A, A), 10), ((A,), 10))
        self.assertEqual(self.contar(frames), [A, A])


class VideoTests(unittest.TestCase):
    """Video generado con generar_video_prueba.py (MJPG), leido con VideoCapture."""

    def contar_video(self, skus: list[str], frames_vacio: int) -> list[str]:
        with tempfile.TemporaryDirectory() as tmp:
            ruta = generar_video_sintetico(
                Path(tmp) / "video.avi", skus, frames_qr=10, frames_vacio=frames_vacio
            )
            return [e.sku for e in procesar_video(str(ruta), simular=True, frames_liberacion=LIBERACION)]

    def test_hueco_de_30_frames(self):
        self.assertEqual(self.contar_video([A, B, A], frames_vacio=30), [A, B, A])

    def test_hueco_igual_al_umbral(self):
        self.assertEqual(self.contar_video([A, A], frames_vacio=LIBERACION), [A, A])

    def test_limitacion_hueco_menor_al_umbral(self):
        self.assertEqual(self.contar_video([A, A], frames_vacio=5), [A])


# --------------------------------------------------------------------------- backend


class BackendFalso:
    """Servidor local con el contrato del endpoint, idempotente por idEscaneo.

    `perder_respuestas` = cuantas peticiones nuevas se procesan (suman stock) pero
    cierran la conexion sin responder. `fallar_con` = codigos que se devuelven en
    las primeras peticiones sin procesarlas.
    """

    def __init__(
        self,
        stock: dict[str, int],
        perder_respuestas: int = 0,
        fallar_con: list[int] | None = None,
        latencia: float = 0.0,
        registrar_llegada: list[float] | None = None,
        respuesta_200: object | None = None,
    ):
        self.stock = dict(stock)
        self.registrados: dict[str, str] = {}
        self.peticiones: list[dict] = []
        self.perder_respuestas = perder_respuestas
        self.fallar_con = list(fallar_con or [])
        self.latencia = latencia
        self.registrar_llegada = registrar_llegada
        self.respuesta_200 = respuesta_200  # cuerpo alternativo para un 200 (contrato roto)
        backend = self

        class Manejador(BaseHTTPRequestHandler):
            def do_POST(self):
                cuerpo = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
                if backend.registrar_llegada is not None:
                    backend.registrar_llegada.append(time.monotonic())
                if backend.latencia:
                    time.sleep(backend.latencia)
                backend.peticiones.append(cuerpo)
                if backend.fallar_con:
                    self._responder(backend.fallar_con.pop(0), {"error": "fallo"})
                    return
                sku, id_escaneo = cuerpo["sku"], cuerpo["idEscaneo"]
                if id_escaneo in backend.registrados:
                    if backend.registrados[id_escaneo] != sku:
                        self._responder(409, "conflicto")
                        return
                    self._responder(200, {"idEscaneo": id_escaneo, "sku": sku, "stock": backend.stock[sku], "duplicado": True})
                    return
                if sku not in backend.stock:
                    self._responder(404, "No existe")
                    return
                backend.stock[sku] += 1
                backend.registrados[id_escaneo] = sku
                if backend.perder_respuestas > 0:
                    backend.perder_respuestas -= 1
                    self.close_connection = True
                    self.connection.shutdown(socket.SHUT_RDWR)
                    return
                self._responder(200, backend.respuesta_200 if backend.respuesta_200 is not None else {"idEscaneo": id_escaneo, "sku": sku, "stock": backend.stock[sku], "duplicado": False})

            def _responder(self, codigo, datos):
                cuerpo = json.dumps(datos).encode()
                self.send_response(codigo)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(cuerpo)))
                self.end_headers()
                self.wfile.write(cuerpo)

            def log_message(self, *args):
                pass

        self._servidor = HTTPServer(("127.0.0.1", 0), Manejador)
        self.url = f"http://127.0.0.1:{self._servidor.server_port}"
        self._hilo = threading.Thread(target=self._servidor.serve_forever, daemon=True)

    def __enter__(self):
        self._hilo.start()
        return self

    def __exit__(self, *args):
        self._servidor.shutdown()
        self._servidor.server_close()


def puerto_cerrado() -> str:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return f"http://127.0.0.1:{s.getsockname()[1]}"


class RegistrarEnBackendTests(unittest.TestCase):
    def test_registro_correcto_envia_sku_e_id(self):
        with BackendFalso({A: 3}) as backend:
            resultado = registrar_en_backend(A, "id-1", backend.url)
        self.assertTrue(resultado.ok)
        self.assertFalse(resultado.duplicado)
        self.assertEqual(resultado.stock, 4)
        self.assertEqual(backend.peticiones, [{"sku": A, "idEscaneo": "id-1"}])

    def test_mismo_id_devuelve_duplicado_sin_sumar(self):
        with BackendFalso({A: 3}) as backend:
            registrar_en_backend(A, "id-1", backend.url)
            resultado = registrar_en_backend(A, "id-1", backend.url)
        self.assertTrue(resultado.ok)
        self.assertTrue(resultado.duplicado)
        self.assertEqual(backend.stock[A], 4)

    def test_404_no_es_reintentable(self):
        with BackendFalso({A: 3}) as backend:
            resultado = registrar_en_backend("NO-EXISTE", "id-1", backend.url)
        self.assertFalse(resultado.ok)
        self.assertFalse(resultado.reintentable)
        self.assertEqual(resultado.estado_http, 404)

    def test_403_y_409_no_son_reintentables(self):
        for codigo in (400, 403, 409):
            with self.subTest(codigo=codigo), BackendFalso({A: 3}, fallar_con=[codigo]) as backend:
                resultado = registrar_en_backend(A, "id-1", backend.url)
                self.assertFalse(resultado.reintentable)
                self.assertEqual(resultado.estado_http, codigo)

    def test_500_es_reintentable(self):
        with BackendFalso({A: 3}, fallar_con=[500]) as backend:
            resultado = registrar_en_backend(A, "id-1", backend.url)
        self.assertFalse(resultado.ok)
        self.assertTrue(resultado.reintentable)

    def test_backend_caido_es_reintentable(self):
        resultado = registrar_en_backend(A, "id-1", puerto_cerrado(), timeout=2)
        self.assertFalse(resultado.ok)
        self.assertTrue(resultado.reintentable)
        self.assertIsNone(resultado.estado_http)

    def test_respuesta_perdida_es_reintentable(self):
        with BackendFalso({A: 3}, perder_respuestas=1) as backend:
            resultado = registrar_en_backend(A, "id-1", backend.url, timeout=2)
        self.assertFalse(resultado.ok)
        self.assertTrue(resultado.reintentable)
        self.assertEqual(backend.stock[A], 4)  # el backend si lo registro


class RelojFalso:
    def __init__(self):
        self.ahora = 0.0

    def __call__(self) -> float:
        return self.ahora

    def dormir(self, segundos: float) -> None:
        self.ahora += segundos


def escaneo(sku: str = A, id_escaneo: str = "id-1") -> Escaneo:
    return Escaneo(sku=sku, frame=1, timestamp="2026-11-18 10:00:00", id_escaneo=id_escaneo)


class ColaRegistroTests(unittest.TestCase):
    def test_fallo_temporal_se_reintenta_con_el_mismo_id(self):
        respuestas = [
            ResultadoRegistro(ok=False, estado_http=None, detalle="caido", reintentable=True),
            ResultadoRegistro(ok=True, estado_http=200, detalle="ok", stock=4),
        ]
        enviados: list[str] = []

        def enviar(e: Escaneo) -> ResultadoRegistro:
            enviados.append(e.id_escaneo)
            return respuestas.pop(0)

        reloj = RelojFalso()
        cola = ColaRegistro(enviar, reloj=reloj)
        cola.agregar(escaneo())

        self.assertEqual(cola.procesar(), [])
        self.assertEqual(len(cola.pendientes), 1)
        self.assertEqual(cola.procesar(), [])  # todavia no toca: espera 1 s
        reloj.ahora = 1.0
        terminados = cola.procesar()

        self.assertEqual([e.estado for e in terminados], ["registrado"])
        self.assertEqual(enviados, ["id-1", "id-1"])
        self.assertEqual(terminados[0].intentos, 2)
        self.assertEqual(cola.pendientes, [])

    def test_espera_entre_reintentos_crece_hasta_el_maximo(self):
        reloj = RelojFalso()
        cola = ColaRegistro(
            lambda e: ResultadoRegistro(ok=False, estado_http=None, detalle="caido", reintentable=True),
            reloj=reloj,
            espera_inicial=1.0,
            espera_maxima=4.0,
        )
        cola.agregar(escaneo())
        esperas = []
        for _ in range(5):
            cola.procesar()
            pendiente = cola.pendientes[0]
            esperas.append(pendiente.proximo_intento - reloj.ahora)
            reloj.ahora = pendiente.proximo_intento
        self.assertEqual(esperas, [1.0, 2.0, 4.0, 4.0, 4.0])

    def test_fallo_permanente_no_se_reintenta(self):
        llamadas = []

        def enviar(e):
            llamadas.append(e.id_escaneo)
            return ResultadoRegistro(ok=False, estado_http=404, detalle="no existe")

        cola = ColaRegistro(enviar, reloj=RelojFalso())
        cola.agregar(escaneo())
        terminados = cola.procesar()
        cola.procesar()

        self.assertEqual([e.estado for e in terminados], ["rechazado"])
        self.assertEqual(llamadas, ["id-1"])

    def test_con_backend_caido_no_insiste_con_los_demas_en_la_misma_pasada(self):
        llamadas = []

        def enviar(e):
            llamadas.append(e.id_escaneo)
            return ResultadoRegistro(ok=False, estado_http=None, detalle="caido", reintentable=True)

        cola = ColaRegistro(enviar, reloj=RelojFalso())
        cola.agregar(escaneo(id_escaneo="id-1"))
        cola.agregar(escaneo(id_escaneo="id-2"))
        cola.procesar()

        self.assertEqual(llamadas, ["id-1"])
        self.assertEqual(len(cola.pendientes), 2)

    def test_pendientes_se_guardan_y_se_recuperan_con_el_mismo_id(self):
        caido = lambda e: ResultadoRegistro(ok=False, estado_http=None, detalle="caido", reintentable=True)  # noqa: E731
        with tempfile.TemporaryDirectory() as tmp:
            ruta = Path(tmp) / "pendientes.json"
            cola = ColaRegistro(caido, reloj=RelojFalso(), ruta_pendientes=ruta)
            cola.agregar(escaneo(id_escaneo="id-persistido"))
            cola.procesar()
            self.assertTrue(ruta.exists())
            cola.cerrar()  # simula el reinicio del proceso

            enviados = []

            def enviar(e):
                enviados.append(e.id_escaneo)
                return ResultadoRegistro(ok=True, estado_http=200, detalle="ok", duplicado=True)

            recuperada = ColaRegistro(enviar, reloj=RelojFalso(), ruta_pendientes=ruta)
            self.assertEqual([e.id_escaneo for e in recuperada.pendientes], ["id-persistido"])
            terminados = recuperada.procesar()

            self.assertEqual(enviados, ["id-persistido"])
            self.assertEqual([e.estado for e in terminados], ["duplicado"])
            self.assertFalse(ruta.exists())
            recuperada.cerrar()

    def test_vaciar_reintenta_hasta_registrar_o_agotar_la_espera(self):
        respuestas = [ResultadoRegistro(ok=False, estado_http=503, detalle="", reintentable=True)] * 2 + [
            ResultadoRegistro(ok=True, estado_http=200, detalle="ok")
        ]
        reloj = RelojFalso()
        cola = ColaRegistro(lambda e: respuestas.pop(0), reloj=reloj)
        cola.agregar(escaneo())

        terminados = cola.vaciar(espera_max=10, dormir=reloj.dormir)

        self.assertEqual([e.estado for e in terminados], ["registrado"])
        self.assertEqual(reloj.ahora, 3.0)  # 1 s + 2 s de espera

    def test_vaciar_respeta_el_limite(self):
        reloj = RelojFalso()
        cola = ColaRegistro(
            lambda e: ResultadoRegistro(ok=False, estado_http=None, detalle="caido", reintentable=True),
            reloj=reloj,
        )
        cola.agregar(escaneo())
        cola.vaciar(espera_max=5, dormir=reloj.dormir)
        self.assertEqual(len(cola.pendientes), 1)
        self.assertLessEqual(reloj.ahora, 5.0)


class RecuperacionExtremoAExtremoTests(unittest.TestCase):
    """Cola real + registrar_en_backend real contra el backend falso."""

    def cola_contra(self, url: str) -> tuple[ColaRegistro, RelojFalso]:
        reloj = RelojFalso()
        cola = ColaRegistro(
            enviar=lambda e: registrar_en_backend(e.sku, e.id_escaneo, url, timeout=2),
            reloj=reloj,
        )
        return cola, reloj

    def test_respuesta_perdida_se_reintenta_sin_duplicar(self):
        with BackendFalso({A: 0}, perder_respuestas=1) as backend:
            cola, reloj = self.cola_contra(backend.url)
            cola.agregar(escaneo())
            terminados = cola.vaciar(espera_max=10, dormir=reloj.dormir)

        self.assertEqual([e.estado for e in terminados], ["duplicado"])
        self.assertEqual(backend.stock[A], 1)  # una sola unidad
        self.assertEqual(len(backend.peticiones), 2)
        self.assertEqual({p["idEscaneo"] for p in backend.peticiones}, {"id-1"})

    def test_error_500_se_reintenta_y_registra_una_vez(self):
        with BackendFalso({A: 0}, fallar_con=[500, 503]) as backend:
            cola, reloj = self.cola_contra(backend.url)
            cola.agregar(escaneo())
            terminados = cola.vaciar(espera_max=10, dormir=reloj.dormir)

        self.assertEqual([e.estado for e in terminados], ["registrado"])
        self.assertEqual(backend.stock[A], 1)
        self.assertEqual(terminados[0].intentos, 3)

    def test_dos_prendas_iguales_son_dos_ids_y_suman_dos(self):
        with BackendFalso({A: 0}, perder_respuestas=1) as backend:
            cola, reloj = self.cola_contra(backend.url)
            cola.agregar(escaneo(id_escaneo="prenda-1"))
            cola.agregar(escaneo(id_escaneo="prenda-2"))
            terminados = cola.vaciar(espera_max=10, dormir=reloj.dormir)

        self.assertEqual(backend.stock[A], 2)
        self.assertEqual(sorted(e.estado for e in terminados), ["duplicado", "registrado"])

    def test_backend_caido_deja_el_escaneo_pendiente(self):
        cola, reloj = self.cola_contra(puerto_cerrado())
        cola.agregar(escaneo())
        cola.vaciar(espera_max=3, dormir=reloj.dormir)
        self.assertEqual(len(cola.pendientes), 1)
        self.assertGreaterEqual(cola.pendientes[0].intentos, 2)


class IdsDeEscaneoTests(unittest.TestCase):
    def test_cada_escaneo_tiene_un_id_distinto(self):
        frames = frames_secuencia(((A, A), 5))
        escaneos = procesar_frames(frames, cola=None)
        self.assertEqual(len({e.id_escaneo for e in escaneos}), 2)


class DesacoplamientoHttpTests(unittest.TestCase):
    """La captura no debe esperar a las peticiones HTTP del trabajador."""

    def _capturar_con_latencia(self, latencia: float, espera_final: float, skus: tuple[str, ...]):
        tiempos: list[float] = []

        def generador():
            for sku in skus:
                for _ in range(5):
                    tiempos.append(time.monotonic())
                    yield frame_con(sku)

        with BackendFalso({sku: 0 for sku in skus}, latencia=latencia) as backend:
            cola = ColaRegistro(
                lambda e: registrar_en_backend(e.sku, e.id_escaneo, backend.url, timeout=10)
            )
            escaneos = procesar_frames(generador(), cola, espera_final=espera_final)
            peticiones = len(backend.peticiones)
            stock = dict(backend.stock)
        return tiempos, escaneos, peticiones, stock

    def test_backend_con_latencia_de_3_segundos_no_bloquea_la_captura(self):
        tiempos, escaneos, peticiones, stock = self._capturar_con_latencia(3.0, 6.0, (A,))
        duracion_captura = tiempos[-1] - tiempos[0]
        self.assertEqual(len(tiempos), 5)
        self.assertLess(duracion_captura, 2.0, "la captura esperó al backend")
        self.assertEqual(len(escaneos), 1)
        self.assertEqual(peticiones, 1)
        self.assertEqual(stock[A], 1)

    def test_backend_con_latencia_de_5_segundos_no_bloquea_la_captura(self):
        tiempos, escaneos, peticiones, stock = self._capturar_con_latencia(5.0, 8.0, (A,))
        duracion_captura = tiempos[-1] - tiempos[0]
        self.assertLess(duracion_captura, 2.0, "la captura esperó al backend")
        self.assertEqual(len(escaneos), 1)
        self.assertEqual(stock[A], 1)

    def test_varios_qr_detectados_mientras_hay_peticion_pendiente(self):
        tiempos, escaneos, peticiones, stock = self._capturar_con_latencia(1.5, 9.0, (A, B, C))
        self.assertLess(tiempos[-1] - tiempos[0], 2.0)
        self.assertEqual(sorted(e.sku for e in escaneos), sorted([A, B, C]))
        self.assertEqual(len({e.id_escaneo for e in escaneos}), 3)
        self.assertEqual(peticiones, 3)
        self.assertEqual(stock, {A: 1, B: 1, C: 1})

    def test_http_500_y_respuesta_perdida_con_un_solo_id(self):
        with BackendFalso({A: 0}, fallar_con=[500], perder_respuestas=1) as backend:
            cola = ColaRegistro(
                lambda e: registrar_en_backend(e.sku, e.id_escaneo, backend.url, timeout=2)
            )
            procesar_frames(frames_secuencia(((A,), 3)), cola, espera_final=6)

        self.assertEqual(backend.stock[A], 1)
        self.assertEqual(len(backend.peticiones), 3)
        self.assertEqual({p["idEscaneo"] for p in backend.peticiones}.__len__(), 1)

    def test_un_solo_trabajador_por_cola(self):
        cola = ColaRegistro(lambda e: ResultadoRegistro(True, 200, "ok"))
        primero = TrabajadorRegistro(cola)
        segundo = TrabajadorRegistro(cola)
        primero.iniciar()
        segundo.iniciar()
        self.assertIsNotNone(primero._hilo)
        self.assertIs(segundo._hilo, None)
        primero.cerrar(0)
        cola.cerrar()


class PersistenciaPorDefectoTests(unittest.TestCase):
    def test_cierre_con_pendientes_y_recuperacion_tras_reinicio(self):
        with tempfile.TemporaryDirectory() as tmp:
            ruta = Path(tmp) / "pendientes.json"
            caido = puerto_cerrado()
            cola = ColaRegistro(
                lambda e: registrar_en_backend(e.sku, e.id_escaneo, caido, timeout=1),
                ruta_pendientes=ruta,
            )
            escaneos = procesar_frames(frames_secuencia(((A,), 3)), cola, espera_final=0.2)
            id_original = escaneos[0].id_escaneo

            guardado = json.loads(ruta.read_text(encoding="utf-8"))
            self.assertEqual(guardado[0]["id_escaneo"], id_original)
            self.assertNotIn("password", json.dumps(guardado).lower())

            with BackendFalso({A: 0}) as backend:
                reloj = RelojFalso()
                recuperada = ColaRegistro(
                    lambda e: registrar_en_backend(e.sku, e.id_escaneo, backend.url, timeout=2),
                    reloj=reloj,
                    ruta_pendientes=ruta,
                )
                self.assertEqual([e.id_escaneo for e in recuperada.pendientes], [id_original])
                terminados = recuperada.vaciar(espera_max=5, dormir=reloj.dormir)
                self.assertEqual([e.estado for e in terminados], ["registrado"])
                self.assertEqual(backend.stock[A], 1)
                self.assertFalse(ruta.exists())
                recuperada.cerrar()

    def test_ruta_persistente_por_defecto_sobrevive_a_reinicio_del_proceso(self):
        self.assertEqual(RUTA_PENDIENTES_PREDETERMINADA.name, "qr_pendientes.json")
        with tempfile.TemporaryDirectory() as tmp:
            ruta = Path(tmp) / "pendientes.json"
            with patch("deteccion.RUTA_PENDIENTES_PREDETERMINADA", ruta):
                with tempfile.TemporaryDirectory() as tmp_video:
                    video = generar_video_sintetico(
                        Path(tmp_video) / "con_qr.avi", [A], frames_qr=5, frames_vacio=1
                    )
                    self.assertEqual(
                        main(["--fuente", str(video), "--backend", puerto_cerrado(), "--espera-final", "0.2"]),
                        0,
                    )
                guardado = json.loads(ruta.read_text(encoding="utf-8"))
                id_original = guardado[0]["id_escaneo"]

                with BackendFalso({A: 0}) as backend:
                    with tempfile.TemporaryDirectory() as tmp_video:
                        vacio = generar_video_sintetico(
                            Path(tmp_video) / "vacio.avi", [], frames_vacio=3
                        )
                        self.assertEqual(
                            main(["--fuente", str(vacio), "--backend", backend.url, "--espera-final", "3"]),
                            0,
                        )
                    self.assertEqual(backend.stock[A], 1)  # una sola unidad
                    self.assertEqual(len(backend.peticiones), 1)
                    self.assertEqual(backend.peticiones[0]["idEscaneo"], id_original)
                    self.assertFalse(ruta.exists())

    def test_dos_instancias_no_usan_el_mismo_archivo(self):
        with tempfile.TemporaryDirectory() as tmp:
            ruta = Path(tmp) / "pendientes.json"
            primera = ColaRegistro(lambda e: ResultadoRegistro(True, 200, "ok"), ruta_pendientes=ruta)
            with self.assertRaises(RuntimeError):
                ColaRegistro(lambda e: ResultadoRegistro(True, 200, "ok"), ruta_pendientes=ruta)
            primera.cerrar()
            segunda = ColaRegistro(lambda e: ResultadoRegistro(True, 200, "ok"), ruta_pendientes=ruta)
            segunda.cerrar()


# --------------------------------------------------------------------------- review del 18/11 (2a ronda)


class BackendBloqueable:
    """`enviar` falso e idempotente cuya primera peticion se queda bloqueada.

    La peticion bloqueada SI registra el escaneo en el "backend" pero devuelve un
    fallo reintentable (respuesta perdida), igual que un timeout tras procesarlo.
    """

    def __init__(self):
        self.registrados: dict[str, str] = {}
        self.enviados: list[str] = []
        self.empezo = threading.Event()
        self.soltar = threading.Event()
        self._bloquear_primera = True
        self._lock = threading.Lock()

    def __call__(self, escaneo: Escaneo) -> ResultadoRegistro:
        with self._lock:
            self.enviados.append(escaneo.id_escaneo)
            bloquear, self._bloquear_primera = self._bloquear_primera, False
        if bloquear:
            self.empezo.set()
            self.soltar.wait(10)
            self.registrados.setdefault(escaneo.id_escaneo, escaneo.sku)
            return ResultadoRegistro(ok=False, estado_http=None, detalle="respuesta perdida", reintentable=True)
        duplicado = escaneo.id_escaneo in self.registrados
        self.registrados.setdefault(escaneo.id_escaneo, escaneo.sku)
        return ResultadoRegistro(ok=True, estado_http=200, detalle="ok", stock=1, duplicado=duplicado)


class CierreConPeticionBloqueadaTests(unittest.TestCase):
    """Hallazgo 1: el almacenamiento no se libera mientras el trabajador pueda escribir."""

    def test_cierre_incompleto_no_libera_el_almacenamiento_ni_pierde_ids(self):
        with tempfile.TemporaryDirectory() as tmp:
            ruta = Path(tmp) / "pendientes.json"
            backend = BackendBloqueable()
            cola = ColaRegistro(backend, ruta_pendientes=ruta)
            trabajador = TrabajadorRegistro(cola, notificar=lambda e: None)
            cola.agregar(escaneo(id_escaneo="id-bloqueado"))
            cola.agregar(escaneo(id_escaneo="id-en-espera"))
            trabajador.iniciar()
            self.assertTrue(backend.empezo.wait(5), "la peticion no llego a empezar")

            # Cierre con un plazo (0,2 s) menor que la peticion bloqueada.
            resultado_cierre: list[bool] = []

            def cerrar_todo():
                resultado_cierre.append(trabajador.cerrar(0.2))
                cola.cerrar()

            cierre = threading.Thread(target=cerrar_todo)
            cierre.start()
            time.sleep(0.6)  # el plazo ya vencio

            self.assertTrue(cierre.is_alive(), "el cierre no espero al trabajador")
            self.assertTrue(cola.almacenamiento_bloqueado)
            with self.assertRaises(RuntimeError):  # una segunda instancia no puede entrar
                ColaRegistro(backend, ruta_pendientes=ruta)
            guardados = {d["id_escaneo"] for d in json.loads(ruta.read_text(encoding="utf-8"))}
            self.assertEqual(guardados, {"id-bloqueado", "id-en-espera"})

            backend.soltar.set()  # la peticion original termina
            cierre.join(10)
            self.assertFalse(cierre.is_alive())
            self.assertEqual(resultado_cierre, [False])  # no termino dentro del plazo
            self.assertFalse(cola.almacenamiento_bloqueado)
            # Tras vencer el plazo no se inicio ninguna peticion nueva.
            self.assertEqual(backend.enviados, ["id-bloqueado"])

            # Ahora si entra la segunda instancia y recupera ambos ids sin duplicar.
            segunda = ColaRegistro(backend, ruta_pendientes=ruta, reloj=RelojFalso())
            self.assertEqual({e.id_escaneo for e in segunda.pendientes}, {"id-bloqueado", "id-en-espera"})
            terminados = segunda.vaciar(espera_max=1, dormir=lambda s: None)
            segunda.cerrar()

            self.assertEqual(
                {e.id_escaneo: e.estado for e in terminados},
                {"id-bloqueado": "duplicado", "id-en-espera": "registrado"},
            )
            self.assertEqual(len(backend.registrados), 2)  # una unidad por id
            self.assertFalse(ruta.exists())

    def test_detener_impide_iniciar_peticiones_nuevas(self):
        llamadas: list[str] = []
        cola = ColaRegistro(lambda e: llamadas.append(e.id_escaneo) or ResultadoRegistro(True, 200, "ok"))
        cola.agregar(escaneo())
        cola.detener()
        self.assertEqual(cola.procesar(), [])
        self.assertEqual(llamadas, [])

    def test_no_se_agrega_tras_cerrar(self):
        cola = ColaRegistro(lambda e: ResultadoRegistro(True, 200, "ok"))
        cola.cerrar()
        with self.assertRaises(RuntimeError):
            cola.agregar(escaneo())

    def test_bloqueo_se_libera_si_el_proceso_muere(self):
        # Un proceso hijo toma el bloqueo y termina sin cerrar la cola: el SO lo suelta.
        with tempfile.TemporaryDirectory() as tmp:
            ruta = Path(tmp) / "pendientes.json"
            codigo = (
                "import sys, os; sys.path.insert(0, sys.argv[1]);"
                "from pathlib import Path; from deteccion import ColaRegistro, ResultadoRegistro;"
                "ColaRegistro(lambda e: None, ruta_pendientes=Path(sys.argv[2])); os._exit(0)"
            )
            import subprocess

            subprocess.run(
                [sys.executable, "-c", codigo, str(Path(__file__).resolve().parents[1]), str(ruta)],
                check=True, timeout=60,
            )
            self.assertTrue(ruta.with_name(ruta.name + ".lock").exists())  # quedo el archivo...
            cola = ColaRegistro(lambda e: ResultadoRegistro(True, 200, "ok"), ruta_pendientes=ruta)
            cola.cerrar()  # ...pero no el bloqueo


class SupervisionTrabajadorTests(unittest.TestCase):
    """Hallazgo 2: el trabajador no puede morir en silencio."""

    def test_backoff_con_mas_de_1024_intentos_no_desborda(self):
        reloj = RelojFalso()
        cola = ColaRegistro(
            lambda e: ResultadoRegistro(ok=False, estado_http=None, detalle="caido", reintentable=True),
            reloj=reloj,
        )
        cola.agregar(escaneo())
        for _ in range(1100):
            cola.procesar()
            reloj.ahora = cola.pendientes[0].proximo_intento
        pendiente = cola.pendientes[0]
        self.assertEqual(pendiente.intentos, 1100)
        self.assertEqual(pendiente.id_escaneo, "id-1")

    def test_200_con_json_valido_pero_estructura_incorrecta_no_se_da_por_registrado(self):
        cuerpos = [{"ok": True}, [], {"idEscaneo": "otro-id", "stock": 1, "duplicado": False},
                   {"idEscaneo": "id-1", "stock": "1", "duplicado": False},
                   {"idEscaneo": "id-1", "stock": 1}]
        for cuerpo in cuerpos:
            with self.subTest(cuerpo=cuerpo), BackendFalso({A: 0}, respuesta_200=cuerpo) as backend:
                resultado = registrar_en_backend(A, "id-1", backend.url)
                self.assertFalse(resultado.ok)
                self.assertTrue(resultado.reintentable)

    def test_respuesta_con_estructura_incorrecta_conserva_el_uuid(self):
        with BackendFalso({A: 0}, respuesta_200={"ok": True}) as backend:
            cola = ColaRegistro(lambda e: registrar_en_backend(e.sku, e.id_escaneo, backend.url), reloj=RelojFalso())
            cola.agregar(escaneo(id_escaneo="id-ambiguo"))
            self.assertEqual(cola.procesar(), [])
        self.assertEqual([e.id_escaneo for e in cola.pendientes], ["id-ambiguo"])

    def test_excepcion_inesperada_detiene_la_captura_y_conserva_pendientes(self):
        def enviar_roto(e):
            raise RuntimeError("fallo inesperado")

        with tempfile.TemporaryDirectory() as tmp:
            ruta = Path(tmp) / "pendientes.json"
            cola = ColaRegistro(enviar_roto, ruta_pendientes=ruta)
            procesados: list[int] = []

            def frames():
                for i in range(400):
                    procesados.append(i)
                    yield frame_con(A) if i < 5 else frame_con()
                    time.sleep(0.005)

            with self.assertRaises(TrabajadorDetenidoError):
                procesar_frames(frames(), cola, espera_final=0.2)

            self.assertLess(len(procesados), 400, "siguio capturando sin trabajador")
            self.assertEqual(len(json.loads(ruta.read_text(encoding="utf-8"))), 1)
            self.assertFalse(cola.almacenamiento_bloqueado)

    def test_error_de_escritura_del_almacenamiento_detiene_el_lector(self):
        class ColaConDiscoRoto(ColaRegistro):
            def _guardar(self):
                if threading.current_thread().name == "registro-qr":
                    raise OSError("disco lleno")
                super()._guardar()

        with tempfile.TemporaryDirectory() as tmp:
            ruta = Path(tmp) / "pendientes.json"
            cola = ColaConDiscoRoto(lambda e: ResultadoRegistro(True, 200, "ok", stock=1), ruta_pendientes=ruta)

            def frames():
                for i in range(400):
                    yield frame_con(A) if i < 5 else frame_con()
                    time.sleep(0.005)

            with self.assertRaises(TrabajadorDetenidoError) as ctx:
                procesar_frames(frames(), cola, espera_final=0.2)
            self.assertIsInstance(ctx.exception.__cause__, OSError)
            # El escaneo sigue en disco: al reiniciar se reenvia con su id (el backend
            # respondera duplicado si ya lo tenia).
            self.assertEqual(len(json.loads(ruta.read_text(encoding="utf-8"))), 1)

    def test_main_devuelve_error_si_el_trabajador_falla(self):
        with tempfile.TemporaryDirectory() as tmp:
            video = generar_video_sintetico(Path(tmp) / "v.avi", [A], frames_qr=5, frames_vacio=200)
            with patch("deteccion.registrar_en_backend", side_effect=RuntimeError("boom")):
                codigo = main(["--fuente", str(video), "--backend", "http://127.0.0.1:1",
                               "--pendientes", str(Path(tmp) / "p.json"), "--espera-final", "0.2"])
            self.assertEqual(codigo, 2)


if __name__ == "__main__":
    unittest.main()
