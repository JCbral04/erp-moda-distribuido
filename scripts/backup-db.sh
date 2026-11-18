#!/usr/bin/env bash
# backup-db.sh — Backup de PostgreSQL con pg_dump (issue #63).
# Uso: ./scripts/backup-db.sh [archivo_salida.sql.gz[.gpg]]
#
# Credenciales SOLO por variables de entorno estandar de libpq
# (PGHOST/PGPORT/PGUSER/PGPASSWORD/PGDATABASE/PGSSLMODE). Jamas por argumentos:
# los argumentos de proceso son visibles con ps.
#
# Si GPG_PASSPHRASE esta definida, el dump se cifra con GPG (AES256) y el
# archivo plano se elimina: el artifact de GitHub solo contiene datos cifrados.
set -euo pipefail

OUTPUT="${1:-backup-$(date +%Y%m%d-%H%M%S).sql.gz}"

# Archivos temporales de ESTA ejecucion (mktemp: nombre unico). El trap solo
# los elimina a ellos; nunca toca $OUTPUT ni $OUTPUT.gpg, que pueden ser
# backups anteriores. Asi un fallo de GPG (o de docker/pg_dump/gzip) no deja
# un dump plano abandonado en el directorio de trabajo.
TMP_PLAIN=""
TMP_ENC=""

cleanup() {
  local tmp
  for tmp in "$TMP_PLAIN" "$TMP_ENC"; do
    if [ -n "$tmp" ] && [ -f "$tmp" ]; then
      rm -f -- "$tmp"
    fi
  done
}
trap cleanup EXIT

if [ -z "${PGHOST:-}" ] || [ -z "${PGPORT:-}" ] || [ -z "${PGUSER:-}" ] || [ -z "${PGPASSWORD:-}" ] || [ -z "${PGDATABASE:-}" ]; then
  echo "ERROR: define PGHOST, PGPORT, PGUSER, PGPASSWORD y PGDATABASE (variables estandar de libpq)." >&2
  exit 1
fi

echo "Generando backup: $OUTPUT"
# --no-owner/--no-privileges: backup portable a otro servidor/Supabase.
# --clean --if-exists: el dump incluye DROP ... IF EXISTS de cada objeto
# public antes de recrearlo. OJO: restaurar sobre una base con datos
# ELIMINA sus objetos actuales. Usar solo sobre base vacia o aceptando
# la perdida. Ver docs/seguridad/recuperacion.md.
# BACKUP_DOCKER_NETWORK (opcional): red Docker donde resolver PGHOST,
# necesario cuando PGHOST es otro contenedor local (pruebas sin credenciales
# reales). Contra Supabase u host publico no hace falta.
net_args=()
if [ -n "${BACKUP_DOCKER_NETWORK:-}" ]; then
  net_args=(--network "$BACKUP_DOCKER_NETWORK")
fi
TMP_PLAIN=$(mktemp "${OUTPUT}.tmp.XXXXXX")
# Con pipefail, un fallo de docker, pg_dump o gzip aborta y el trap limpia
# el temporal parcial; no se publica un archivo incompleto.
docker run --rm -e PGCONNECT_TIMEOUT=15 "${net_args[@]}" \
  -e PGHOST -e PGPORT -e PGUSER -e PGPASSWORD -e PGDATABASE -e PGSSLMODE \
  postgres:17-alpine \
  pg_dump -n public --no-owner --no-privileges --clean --if-exists \
  | gzip > "$TMP_PLAIN"

# Verificaciones: gzip integro y dump con esquema.
gzip -t "$TMP_PLAIN" 2>/dev/null || { echo "ERROR: archivo corrupto" >&2; exit 1; }
# grep -c devuelve 1 cuando no hay coincidencias: con set -e eso abortaria
# antes del mensaje y sin la comprobacion explicita.
CREATE_TABLES=$(zcat "$TMP_PLAIN" | grep -c "CREATE TABLE" || true)
if [ "$CREATE_TABLES" -eq 0 ]; then
  echo "ERROR: dump sin esquema (revisa credenciales)" >&2
  exit 1
fi

# Cifrado: obligatorio en CI (GPG_PASSPHRASE desde secret), opcional en local.
if [ -n "${GPG_PASSPHRASE:-}" ]; then
  echo "Cifrando con GPG (AES256)..."
  # La passphrase entra por stdin (fd 0), jamas como argumento de proceso:
  # como argumento seria visible con ps (review #2 del PR #97).
  TMP_ENC=$(mktemp "${OUTPUT}.gpg.tmp.XXXXXX")
  if ! printf '%s' "$GPG_PASSPHRASE" | gpg --batch --yes --symmetric \
      --cipher-algo AES256 --passphrase-fd 0 -o "$TMP_ENC" "$TMP_PLAIN"; then
    echo "ERROR: cifrado GPG fallido; dump plano temporal eliminado" >&2
    # El trap EXIT elimina TMP_PLAIN y TMP_ENC. No se toca ningun backup anterior.
    exit 1
  fi
  mv -f -- "$TMP_ENC" "$OUTPUT.gpg"
  TMP_ENC=""
  rm -f -- "$TMP_PLAIN"
  TMP_PLAIN=""
  OUTPUT="$OUTPUT.gpg"
else
  # Politica local: sin passphrase se conserva el backup sin cifrar.
  mv -f -- "$TMP_PLAIN" "$OUTPUT"
  TMP_PLAIN=""
fi

echo "OK: $(du -h "$OUTPUT" | cut -f1) -> $OUTPUT"
