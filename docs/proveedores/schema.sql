-- Esquema de base de datos -- Modulo Proveedores y Ordenes de Compra (issue #10)
-- PostgreSQL 16. Script de creacion manual; EF Core esta configurado para PostgreSQL/Supabase.
-- Aplicar manualmente, p.ej.:
--   docker compose exec -T db psql -U erpuser -d erp_moda < docs/proveedores/schema.sql

BEGIN;

-- ===========================================================================
-- proveedores: catalogo de proveedores (HU-001)
-- ===========================================================================
CREATE TABLE proveedores (
    id                   SERIAL PRIMARY KEY,
    nombre               VARCHAR(150) NOT NULL,
    nit                  VARCHAR(50)  NOT NULL,
    contacto             VARCHAR(150),
    telefono             VARCHAR(50),
    correo               VARCHAR(100),
    direccion            VARCHAR(250),
    tiempo_entrega_dias  INTEGER      NOT NULL DEFAULT 0 CHECK (tiempo_entrega_dias >= 0),
    forma_pago           VARCHAR(100),
    activo               BOOLEAN      NOT NULL DEFAULT TRUE,
    creado_en            TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en       TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT uq_proveedores_nit UNIQUE (nit)
);

CREATE INDEX ix_proveedores_nit    ON proveedores (nit);
CREATE INDEX ix_proveedores_activo ON proveedores (activo);

-- ===========================================================================
-- ordenes_compra: cabecera de la orden de compra a proveedor (HU-002)
--   proveedores 1:N -> ordenes_compra (ON DELETE RESTRICT)
--   ordenes_compra 1:N -> detalles_orden_compra (ON DELETE CASCADE)
-- ===========================================================================
CREATE TABLE ordenes_compra (
    id             SERIAL PRIMARY KEY,
    proveedor_id   INTEGER       NOT NULL REFERENCES proveedores(id) ON DELETE RESTRICT,
    fecha          TIMESTAMPTZ   NOT NULL DEFAULT now(),
    estado         VARCHAR(20)   NOT NULL DEFAULT 'Pendiente'
        CHECK (estado IN ('Pendiente', 'Enviada', 'Parcial', 'Recibida', 'Cancelada')),
    subtotal       NUMERIC(12,2) NOT NULL CHECK (subtotal >= 0),
    impuesto       NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (impuesto >= 0),
    total          NUMERIC(12,2) NOT NULL CHECK (total >= 0),
    observaciones  VARCHAR(500),
    creado_en      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    actualizado_en TIMESTAMPTZ   NOT NULL DEFAULT now(),

    CONSTRAINT ck_ordenes_compra_total_coherente CHECK (subtotal + impuesto = total)
);

CREATE INDEX ix_ordenes_compra_proveedor_id ON ordenes_compra (proveedor_id);
CREATE INDEX ix_ordenes_compra_fecha        ON ordenes_compra (fecha);
CREATE INDEX ix_ordenes_compra_estado       ON ordenes_compra (estado);

-- ===========================================================================
-- detalles_orden_compra: renglon de producto ordenado (HU-002)
--   producto_id es referencia suave a inventario.productos(id)
-- ===========================================================================
CREATE TABLE detalles_orden_compra (
    id               SERIAL PRIMARY KEY,
    orden_compra_id  INTEGER       NOT NULL REFERENCES ordenes_compra(id) ON DELETE CASCADE,
    producto_id      INTEGER       NOT NULL,
    cantidad         INTEGER       NOT NULL CHECK (cantidad > 0),
    precio_unitario  NUMERIC(12,2) NOT NULL CHECK (precio_unitario >= 0),
    subtotal         NUMERIC(12,2) NOT NULL CHECK (subtotal >= 0),

    CONSTRAINT ck_detalles_orden_subtotal_coherente CHECK (subtotal = cantidad * precio_unitario)
);

CREATE INDEX ix_detalles_orden_compra_orden_id    ON detalles_orden_compra (orden_compra_id);
CREATE INDEX ix_detalles_orden_compra_producto_id ON detalles_orden_compra (producto_id);

COMMIT;

