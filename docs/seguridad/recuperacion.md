# Recuperación de la base de datos — erp-moda-distribuido

Issue #63. Procedimiento de backup y restauración probado el 2026-11-14
después de que la pausa del proyecto en Supabase (plan FREE, sin backups
automáticos) borrara los datos.

## Advertencias previas (aprendidas en la recuperación real)

1. **Plan FREE de Supabase: la pausa elimina la BD.** No hay backups
   automáticos. Por eso existe el backup semanal del workflow `backup-db`.
2. **Supabase corre PostgreSQL 17; el compose local usa 16.** El dump es SQL
   estándar y restaura en ambos, pero el cliente `pg_dump` debe ser ≥ 17
   (`postgres:17-alpine` en el script). Un cliente 16 contra servidor 17
   falla con "server version mismatch".
3. **Dos formatos de connection string, no confundirlos:**
   - App/EF Core (User Secrets): formato Npgsql
     `Host=...;Port=6543;Database=postgres;Username=postgres.&lt;ref&gt;;Password=...;SSL Mode=Require`
   - `psql`/`pg_dump` (script de backup): URI
     `postgresql://postgres.&lt;ref&gt;:&lt;pass&gt;@aws-0-sa-east-1.pooler.supabase.com:6543/postgres?sslmode=require`
4. **Red:** la conexión directa (puerto 5432, host `db.&lt;ref&gt;.supabase.co`) usa
   solo IPv6. Sin IPv6 local usar siempre el **Transaction pooler** (6543).
5. **El dump debe limitarse al schema `public`** (`-n public`): sin el
   filtro, pg_dump incluye schemas internos de Supabase (`auth`, `storage`,
   `supabase_vault`, ...) que no existen en un Postgres vanilla y la
   restauración falla con "extension is not available".

## Backup manual

```bash
PGHOST=tu-host PGPORT=5432 PGUSER=usuario PGPASSWORD=*** PGDATABASE=postgres PGSSLMODE=require \
  GPG_PASSPHRASE='tu-passphrase' ./scripts/backup-db.sh backup.sql.gz
# Descifrar (prompt interactivo, sin passphrase en el comando):
gpg --decrypt backup.sql.gz.gpg > backup.sql.gz
```

Verificaciones del script: gzip íntegro + al menos una tabla en el dump.

## Backup automático

Workflow `.github/workflows/backup.yml`: semanal (domingo 03:00 UTC) +
manual (`workflow_dispatch`). Requiere el secret `SUPABASE_DATABASE_URL` con
la URI del pooler. Artifact retenido 30 días.

## Restauración (probada en contenedor limpio)

```bash
# 1. Contenedor limpio
docker run -d --name restore -e POSTGRES_PASSWORD=testpass postgres:17-alpine
until docker exec restore pg_isready -U postgres &gt; /dev/null 2&gt;&1; do sleep 1; done
# 2. Restaurar
gunzip -c backup.sql.gz | docker exec -i restore psql -U postgres -d postgres -v ON_ERROR_STOP=1
# 3. Verificar (esperado: 7 tablas + fila del contador de facturas)
docker exec restore psql -U postgres -d postgres -c '\dt' -c "SELECT * FROM contador_factura;"
```

Para restaurar **en Supabase**: desde SQL Editor del dashboard, ejecutar el
dump descomprimido (o reaplicar los scripts `docs/*/schema.sql` si la BD
quedó vacía — es la ruta corta y por eso los esquemas viven en el repo).