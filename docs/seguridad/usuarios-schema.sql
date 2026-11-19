-- Esquema de base de datos -- Modulo de Autenticacion (issues #59/#82)
-- PostgreSQL. Script de creacion manual alineado con la entidad Usuario tal como
-- la configura ErpModaDbContext (OnModelCreating). El proyecto NO usa migraciones
-- EF Core: la tabla se crea ejecutando este script a mano (ver SETUP_INSTRUCTIONS.md).
-- No se aplica automaticamente. Aplicar manualmente, p.ej.:
--   docker compose exec -T db psql -U erpuser -d erp_moda < docs/seguridad/usuarios-schema.sql
--
-- Seguridad:
--   - password_hash guarda el hash BCrypt (nunca la contraseña en texto plano).
--   - email se normaliza en la aplicacion (minúsculas); el indice unico evita
--     cuentas duplicadas por diferencias de mayúsculas/minúsculas.
--   - rol usa los valores exactos del enum RolUsuario.

BEGIN;

CREATE TABLE IF NOT EXISTS usuarios (
    id              SERIAL        PRIMARY KEY,
    email           VARCHAR(256)  NOT NULL,
    password_hash   VARCHAR(255)  NOT NULL,
    rol             VARCHAR(20)   NOT NULL
        CHECK (rol IN ('Administrador', 'Vendedor', 'Almacenista')),
    creado_en       TIMESTAMPTZ   NOT NULL DEFAULT now(),
    actualizado_en  TIMESTAMPTZ   NOT NULL DEFAULT now()
);

-- La unicidad se declara como indice, igual que HasIndex(...).IsUnique() de EF.
CREATE UNIQUE INDEX IF NOT EXISTS ux_usuarios_email ON usuarios (email);

COMMIT;
