# Guía: Docker Compose — entorno de desarrollo

**Issue relacionado:** #2 · **Responsable:** Juan Esteban Cabral (Arquitectura) · **Fecha:** 13/09/2026

## ¿Qué se hizo?

Se configuró la orquestación completa del entorno de desarrollo con Docker Compose. Con un solo comando (`docker compose up --build`), cualquier miembro del equipo levanta los 4 servicios del sistema sin instalar nada más que Docker.

## Servicios

| Servicio | Imagen base | Puerto | Rol en la arquitectura distribuida |
|:---|:---|:---|:---|
| `db` | postgres:16-alpine | 5432 | Base de datos relacional. Persiste datos en el volumen `pgdata` |
| `redis` | redis:7-alpine | 6379 | Caché distribuido |
| `backend` | build desde `backend/ErpModa.Api/Dockerfile` | 5000 → 8080 | API principal .NET 8 (nodos de lógica de negocio) |
| `ia` | build desde `ai/Dockerfile` | 8000 | Microservicio Python + FastAPI + OpenCV (módulo de visión) |

Todos los servicios comparten la red bridge `erp-net`, por lo que se comunican entre sí usando el nombre del servicio como host (ejemplo: el backend llama a la IA con `http://ia:8000`, no con `localhost`).

## Documentación de la API con OpenAPI/Swagger

El backend genera su documentación automáticamente con OpenAPI (paquete `Microsoft.AspNetCore.OpenApi`, incluido en la plantilla del proyecto). Con los contenedores arriba, la interfaz interactiva Swagger UI está disponible en:

```
http://localhost:5000/swagger
```

### ¿Qué muestra?

Cada endpoint del backend con su ruta, método HTTP, parámetros, formato del JSON de entrada y salida, y códigos de respuesta. Además permite **ejecutar los endpoints desde el navegador** (botón "Try it out"), sin necesidad de Postman ni curl.

### ¿Por qué lo usamos como documentación del proyecto?

- **Se mantiene sola:** se genera desde el código C#, así que nunca queda desactualizada respecto a lo que la API realmente hace.
- **Soporta la regla de contratos primero:** en el Sprint 1 definiremos los contratos técnicos (endpoints y JSON entre módulos), y OpenAPI será su expresión viva en el código.
- **Facilita las pruebas del equipo:** Jair y Andres pueden probar los endpoints de los demás desde el navegador sin herramientas extra.

### Matiz de seguridad

Swagger UI solo se expone cuando `ASPNETCORE_ENVIRONMENT=Development` (así está configurado el compose). En un despliegue de producción queda deshabilitado automáticamente — es la práctica estándar.

> **Nota:** el microservicio de IA (FastAPI) también genera su propia documentación OpenAPI automáticamente, disponible en `http://localhost:8000/docs`.

## Decisiones técnicas y por qué

### 1. Multi-stage build en el Dockerfile del backend

El Dockerfile tiene dos etapas: una compila con la imagen del SDK de .NET (~800 MB) y otra ejecuta solo con el runtime (~200 MB). La imagen final es pequeña y no expone herramientas de compilación.

### 2. Healthchecks con `depends_on: service_healthy`

PostgreSQL y Redis tardan unos segundos en estar listos. Sin healthcheck, el backend arrancaba antes que la base de datos y fallaba con "conexión rechazada". El healthcheck (`pg_isready` / `redis-cli ping`) garantiza el orden correcto de arranque.

### 3. Volumen `pgdata` para persistencia

Sin volumen, los datos de PostgreSQL se pierden cada vez que se hace `docker compose down`. El volumen los guarda en el disco del host.

### 4. El microservicio de IA arranca con placeholders

Los endpoints `/health` y `/detectar` de `ai/main.py` son mínimos a propósito: el issue #2 exige verificar que el backend y la IA se comunican, y eso solo se puede probar con un servicio que responda. La lógica real de detección con OpenCV la implementa Jair en el issue #7 sobre esta base.

### 5. Copiar dependencias antes que el código (caché de Docker)

En ambos Dockerfiles se copian primero los archivos de dependencias (`.csproj` / `requirements.txt`) y luego el código. Así, un cambio de código que no toque dependencias reutiliza las capas cacheadas y el rebuild tarda segundos en vez de minutos.

## Incidencia durante la implementación

El primer `docker compose up --build` falló con el error `NETSDK1064` (paquete `Microsoft.AspNetCore.OpenApi` no encontrado): la descarga de paquetes NuGet durante el `dotnet restore` quedó incompleta por un corte de red, y Docker cacheó esa capa dañada, haciendo fallar los intentos siguientes.

**Solución:** reconstruir sin caché (`docker compose build --no-cache backend`) y quitar el flag `--no-restore` del `dotnet publish`, para que el publish complete la restauración si falta algo. Lección: ante errores de paquetes en Docker, invalidar la capa de restore antes de reintentar.

## Verificación

Con los 4 contenedores arriba:

```bash
curl http://localhost:5000/weatherforecast   # Backend .NET → JSON del clima (plantilla)
curl http://localhost:8000/health            # IA → {"status":"ok","servicio":"ia"}
```

Ambos respondieron correctamente el 13/09/2026. Documentación visual confirmada en `http://localhost:5000/swagger` y `http://localhost:8000/docs`. Criterios de aceptación del issue #2 cumplidos.

## Comandos útiles

```bash
docker compose up --build                     # Construir y levantar todo
docker compose up -d                          # Levantar en segundo plano
docker compose logs backend                   # Ver logs de un servicio
docker compose down                           # Detener todo (los datos de la BD persisten)
docker compose down -v                        # Detener todo Y borrar los datos de la BD
docker compose build --no-cache <servicio>    # Reconstruir sin caché
```

## Pendiente (Sprint 1)

- Evaluar migración de la base de datos: Supabase (Postgres gestionado) o SQL Server. Ambas opciones son compatibles con EF Core cambiando solo el proveedor y el connection string.
- Mover credenciales del compose a un archivo `.env` (ya excluido en `.gitignore`) cuando haya valores reales.
- Definir los contratos técnicos entre módulos y reflejarlos como endpoints documentados con OpenAPI.
