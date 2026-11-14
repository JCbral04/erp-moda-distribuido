#!/usr/bin/env bash
# backup-db.sh — Backup de PostgreSQL con pg_dump (issue #63).
# Uso: ./scripts/backup-db.sh [archivo_salida.sql.gz]
# Requiere la variable DATABASE_URL en formato URI:
#   postgresql://usuario:password@host:puerto/base?sslmode=require
# En CI se inyecta como secret de GitHub Actions.
set -euo pipefail

OUTPUT="${1:-backup-$(date +%Y%m%d-%H%M%S).sql.gz}"
URL="${DATABASE_URL:-}"

if [ -z "$URL" ]; then
  echo "ERROR: define DATABASE_URL (URI postgresql://...)" >&2
  exit 1
fi

echo "Generando backup: $OUTPUT"
# pg_dump dentro del contenedor oficial de PostgreSQL 17.
# --no-owner/--no-privileges: el backup es portable a otro servidor/Supabase.
# --clean --if-exists: permite restaurar sobre una BD con datos (idempotente).
docker run --rm -e PGCONNECT_TIMEOUT=15 postgres:17-alpine \
  pg_dump -n public --no-owner --no-privileges --clean --if-exists "$URL" \
  | gzip > "$OUTPUT"

# Verificaciones: gzip integro y dump con esquema.
gzip -t "$OUTPUT" 2>/dev/null || { echo "ERROR: archivo corrupto" >&2; exit 1; }
CREATE_TABLES=$(zcat "$OUTPUT" | grep -c "CREATE TABLE")
if [ "$CREATE_TABLES" -eq 0 ]; then
  echo "ERROR: dump sin esquema (revisa credenciales)" >&2
  exit 1
fi

echo "OK: $(du -h "$OUTPUT" | cut -f1) -> $OUTPUT"