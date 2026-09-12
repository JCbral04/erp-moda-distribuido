from fastapi import FastAPI

app = FastAPI(title="ERP Moda - Microservicio IA")


@app.get("/health")
def health():
    return {"status": "ok", "servicio": "ia"}


@app.post("/detectar")
def detectar():
    # Placeholder: la detección con OpenCV se implementa en sprints siguientes
    return {"detalle": "Detección aún no implementada"}