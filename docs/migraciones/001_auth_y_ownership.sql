-- Actualizacion incremental para bases ya inicializadas con los scripts SQL
-- oficiales (issues #59/#82/#83). Seguro para ejecutar mas de una vez:
--   - no elimina columnas, filas ni tablas;
--   - añade solo lo que falta;
--   - conserva las ventas existentes (vendedor_id queda NULL).

BEGIN;

-- Ownership de Ventas. Mismo tipo, longitud y nombre de indice que el modelo EF.
ALTER TABLE ventas
    ADD COLUMN IF NOT EXISTS vendedor_id VARCHAR(150) NULL;

CREATE INDEX IF NOT EXISTS "IX_ventas_vendedor_id" ON ventas (vendedor_id);

-- Tabla de usuarios necesaria para el login JWT y el primer administrador.
CREATE TABLE IF NOT EXISTS usuarios (
    id              SERIAL        PRIMARY KEY,
    email           VARCHAR(256)  NOT NULL,
    password_hash   VARCHAR(255)  NOT NULL,
    rol             VARCHAR(20)   NOT NULL
        CHECK (rol IN ('Administrador', 'Vendedor', 'Almacenista')),
    creado_en       TIMESTAMPTZ   NOT NULL DEFAULT now(),
    actualizado_en  TIMESTAMPTZ   NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_usuarios_email ON usuarios (email);

COMMIT;
