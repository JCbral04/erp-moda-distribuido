# TLS/HTTPS por entorno — erp-moda-distribuido

Issue #60. Documenta cómo se sirve HTTPS y qué certificado se valida en cada
entorno, y la comunicación backend ↔ microservicio IA.

## 1. Desarrollo local (`dotnet run` / docker compose)

- La API sirve **HTTP** (`http://localhost:5000` en compose, puertos de
  `launchSettings.json` en local). Sin TLS: es tráfico solo de loopback.
- Swagger UI activo únicamente en `Development`.
- **Supabase (BD):** TLS siempre. `SSL Mode=Require` con el certificado TLS
  válido que sirve Supabase. **Nunca** `Trust Server Certificate=true`.
- Opcional en Windows: `dotnet dev-certs https --trust` para HTTPS local de
  prueba; no requerido por el compose.

## 2. GitHub Codespaces

- El puerto 5000 se expone por **forwarding HTTPS de GitHub**: el navegador
  habla TLS con el proxy de GitHub y este habla HTTP interno con el contenedor.
- El certificado lo emite GitHub (válido para `*.github.dev`); no hay nada que
  configurar en la app. La app sigue sirviendo HTTP dentro del contenedor.
- La BD es Supabase con el mismo esquema del punto 1 (User Secrets).

## 3. Producción (despliegue futuro)

- Terminación TLS en un **reverse proxy** (nginx, Cloudflare, ELB, etc.) con
  certificado público válido (Let's Encrypt u otro CA reconocido).
- La app corre detrás del proxy en HTTP interno; el proxy:
  - Redirige HTTP → HTTPS (301).
  - Sirve la cabecera `Strict-Transport-Security` (HSTS, activado por
    `UseHsts()` cuando `ASPNETCORE_ENVIRONMENT=Production`, max-age 30 días).
- La clave de firma JWT y la connection string llegan por **variables de
  entorno** del servidor, nunca por archivos versionados.

## 4. Comunicación backend ↔ microservicio IA

- **Solo red Docker interna** (`erp-net`): el servicio `ia` **no publica
  puerto al host** en producción; el backend lo llama por `http://ia:8000`.
  HTTP en red interna de un solo host: aceptable (issue #60, alcance).
- Si en el futuro `ia` se expone fuera del compose (otro host), ese tráfico
  debe pasar a TLS (issue #60: "si se expone, exigir TLS").

## 5. Checklist de secretos por entorno

| Secreto | Dev local | Codespaces | Producción | CI |
|---|---|---|---|---|
| Connection string Supabase | User Secrets (`setup-user-secrets.ps1`) | User Secrets | Variable de entorno | GitHub Actions secrets |
| Clave firma JWT (issue #59) | User Secrets | User Secrets | Variable de entorno | GitHub Actions secrets |
| POSTGRES_PASSWORD (compose) | `.env` (gitignored) | `.env` | n/a | n/a |

Regla: ningún secreto en el repo, appsettings versionados, compose versionado
ni logs. Verificación: verificación automatizada con TruffleHog en cada PR
(devsecops.yml, `--only-verified`).

## Verificación del criterio "grep sin Trust Server Certificate"

```bash
grep -ri "Trust Server Certificate" --include="*.yml" --include="*.yaml" --include="*.json" --include="*.md" .
```

