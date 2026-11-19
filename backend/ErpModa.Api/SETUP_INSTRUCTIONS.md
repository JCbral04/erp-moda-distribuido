# Instrucciones de Configuración — Conexión a Supabase

## Parámetros de conexión

| Parámetro | Valor |
|---|---|
| Host | `aws-0-sa-east-1.pooler.supabase.com` |
| Port | `5432` |
| Database | `postgres` |
| Username | `postgres.gcqyxbdifutrbbdouzlk` |
| Password | **Configurar localmente — ver sección 1** |
| SSL Mode | `Require` |

> **Nota sobre SSL:** No uses `Trust Server Certificate=true` como configuración habitual.
> Supabase provee certificados TLS válidos. `SSL Mode=Require` es suficiente y seguro.
> Consulta la sección de solución de problemas si encuentras errores de certificado.

---

## 1. Configurar User Secrets (credenciales locales)

La contraseña **nunca debe escribirse en ningún archivo del repositorio**.
Se gestiona con `dotnet user-secrets`, que la almacena localmente en
`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`, fuera del control de versiones.

### Opción A: Script PowerShell (recomendado)

Ejecuta desde `backend/ErpModa.Api`:

```powershell
.\setup-user-secrets.ps1
```

El script solicita la contraseña de forma segura (`Read-Host -AsSecureString`)
y la registra **sin mostrarla en pantalla ni en logs**.

### Opción B: Configuración manual

```powershell
cd backend/ErpModa.Api
dotnet user-secrets init   # solo la primera vez
```

Luego, con tu contraseña real en lugar de `<DB_PASSWORD>`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=aws-0-sa-east-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.gcqyxbdifutrbbdouzlk;Password=<DB_PASSWORD>;SSL Mode=Require"
```

> **Sustituye `<DB_PASSWORD>` directamente en la línea del terminal.**
> No lo copies a ningún archivo.

### Verificar que la clave está configurada (sin revelar el valor)

> [!WARNING]
> **No uses `dotnet user-secrets list`**: ese comando imprime las cadenas de conexión completas incluyendo contraseñas en texto plano, con riesgo de quedar expuestas en logs, terminal o capturas.

Para verificar que la clave está registrada sin exponer el valor:
- **Recomendado:** Ejecuta `.\setup-user-secrets.ps1`, el cual verifica internamente `secrets.json` y solo reporta si la clave está registrada o ausente.
- **Vía PowerShell:** Comprueba la existencia del archivo de secretos de forma no intrusiva:
  ```powershell
  $id = (Select-String -Path ErpModa.Api.csproj -Pattern '<UserSecretsId>(.*?)</UserSecretsId>').Matches.Groups[1].Value
  Test-Path "$env:APPDATA\Microsoft\UserSecrets\$id\secrets.json"
  ```

---

## 2. Verificar la configuración de appsettings.json

El archivo `appsettings.json` debe tener la clave vacía (el secreto la sobreescribe en runtime):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

**No debe contener ninguna contraseña ni token real.**

---

## 3. Aplicar scripts SQL en Supabase

Antes de ejecutar la aplicación, crea las tablas en tu proyecto Supabase:

1. Abre **Supabase Dashboard → SQL Editor**
2. Ejecuta `docs/inventario/schema.sql`
3. Ejecuta `docs/ventas/schema.sql`
4. Ejecuta `docs/seguridad/usuarios-schema.sql` (tabla `usuarios`, necesaria para la autenticación JWT — ver sección 8)

---

## 4. Restaurar y compilar

```powershell
cd backend/ErpModa.Api
dotnet restore
dotnet build
```

---

## 5. Ejecutar la aplicación

```powershell
dotnet run
```

---

## 6. Verificar health check

```
GET http://localhost:5000/health
```

- `200 OK` → conexión exitosa
- `503 Service Unavailable` → fallo de conexión (revisar User Secrets y credenciales)

---

## 7. Pruebas CRUD recomendadas

### Inventario
| Método | Endpoint | Acción |
|---|---|---|
| GET | `/api/Productos` | Listar productos |
| POST | `/api/Productos` | Crear producto con variantes |
| GET | `/api/Productos/{id}` | Obtener producto |
| PUT | `/api/Productos/{id}` | Actualizar producto |
| DELETE | `/api/Productos/{id}` | Eliminar producto |

### Ventas
| Método | Endpoint | Acción |
|---|---|---|
| GET | `/api/Ventas` | Listar ventas |
| POST | `/api/Ventas` | Crear venta |
| GET | `/api/Ventas/{id}` | Obtener venta |
| POST | `/api/Ventas/{id}/confirmar` | Confirmar (genera factura) |
| POST | `/api/Ventas/{id}/anular` | Anular venta |
| GET | `/api/Ventas/resumen` | Resumen de ventas |

---

## 8. Autenticación JWT y primer administrador (issue #82)

La API **no guarda secretos en el repositorio**. `appsettings.json` solo contiene el
emisor (`Jwt:Issuer`), la audiencia (`Jwt:Audience`) y la duración del token
(`Jwt:DuracionMinutos`, 60). La clave de firma y el token de arranque se entregan
por configuración externa.

### 8.1 Desarrollo: User Secrets

Desde `backend/ErpModa.Api`:

```powershell
dotnet user-secrets init   # solo la primera vez

# Genera VALORES NUEVOS Y ALEATORIOS. No los copies a un archivo ni los compartas.
$jwtKey = [Convert]::ToBase64String([byte[]](1..32 | ForEach-Object { Get-Random -Max 256 }))
dotnet user-secrets set "Jwt:Key" $jwtKey

$bootstrap = [Convert]::ToBase64String([byte[]](1..32 | ForEach-Object { Get-Random -Max 256 }))
dotnet user-secrets set "Auth:BootstrapToken" $bootstrap
```

`Jwt:Key` debe tener **al menos 32 caracteres** (el backend lo valida al arrancar).
En Development, si no la configuras, se genera una clave efífera solo en memoria y
se registra un aviso: los tokens dejan de ser válidos al reiniciar. Eso **no es
válido para producción**.

> No ejecutes `dotnet user-secrets list`: imprime los valores en claro. Para saber
> si están registrados, comprueba únicamente que existe `secrets.json` (sección 1).

### 8.2 Producción: variables de entorno

| Variable de entorno | Clave de configuración | Uso |
|---|---|---|
| `Jwt__Key` | `Jwt:Key` | Clave de firma HMAC-SHA256 (≥32 caracteres). **Obligatoria**: si falta, la aplicación no arranca en `Production`. |
| `Auth__BootstrapToken` | `Auth:BootstrapToken` | Token de arranque para `/api/auth/inicializar`. Si falta, el endpoint responde `503`. |

Ejemplo (sustituye los marcadores por valores generados **en el servidor**, nunca
por secretos reales pegados en el repositorio):

```bash
export Jwt__Key='<CLAVE_ALEATORIA_GENERADA_EN_EL_SERVIDOR>'
export Auth__BootstrapToken='<TOKEN_ALEATORIO_GENERADO_EN_EL_SERVIDOR>'
```

En Docker Compose, inyéctalas en el `environment` del servicio backend desde el
entorno del despliegue o desde un `.env` local excluido por `.gitignore`; **nunca**
en un `docker-compose.yml` versionado.

### 8.3 Aplicar el esquema de usuarios

El proyecto usa **scripts SQL manuales**: no hay migraciones EF Core y la
aplicación no crea tablas al arrancar.

```powershell
# Local con Docker Compose
docker compose exec -T db psql -U erpuser -d erp_moda -v ON_ERROR_STOP=1 < docs/seguridad/usuarios-schema.sql
```

En Supabase: **SQL Editor** → pega y ejecuta `docs/seguridad/usuarios-schema.sql`.
El script es idempotente (`CREATE TABLE IF NOT EXISTS` / `CREATE UNIQUE INDEX IF NOT EXISTS`).

### 8.4 Crear el primer administrador

1. Arranca la API y verifica `GET /health` → `200 OK`.
2. Configura `Auth:BootstrapToken` como se indica en 8.1 u 8.2.
   Sin él, `/api/auth/inicializar` devuelve `503`; con un valor distinto, `401`.
3. Envía el token en la cabecera `X-Bootstrap-Token`:

```bash
curl -i -X POST http://localhost:5000/api/auth/inicializar \
  -H "Content-Type: application/json" \
  -H "X-Bootstrap-Token: <TOKEN_DE_ARRANQUE>" \
  -d '{"email":"admin@tu-dominio.com","password":"<CONTRASEÑA_LARGA_Y_ALEATORIA>"}'
```

- `200 OK` → responde `id`, `email` y `rol: "Administrador"` (nunca el hash BCrypt).
- `401` → token de arranque no válido.
- `409` → ya existe al menos un usuario: el arranque queda deshabilitado.

> Solo puede completarse **una** inicialización. En PostgreSQL la comprobación y
> el `INSERT` están bajo `pg_advisory_xact_lock` dentro de la misma transacción,
> así que dos peticiones simultáneas no crean dos administradores.

4. Inicia sesión y usa el JWT para crear el resto de usuarios:

```bash
curl -i -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@tu-dominio.com","password":"<CONTRASEÑA_LARGA_Y_ALEATORIA>"}'

curl -i -X POST http://localhost:5000/api/auth/registro \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <JWT_DEVUELTO_POR_LOGIN>" \
  -d '{"email":"vendedor@tu-dominio.com","password":"<OTRA_CONTRASEÑA>","rol":1}'
```

Roles: `0 = Administrador`, `1 = Vendedor`, `2 = Almacenista`. El JWT dura 60
minutos; se validan firma, emisor, audiencia y expiración. Un endpoint protegido
devuelve `401` sin token y `403` si el rol carece de permiso.

---

## Notas importantes

- La connection string **nunca se guarda en el repositorio** (User Secrets es local al equipo)
- Para **producción**, configura la connection string en las variables de entorno del servidor
- La aplicación **no ejecuta migraciones automáticamente** al iniciar
- Las tablas se crean manualmente con los scripts SQL de Supabase

---

## Solución de problemas

### Error: `Failed to connect to database`
- Verifica que User Secrets esté configurado ejecutando `.\setup-user-secrets.ps1` o comprobando que `secrets.json` existe (no uses `dotnet user-secrets list` para no exponer la contraseña)
- Verifica que las credenciales de Supabase sean correctas

### Error: `relation does not exist`
- Aplica los scripts SQL en Supabase antes de ejecutar la aplicación

### Error: `SSL connection required`
- Verifica que la connection string incluya `SSL Mode=Require`

### Error: `certificate validation failed` (solo desarrollo local)
- Este error suele aparecer en entornos con proxies o certificados corporativos (**no** con Supabase, que sirve certificados TLS válidos)
- **Únicamente en desarrollo local y como último recurso**, puedes usar `Trust Server Certificate=true` en un **override exclusivo de desarrollo** (por ejemplo, una connection string definida en `appsettings.Development.json` o un secreto local de desarrollo). Nunca en la connection string compartida ni en la configuración de Supabase
- **Nunca uses `Trust Server Certificate=true` en producción, en `docker-compose.yml` ni en configuración versionada/compartida**
- La alternativa correcta es configurar el certificado raíz de CA en tu entorno local
