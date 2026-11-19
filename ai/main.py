import logging
import re
import uuid

from fastapi import FastAPI, Request
from starlette.middleware.base import BaseHTTPMiddleware

app = FastAPI(title="ERP Moda - Microservicio IA")

logger = logging.getLogger("erp_moda_ia")
logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")

_CORRELATION_HEADER = "X-Correlation-ID"
_MAX_LENGTH = 64
_VALID_PATTERN = re.compile(r"^[A-Za-z0-9._-]+$")


def es_correlation_id_valido(valor: str | None) -> bool:
    if not valor:
        return False
    if len(valor) > _MAX_LENGTH:
        return False
    if any(ord(ch) < 32 or ord(ch) == 127 for ch in valor):
        return False
    return _VALID_PATTERN.fullmatch(valor) is not None


class CorrelationIdMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next):
        entrante = request.headers.get(_CORRELATION_HEADER)
        correlation_id = entrante if es_correlation_id_valido(entrante) else uuid.uuid4().hex

        request.state.correlation_id = correlation_id
        response = await call_next(request)
        response.headers[_CORRELATION_HEADER] = correlation_id
        logger.info("correlation_id=%s method=%s path=%s", correlation_id, request.method, request.url.path)
        return response


app.add_middleware(CorrelationIdMiddleware)


@app.get("/health")
def health(request: Request):
    correlation_id = getattr(request.state, "correlation_id", None)
    logger.info("correlation_id=%s operation=health", correlation_id)
    return {"status": "ok", "servicio": "ia"}


@app.post("/detectar")
def detectar(request: Request):
    correlation_id = getattr(request.state, "correlation_id", None)
    logger.info("correlation_id=%s operation=detectar", correlation_id)
    return {"detalle": "Deteccion aun no implementada"}
