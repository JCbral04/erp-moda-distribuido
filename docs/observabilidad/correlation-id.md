# Correlation ID y logs estructurados (Issue #62)

## Generación y validación

- El backend lee la cabecera `X-Correlation-ID` en cada solicitud.
- Si no existe o no es válido (más de 64 caracteres, caracteres de control o caracteres fuera de `[A-Za-z0-9._-]`), se genera un identificador nuevo con `Guid.NewGuid().ToString("N")`.
- El identificador se guarda en `HttpContext.Items["CorrelationId"]` y se devuelve en la respuesta en `X-Correlation-ID`.
- El mismo identificador se mantiene durante todo el ciclo de vida de la solicitud, incluso si se produce una excepción.

## Propagación

- Backend no realiza llamadas HTTP salientes a IA en el estado actual del repositorio.
- El mecanismo reutilizable queda disponible mediante `CorrelationIdMiddleware.Get(context)`.
- El microservicio IA recibe `X-Correlation-ID`, lo valida, genera uno si falta o es inválido, lo incluye en sus logs y lo devuelve en la respuesta.

## Campos de log

Los logs estructurados incluyen, cuando corresponde:

- `CorrelationId`
- `Operation`
- `UserId` (solo identidad autenticada)
- `Result`
- `StatusCode`
- `Timestamp` (proporcionado por el logger)

Eventos registrados:

- Ventas: creada, confirmada, anulada y fallos.
- Autenticación: intentos fallidos (401).
- Autorización: solicitudes rechazadas (403).

## Información prohibida en logs

Nunca registrar:

- Contraseñas.
- Tokens JWT completos.
- Claves de firma.
- Connection strings.
- Credenciales de base de datos.
- Secretos de configuración.
- Datos personales innecesarios.
- Cabecera `Authorization`.
- Cuerpos completos de solicitudes sensibles.

## Niveles por entorno

- Development: nivel Information para depuración.
- Production: nivel Warning/Error para operación, sin detalles internos ni stack traces al cliente.

## Ejemplo

Solicitud:

```
POST /api/ventas HTTP/1.1
X-Correlation-ID: corr-123
Authorization: Bearer <token>
```

Respuesta:

```
X-Correlation-ID: corr-123
```

Consulta de logs filtrando por `CorrelationId=corr-123` para rastrear la operación en backend e IA.
