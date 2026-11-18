# TLS/HTTPS por entorno — erp-moda-distribuido

Issue #60. Documenta cómo se sirve HTTPS, qué certificado se valida en cada
entorno y la comunicación backend ↔ microservicio IA. Actualizado según las
reviews del PR #93: configuración real del compose (base prod / override dev),
HSTS explícito, ForwardedHeaders y puertos ligados a loopback en dev.

## 1. Desarrollo local (docker compose / dotnet run)

- Comando: `docker compose up` — aplica `docker-compose.yml` +
  `docker-compose.override.yml` automáticamente. El override publica al host,
  **todos ligados a 127.0.0.1** (sin exposición a la LAN):
  - `127.0.0.1:5000` → API (backend `5000:8080`), `Development` + Swagger activo.
  - `127.0.0.1:5432` → Postgres y `127.0.0.1:6379` → Redis (acceso local).
  - `127.0.0.1:8000` → microservicio IA (depuración).
- `ASPNETCORE_ENVIRONMENT=Development` viene del override; Swagger UI activo.
- **Supabase (BD):** TLS siempre. `SSL Mode=Require` con el certificado TLS
  válido que sirve Supabase. **Nunca** `Trust Server Certificate=true`.

## 2. GitHub Codespaces

- El puerto 5000 se expone por **forwarding HTTPS de GitHub**: el navegador
  habla TLS con el proxy de GitHub y este habla HTTP interno con el contenedor.
- El certificado lo emite GitHub (válido para `*.github.dev`); nada que
  configurar en la app. La app sigue sirviendo HTTP dentro del contenedor.
- La BD es Supabase con el mismo esquema del punto 1 (User Secrets).

## 3. Producción (despliegue futuro)

- Comando: `docker compose -f docker-compose.yml up -d` — **sin el override**.
  El compose base **no publica ningún puerto al host**: db, redis, backend e
  ia son internos a la red `erp-net`. Solo el **reverse proxy** expone 443.
- `ASPNETCORE_ENVIRONMENT=Production` en el compose base: Swagger apagado,
  HSTS activo (cabecera `Strict-Transport-Security`, max-age 30 días).
- Terminación TLS en el reverse proxy (nginx, Cloudflare, ELB) con certificado
  público válido (Let's Encrypt u otro CA reconocido); redirige HTTP → HTTPS.
- **ForwardedHeaders obligatorio**: `UseForwardedHeaders()` (registrado antes
  de `UseHsts`/`UseHttpsRedirection`) acepta `X-Forwarded-For`/
  `X-Forwarded-Proto` **solo del proxy de confianza** (`KnownNetworks`/
  `KnownProxies` limpios; única IP en `ProxyTrustedIP`, default `127.0.0.1`).
  Sin esto, el esquema percibido falla detrás del proxy y HSTS no emite el
  header. Configurar la IP real del proxy por entorno.
- **Opciones HSTS reales** (`AddHsts` en `Program.cs`): `MaxAge = 30 días`.
  `Preload = false`: el envío a la lista HSTS preload es decisión del dominio
  del despliegue y **no es revocable**. `IncludeSubdomains = false`: el ERP se
  sirve en un solo host; habilitarlo forzaría HTTPS en subdominios futuros sin
  certificado y los rompería.
- La clave de firma JWT y la connection string llegan por **variables de
  entorno**, nunca por archivos versionados.

## 4. Comunicación backend ↔ microservicio IA

- En la configuración base (`docker-compose.yml`, usada en producción), el
  servicio `ia` **no publica ningún puerto al host**. Producción usa
  únicamente el compose base (`docker compose -f docker-compose.yml up -d`),
  sin override.
- En desarrollo local, `docker-compose.override.yml` publica
  `127.0.0.1:8000:8000` **solo para depuración** desde el equipo local.
- El backend se comunica con IA por la red Docker interna `erp-net`
  (`http://ia:8000`, variable `IaService__BaseUrl`), con o sin puerto
  publicado en dev.
- HTTP en red interna de un solo host: aceptable (issue #60, alcance). Si en
  el futuro `ia` se expone fuera del compose (otro host), ese tráfico debe
  pasar a TLS.

## 5. Checklist de secretos por entorno

| Secreto | Dev local | Codespaces | Producción | CI |
|---|---|---|---|---|
| Connection string Supabase | User Secrets | User Secrets | Variable de entorno | GitHub Actions secrets |
| Clave firma JWT (issue #59) | User Secrets | User Secrets | Variable de entorno | GitHub Actions secrets |
| POSTGRES_PASSWORD (compose) | `.env` (gitignored) | `.env` | n/a | n/a |
| ProxyTrustedIP (proxy confianza) | n/a | n/a | Variable de entorno | n/a |

Regla: ningún secreto en el repo, appsettings versionados, compose versionado
ni logs. Verificación automatizada con TruffleHog en cada PR
(devsecops.yml, `--only-verified`).

## Verificación del criterio "grep sin Trust Server Certificate"

```bash
grep -ri "Trust Server Certificate" --include="*.yml" --include="*.yaml" --include="*.json" --include="*.md" .
# Esperado: solo menciones en documentación prohibiéndolo (este archivo y
# SETUP_INSTRUCTIONS.md). Ninguna conexión real configurada con ese flag.
