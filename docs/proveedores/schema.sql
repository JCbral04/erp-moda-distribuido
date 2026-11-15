-- Esquema de base de datos -- Modulo Proveedores y Ordenes de Compra (issue #10)
-- PostgreSQL 16. Script de creacion manual alineado con el modelo EF Core actual
-- (ErpModaDbContext + entidades Proveedor, OrdenCompra, DetalleOrdenCompra).
-- No se aplica automaticamente: aplicar manualmente, p.ej.:
--   docker compose exec -T db psql -U erpuser -d erp_moda < docs/proveedores/schema.sql
--
-- Dependencias previas:
--   - Base de datos erp_moda creada.
--   - No requiere que exista la tabla productos: detalles_orden_compra.producto_id
--     es una referencia logica/suave a productos.id (sin FK dura), igual que
--     movimientos_stock.referencia_id en el modulo de Inventario. El DDL de este
--     script es independiente y puede ejecutarse antes o despues de
--     docs/inventario/schema.sql.
--
-- Notas de diseno:
--   - Los enums EstadoProveedor y EstadoOrdenCompra se persisten como INTEGER
--     (mapeo por defecto de EF Core): 0=Activo/0=Pendiente, etc.
--   - version es el token de concurrencia optimista (IsConcurrencyToken en EF Core).
--   - nit tiene restriccion UNIQUE, que en PostgreSQL crea automaticamente un
--     indice unico; no se declara un indice adicional.
--   - proveedor_id y orden_compra_id tienen indices para las FK.

BEGIN;

CREATE TABLE proveedores (
    id              SERIAL PRIMARY KEY,
    razon_social    VARCHAR(150)   NOT NULL,
    nit             VARCHAR(20)    NOT NULL,
    contacto        VARCHAR(60),
    email           VARCHAR(100)   NOT NULL,
    telefono        VARCHAR(20),
    direccion       VARCHAR(120),
    forma_pago      VARCHAR(50),
    tiempo_entrega  INTEGER,
    estado          INTEGER        NOT NULL,
    version         INTEGER        NOT NULL,
    creado_en       TIMESTAMPTZ    NOT NULL DEFAULT now(),
    actualizado_en  TIMESTAMPTZ    NOT NULL DEFAULT now(),
    CONSTRAINT uq_proveedores_nit UNIQUE (nit)
);

CREATE TABLE ordenes_compra (
    id              SERIAL PRIMARY KEY,
    proveedor_id    INTEGER        NOT NULL REFERENCES proveedores(id) ON DELETE RESTRICT,
    fecha           TIMESTAMPTZ    NOT NULL DEFAULT now(),
    subtotal        NUMERIC(12,2)  NOT NULL,
    total           NUMERIC(12,2)  NOT NULL,
    estado          INTEGER        NOT NULL,
    observaciones   VARCHAR(500),
    creado_en       TIMESTAMPTZ    NOT NULL DEFAULT now(),
    actualizado_en  TIMESTAMPTZ    NOT NULL DEFAULT now(),
    version         INTEGER        NOT NULL
);

CREATE INDEX ix_ordenes_compra_proveedor_id ON ordenes_compra (proveedor_id);

CREATE TABLE detalles_orden_compra (
    id                  SERIAL PRIMARY KEY,
    orden_compra_id     INTEGER        NOT NULL REFERENCES ordenes_compra(id) ON DELETE CASCADE,
    producto_id         INTEGER        NOT NULL,  -- referencia suave a productos.id (Inventario)
    cantidad            INTEGER        NOT NULL,
    precio_unitario     NUMERIC(12,2)  NOT NULL,
    subtotal            NUMERIC(12,2)  NOT NULL,
    version             INTEGER        NOT NULL
);

CREATE INDEX ix_detalles_orden_compra_orden_compra_id ON detalles_orden_compra (orden_compra_id);

COMMIT;
