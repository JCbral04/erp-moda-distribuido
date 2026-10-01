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

### Verificar que la clave existe (sin revelar el valor)

`dotnet user-secrets list` imprime los **valores completos** (incluida la connection string con la contraseña). Para confirmar **solo la existencia** de la clave sin mostrar nada:

```powershell
dotnet user-secrets list 2>$null | Select-String -Quiet "ConnectionStrings:DefaultConnection"
```

La salida es únicamente `True`/`False`. **No ejecutes `dotnet user-secrets list` sin `-Quiet`**
en entornos donde la salida pueda quedar en capturas, historial del terminal o logs de CI.

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

## Notas importantes

- La connection string **nunca se guarda en el repositorio** (User Secrets es local al equipo)
- Para **producción**, configura la connection string en las variables de entorno del servidor
- La aplicación **no ejecuta migraciones automáticamente** al iniciar
- Las tablas se crean manualmente con los scripts SQL de Supabase

---

## Solución de problemas

### Error: `Failed to connect to database`
- Verifica que User Secrets esté configurado (`dotnet user-secrets list 2>$null | Select-String -Quiet "ConnectionStrings:DefaultConnection"` → debe imprimir `True`)
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
