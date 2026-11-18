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

if [ -z "${PGHOST:-}" ] || [ -z "${PGUSER:-}" ] || [ -z "${PGPASSWORD:-}" ] || [ -z "${PGDATABASE:-}" ]; then
  echo "ERROR: define PGHOST, PGUSER, PGPASSWORD y PGDATABASE (variables estandar de libpq)." >&2
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
docker run --rm -e PGCONNECT_TIMEOUT=15 "${net_args[@]}" \
  -e PGHOST -e PGPORT -e PGUSER -e PGPASSWORD -e PGDATABASE -e PGSSLMODE \
  postgres:17-alpine \
  pg_dump -n public --no-owner --no-privileges --clean --if-exists \
  | gzip > "$OUTPUT"

# Verificaciones: gzip integro y dump con esquema.
gzip -t "$OUTPUT" 2>/dev/null || { echo "ERROR: archivo corrupto" >&2; exit 1; }
CREATE_TABLES=$(zcat "$OUTPUT" | grep -c "CREATE TABLE")
if [ "$CREATE_TABLES" -eq 0 ]; then
  echo "ERROR: dump sin esquema (revisa credenciales)" >&2
  exit 1
fi

# Cifrado: obligatorio en CI (GPG_PASSPHRASE desde secret), opcional en local.
if [ -n "${GPG_PASSPHRASE:-}" ]; then
  echo "Cifrando con GPG (AES256)..."
  # La passphrase entra por stdin (fd 0), jamas como argumento de proceso:
  # como argumento seria visible con ps (review #2 del PR #97).
  printf '%s' "$GPG_PASSPHRASE" | gpg --batch --yes --symmetric \
      --cipher-algo AES256 --passphrase-fd 0 -o "$OUTPUT.gpg" "$OUTPUT"
  rm -f "$OUTPUT"
  OUTPUT="$OUTPUT.gpg"
fi

echo "OK: $(du -h "$OUTPUT" | cut -f1) -> $OUTPUT"