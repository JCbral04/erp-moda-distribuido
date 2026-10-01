# Configuración de User Secrets para Supabase

## Comandos para configurar User Secrets

### Paso 1: Inicializar User Secrets (solo la primera vez)

```powershell
cd backend/ErpModa.Api
dotnet user-secrets init
```

### Paso 2: Configurar la Connection String

Ejecuta el siguiente comando **reemplazando `<DB_PASSWORD>` con tu contraseña real de Supabase**
(la contraseña NO debe escribirse en ningún archivo del repositorio):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=aws-0-sa-east-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.gcqyxbdifutrbbdouzlk;Password=<DB_PASSWORD>;SSL Mode=Require"
```

> **IMPORTANTE:**
> - Sustituye `<DB_PASSWORD>` por tu contraseña real **en la línea del terminal**, no en este archivo.
> - El valor nunca debe aparecer en el repositorio.

### Verificar que la clave existe (sin mostrar el valor)

`dotnet user-secrets list` imprime los **valores completos** (incluida la connection string con la contraseña envuelta en `Password=`). Para confirmar **solo que la clave existe** sin revelar nada, usa:

```powershell
dotnet user-secrets list 2>$null | Select-String -Quiet "ConnectionStrings:DefaultConnection"
```

La salida es únicamente `True` (configurada) o `False` (no configurada). Nunca muestra el valor.

> **No ejecutes `dotnet user-secrets list` a secas ni con `Select-String` sin `-Quiet`:** su salida completa puede quedar registrada en logs, historial del terminal o capturas de pantalla.

## Método recomendado: script PowerShell

En lugar de los comandos manuales, usa el script interactivo que solicita la contraseña de forma segura y **no la muestra en pantalla**:

```powershell
cd backend/ErpModa.Api
.\setup-user-secrets.ps1
```

El script:
1. Verifica el directorio correcto
2. Solicita la contraseña con `Read-Host -AsSecureString` (no se muestra en pantalla)
3. Registra el secreto sin imprimirlo
4. Confirma únicamente que la clave `ConnectionStrings:DefaultConnection` existe

## Parámetros de conexión Supabase

| Parámetro | Valor |
|---|---|
| Host | `aws-0-sa-east-1.pooler.supabase.com` |
| Port | `5432` |
| Database | `postgres` |
| Username | `postgres.gcqyxbdifutrbbdouzlk` |
| Password | **Configurar localmente vía User Secrets** |
| SSL Mode | `Require` |

> **Nota sobre SSL:** No uses `Trust Server Certificate=true` en producción.
> Supabase provee certificados válidos — `SSL Mode=Require` es suficiente y seguro.
> Si enfrentas errores de certificado en desarrollo local, consulta la sección de solución de problemas en `SETUP_INSTRUCTIONS.md`.
