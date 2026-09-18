# =============================================================
# setup-user-secrets.ps1
# Configura dotnet user-secrets para ErpModa.Api
#
# Uso: Ejecutar desde PowerShell en la carpeta backend/ErpModa.Api
#   .\setup-user-secrets.ps1
#
# SEGURIDAD:
#   - La contraseña se solicita con Read-Host -AsSecureString (no visible en pantalla)
#   - La contraseña NO se imprime después de configurarse
#   - Solo se confirma que la clave existe, sin mostrar el valor
# =============================================================

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Verificar directorio ───────────────────────────────────────
$csproj = "ErpModa.Api.csproj"
if (-not (Test-Path $csproj)) {
    Write-Host "ERROR: Debes ejecutar este script desde el directorio backend\ErpModa.Api" -ForegroundColor Red
    Write-Host "  Directorio actual: $(Get-Location)" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "=== Configuración de User Secrets para ErpModa.Api ===" -ForegroundColor Cyan
Write-Host ""

# ── Verificar que ya existe el UserSecretsId en el .csproj ────
$csprojContent = Get-Content $csproj -Raw
if ($csprojContent -notmatch "<UserSecretsId>") {
    Write-Host "Inicializando User Secrets..." -ForegroundColor Yellow
    dotnet user-secrets init
} else {
    Write-Host "✅ UserSecretsId ya existe en el proyecto." -ForegroundColor Green
}

# ── Solicitar contraseña de forma segura ──────────────────────
Write-Host ""
Write-Host "Ingresa la contraseña de Supabase (no se mostrará en pantalla):" -ForegroundColor Yellow
$password = Read-Host -AsSecureString "Contraseña"
$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
try {
    $passwordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
} finally {
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

if ([string]::IsNullOrWhiteSpace($passwordPlain)) {
    Write-Host "ERROR: La contraseña no puede estar vacía." -ForegroundColor Red
    exit 1
}

# ── Construir y registrar la connection string ────────────────
# NOTA DE SEGURIDAD:
#   - SSL Mode=Require garantiza cifrado TLS con Supabase.
#   - NO se usa Trust Server Certificate=true; Supabase provee
#     certificados válidos y no debe desactivarse la validación.
$connectionString = "Host=aws-0-sa-east-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.gcqyxbdifutrbbdouzlk;Password=$passwordPlain;SSL Mode=Require"

Write-Host ""
Write-Host "Registrando ConnectionStrings:DefaultConnection en User Secrets..." -ForegroundColor Cyan

# Redirigir TODA la salida a Out-Null: `dotnet user-secrets set` imprime la
# connection string completa (incluida la contraseña) si no se captura.
# El resultado se evalúa solo mediante $LASTEXITCODE; no se conserva el valor.
dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connectionString 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR al registrar el secreto (código $LASTEXITCODE). Revisa la conexión a Supabase y vuelve a intentarlo." -ForegroundColor Red
    exit 1
}

# Limpiar la contraseña de variables en memoria
$password = $null
$passwordPlain = $null
$connectionString = $null
[System.GC]::Collect()

# ── Verificar que la clave existe (SIN mostrar el valor) ──────
# NO se usa `dotnet user-secrets list`: ese comando imprime los valores
# completos si no se filtra con -Quiet. Se verifica directamente en
# secrets.json que la clave exista, sin leer su valor.
Write-Host ""
Write-Host "Verificando que la clave está registrada..." -ForegroundColor Cyan

$userSecretsIdMatch = [regex]::Match($csprojContent, '<UserSecretsId>(.*?)</UserSecretsId>')
if (-not $userSecretsIdMatch.Success) {
    Write-Host "⚠️  No se encontró UserSecretsId en el .csproj." -ForegroundColor Yellow
    exit 1
}

$secretsJson = Join-Path $env:APPDATA "Microsoft\UserSecrets\$($userSecretsIdMatch.Groups[1].Value)\secrets.json"
$keyExists = $false
if (Test-Path $secretsJson) {
    $secrets = Get-Content $secretsJson -Raw | ConvertFrom-Json
    $keyExists = $null -ne $secrets.'ConnectionStrings:DefaultConnection'
}

if ($keyExists) {
    Write-Host "✅ ConnectionStrings:DefaultConnection está registrada en User Secrets (valor no mostrado)." -ForegroundColor Green
} else {
    Write-Host "⚠️  No se pudo confirmar el registro en $secretsJson" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✅ Configuración completada." -ForegroundColor Green
Write-Host ""
Write-Host "La contraseña se almacena localmente en:" -ForegroundColor Yellow
Write-Host "  %APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json" -ForegroundColor White
Write-Host "  Este archivo está FUERA del repositorio y nunca se commitea." -ForegroundColor Green
Write-Host ""
Write-Host "Pasos siguientes:" -ForegroundColor Yellow
Write-Host "  dotnet restore" -ForegroundColor White
Write-Host "  dotnet build" -ForegroundColor White
Write-Host "  dotnet run" -ForegroundColor White
