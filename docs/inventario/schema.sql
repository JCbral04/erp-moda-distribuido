-- Esquema de base de datos -- Modulo Inventario (issue #5)
-- PostgreSQL 16. Script interino de creacion manual; no hay EF Core todavia
-- (ver docs/guias/docker-compose.md, seccion "Pendiente Sprint 1").
-- No se aplica automaticamente: no hay volumen docker-entrypoint-initdb.d configurado.
-- Aplicar manualmente, p.ej.:
--   docker compose exec -T db psql -U erpuser -d erp_moda < docs/inventario/schema.sql

BEGIN;

CREATE TABLE productos (
    id              SERIAL PRIMARY KEY,
    nombre          VARCHAR(150)   NOT NULL,
    descripcion     TEXT,
    precio_base     NUMERIC(12,2)  NOT NULL CHECK (precio_base >= 0),
    categoria       VARCHAR(100)   NOT NULL,
    activo          BOOLEAN        NOT NULL DEFAULT TRUE,
    creado_en       TIMESTAMPTZ    NOT NULL DEFAULT now(),
    actualizado_en  TIMESTAMPTZ    NOT NULL DEFAULT now()
);

CREATE TABLE variantes (
    id              SERIAL PRIMARY KEY,
    producto_id     INTEGER        NOT NULL REFERENCES productos(id) ON DELETE RESTRICT,
    talla           VARCHAR(20)    NOT NULL,
    color           VARCHAR(50)    NOT NULL,
    sku             VARCHAR(50)    NOT NULL,
    stock           INTEGER        NOT NULL DEFAULT 0 CHECK (stock >= 0),
    sobreprecio     NUMERIC(12,2)  CHECK (sobreprecio IS NULL OR sobreprecio >= 0),
    stock_minimo    INTEGER        NOT NULL DEFAULT 5 CHECK (stock_minimo >= 0),
    creado_en       TIMESTAMPTZ    NOT NULL DEFAULT now(),
    actualizado_en  TIMESTAMPTZ    NOT NULL DEFAULT now(),
    CONSTRAINT uq_variantes_sku UNIQUE (sku),
    CONSTRAINT uq_variantes_producto_talla_color UNIQUE (producto_id, talla, color)
);

CREATE INDEX ix_variantes_producto_id ON variantes (producto_id);

-- movimientos_stock: registro de auditoria de TODO cambio de stock (RN-002 inventario.md)
--
-- Notas de diseno:
-- - cantidad es FIRMADA (positivo = entrada, negativo = salida). Mapea directo con el
--   contrato de IA ("+1/-1") y permite SUM(cantidad) para el stock neto sin logica extra.
-- - variante_id es obligatorio para TODOS los tipos, incluida recepcion_proveedor, aunque
--   el contrato actual de Proveedores (docs/requerimientos/proveedores.md) solo declara
--   "ID producto". Ver docs/inventario/esquema-bd.md, seccion "Coordinacion con Andres".
-- - referencia_id es una referencia SUAVE (sin FK dura) a venta.id u orden_compra.id: esas
--   tablas pertenecen a otros modulos (Ventas sigue en memoria, Proveedores es dueno de
--   ordenes_compra) y no viven en este esquema.
-- - tipo/referencia_tipo usan VARCHAR + CHECK en vez de ENUM nativo de Postgres, porque
--   agregar valores a un ENUM requiere ALTER TYPE y este esquema es interino pre-EF Core.
CREATE TABLE movimientos_stock (
    id                  BIGSERIAL PRIMARY KEY,
    variante_id         INTEGER        NOT NULL REFERENCES variantes(id) ON DELETE RESTRICT,
    tipo                VARCHAR(30)    NOT NULL
        CHECK (tipo IN ('venta', 'anulacion_venta', 'recepcion_proveedor', 'ajuste_manual', 'deteccion_ia')),
    cantidad            INTEGER        NOT NULL CHECK (cantidad <> 0), -- positivo=entrada, negativo=salida
    referencia_tipo     VARCHAR(30)
        CHECK (referencia_tipo IS NULL OR referencia_tipo IN ('venta', 'orden_compra')),
    referencia_id       INTEGER,       -- referencia suave: venta.id u orden_compra.id
    motivo              VARCHAR(255),  -- obligatorio para ajuste_manual / anulacion_venta
    usuario             VARCHAR(150)   NOT NULL, -- texto libre: no existe modulo de usuarios/auth aun
    confianza           NUMERIC(5,4)   CHECK (confianza IS NULL OR (confianza >= 0 AND confianza <= 1)),
    creado_en           TIMESTAMPTZ    NOT NULL DEFAULT now(),

    CONSTRAINT ck_mov_motivo_requerido
        CHECK (tipo NOT IN ('ajuste_manual', 'anulacion_venta') OR motivo IS NOT NULL),
    CONSTRAINT ck_mov_confianza_solo_ia
        CHECK (tipo = 'deteccion_ia' OR confianza IS NULL),
    -- Nota: se usa IS NOT DISTINCT FROM en vez de "=" porque una comparacion normal con
    -- NULL evalua a NULL (no a FALSE), y Postgres considera satisfecho un CHECK cuyo
    -- resultado es NULL -- eso dejaria pasar, por ejemplo, un movimiento tipo='venta'
    -- con referencia_tipo=NULL sin rechazarlo. IS NOT DISTINCT FROM siempre da TRUE/FALSE.
    CONSTRAINT ck_mov_referencia_coherente
        CHECK (
            (tipo IN ('venta', 'anulacion_venta') AND referencia_tipo IS NOT DISTINCT FROM 'venta') OR
            (tipo = 'recepcion_proveedor' AND referencia_tipo IS NOT DISTINCT FROM 'orden_compra') OR
            (tipo IN ('ajuste_manual', 'deteccion_ia') AND referencia_tipo IS NULL)
        )
);

CREATE INDEX ix_mov_variante_id ON movimientos_stock (variante_id);
CREATE INDEX ix_mov_tipo        ON movimientos_stock (tipo);
CREATE INDEX ix_mov_creado_en   ON movimientos_stock (creado_en);

COMMIT;
