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
   - `psql`/`pg_dump` (script de backup): variables estándar de libpq
     (`PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `PGDATABASE`, `PGSSLMODE`),
     no una URI única guardada en un secreto.
4. **Red:** la conexión directa (puerto 5432, host `db.&lt;ref&gt;.supabase.co`) usa
   solo IPv6. Sin IPv6 local usar siempre el **Transaction pooler** (6543).
5. **El dump debe limitarse al schema `public`** (`-n public`): sin el
   filtro, pg_dump incluye schemas internos de Supabase (`auth`, `storage`,
   `supabase_vault`, ...) que no existen en un Postgres vanilla y la
   restauración falla con "extension is not available".

## Secretos obligatorios en GitHub Actions

El workflow `.github/workflows/backup.yml` usa credenciales independientes
(no una única URI). Los secretos se llaman exactamente así y los seis son obligatorios tanto para
la ejecución programada (domingo 03:00 UTC) como para el `workflow_dispatch`:

| Secreto del repositorio | Variable de entorno que inyecta el workflow | Para qué sirve |
| :--- | :--- | :--- |
| `SUPABASE_DB_HOST` | `PGHOST` | Host del servidor/pooler de Supabase |
| `SUPABASE_DB_PORT` | `PGPORT` | Puerto (el del pooler si no hay IPv6) |
| `SUPABASE_DB_USER` | `PGUSER` | Usuario de la base de datos |
| `SUPABASE_DB_PASSWORD` | `PGPASSWORD` | Contraseña de ese usuario |
| `SUPABASE_DB_NAME` | `PGDATABASE` | Nombre de la base de datos |
| `BACKUP_GPG_PASSPHRASE` | `GPG_PASSPHRASE` | Cifrado del dump y verificación del artifact |

`PGSSLMODE` no es un secreto: el workflow lo fija a `require`. No se definen
valores por defecto: toma cada dato de tu proyecto Supabase y elige tu propia
passphrase. No inventes ni publiques credenciales; los secretos no se pueden
consultar después de guardarlos.

Para configurarlos:

1. En el repositorio: **Settings → Secrets and variables → Actions → New repository secret**.
2. Crea un secreto por cada nombre de la tabla anterior, con el valor exacto
   (sin comillas, sin `export`, sin URI completa).
3. El workflow los pasa como variables de entorno; el script no los recibe
   como argumentos (visibles con `ps`) y GPG usa `--passphrase-fd 0`.
4. Si falta algun secreto de conexión, `backup-db.sh` termina con error; si
   falta la passphrase, el artifact no supera la verificación de cifrado.
   En ambos casos **no se publica un backup sin cifrar**.

El artifact se llama `backup-<run_id>` y contiene únicamente
`backup.sql.gz.gpg`, con retención de 30 días.

## Backup manual

Con las mismas variables de entorno (sin credenciales reales en este ejemplo):

```bash
PGHOST='<valor-de-SUPABASE_DB_HOST>' \
PGPORT='<valor-de-SUPABASE_DB_PORT>' \
PGUSER='<valor-de-SUPABASE_DB_USER>' \
PGPASSWORD='<valor-de-SUPABASE_DB_PASSWORD>' \
PGDATABASE='<valor-de-SUPABASE_DB_NAME>' \
PGSSLMODE=require \
GPG_PASSPHRASE='<valor-de-BACKUP_GPG_PASSPHRASE>' \
  ./scripts/backup-db.sh backup.sql.gz
# Descifrar (prompt interactivo, sin passphrase en el comando):
gpg --decrypt backup.sql.gz.gpg > backup.sql.gz
```

Política: **sin `GPG_PASSPHRASE` el script genera un backup local sin cifrar**
(útil fuera de CI); en GitHub Actions la passphrase siempre está presente y
el workflow exige el `.gpg` y comprueba que no exista el archivo plano antes
de subir el artifact.

Verificaciones del script: gzip íntegro y al menos una tabla en el dump. Si
falla docker, pg_dump, gzip o GPG, elimina solo los archivos temporales de esa
ejecución y no borra backups anteriores.

## Restauración: tres escenarios distintos

### A) Reconstrucción del esquema (NO recupera datos)

Reaplicar `docs/*/schema.sql` desde SQL Editor crea la **estructura vacía**
(tablas, índices y restricciones), pero **no restaura ninguna fila del
backup**. Es útil solo para volver a tener el esquema en una base vacía; no
sustituye a la restauración del dump. No lo ejecutes sobre una base con datos
sin revisar antes qué objetos ya existen.

### B) Restauración completa de datos en Supabase (ruta probada)

1. Descarga el artifact `.gpg` y descifra sin poner la passphrase en la línea
   de comandos (se solicitará por prompt):

   ```bash
   gpg --decrypt backup.sql.gz.gpg > backup.sql.gz
   gzip -t backup.sql.gz
   ```

2. **ADVERTENCIA DESTRUCTIVA:** el dump se generó con
   `--clean --if-exists`, por lo que contiene sentencias `DROP ... IF EXISTS`
   de los objetos del schema `public`. Restaurarlo sobre una base que ya tiene
   datos **elimina esos objetos y su contenido**. Haz antes un backup del
   estado actual y restaura preferiblemente en una base vacía.

3. Restaura con `psql` (cliente ≥ 17), usando las mismas variables de entorno
   de libpq que el workflow, con valores de tu propio secret store:

   ```bash
   export PGHOST='<valor-de-SUPABASE_DB_HOST>'
   export PGPORT='<valor-de-SUPABASE_DB_PORT>'
   export PGUSER='<valor-de-SUPABASE_DB_USER>'
   export PGPASSWORD='<valor-de-SUPABASE_DB_PASSWORD>'
   export PGDATABASE='<valor-de-SUPABASE_DB_NAME>'
   export PGSSLMODE=require

   gunzip -c backup.sql.gz | psql -v ON_ERROR_STOP=1
   ```

   `psql` sin `-d` usa `PGDATABASE`. El dump ya viene limitado a `-n public`,
   `--no-owner` y `--no-privileges`.

   **No asumas que SQL Editor acepta cualquier dump generado por `pg_dump`**:
   puede rechazar sentencias de meta-comando o dumps grandes. Compruébalo con
   un dump de prueba antes de depender de esa vía; la ruta verificada es `psql`.

4. Verifica tablas y datos (esperado: 7 tablas y la fila del contador):

   ```bash
   psql -c '\dt' -c "SELECT * FROM contador_factura;" \
     -c "SELECT count(*) AS ventas FROM ventas;" \
     -c "SELECT count(*) AS productos FROM productos;"
   ```

### C) Restauración en un PostgreSQL 17 local

```bash
# 1. Base limpia (ADVERTENCIA: se crea un contenedor nuevo; no reutilices
#    uno con datos que quieras conservar)
docker run -d --name restore -e POSTGRES_PASSWORD=testpass postgres:17-alpine
until docker exec restore pg_isready -U postgres > /dev/null 2>&1; do sleep 1; done

# 2. Restaurar (ON_ERROR_STOP para no ignorar errores parciales)
gunzip -c backup.sql.gz | docker exec -i restore psql -U postgres -d postgres -v ON_ERROR_STOP=1

# 3. Verificar (esperado: 7 tablas + fila del contador de facturas + filas)
docker exec restore psql -U postgres -d postgres -c '\dt' \
  -c "SELECT * FROM contador_factura;" \
  -c "SELECT count(*) AS ventas FROM ventas;" \
  -c "SELECT count(*) AS productos FROM productos;"
```
