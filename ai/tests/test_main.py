from fastapi.testclient import TestClient

from ai.main import app


@app.get("/__test/exception")
def lanzar_excepcion():
    raise RuntimeError("boom secreto interno")


def test_health_genera_correlation_id():
    client = TestClient(app)
    response = client.get("/health")
    assert response.status_code == 200
    assert "X-Correlation-ID" in response.headers
    assert response.headers["X-Correlation-ID"]


def test_recibe_correlation_id_valido():
    client = TestClient(app)
    response = client.get("/health", headers={"X-Correlation-ID": "abc-123"})
    assert response.status_code == 200
    assert response.headers["X-Correlation-ID"] == "abc-123"


def test_reemplaza_correlation_id_invalido():
    client = TestClient(app)
    response = client.get("/health", headers={"X-Correlation-ID": "invalido con espacios"})
    assert response.status_code == 200
    assert response.headers["X-Correlation-ID"] != "invalido con espacios"


def test_detectar_funciona():
    client = TestClient(app)
    response = client.post("/detectar", headers={"X-Correlation-ID": "corr-456"})
    assert response.status_code == 200
    assert response.headers["X-Correlation-ID"] == "corr-456"


def test_excepcion_devuelve_500_con_correlation_id():
    client = TestClient(app, raise_server_exceptions=False)
    response = client.get("/__test/exception", headers={"X-Correlation-ID": "corr-error"})
    assert response.status_code == 500
    assert response.headers["X-Correlation-ID"] == "corr-error"
    assert "boom secreto interno" not in response.text
