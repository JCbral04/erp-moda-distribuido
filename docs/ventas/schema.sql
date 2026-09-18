-- Esquema de base de datos -- Modulo Ventas y Facturacion (issue #8)
-- PostgreSQL 16. Script de creacion manual; EF Core esta configurado para PostgreSQL/Supabase.
-- La creacion de tablas se realiza mediante este script SQL manual y la conexion
-- debe configurarse mediante User Secrets (ver backend/ErpModa.Api/USER_SECRETS_CONFIG.md).
-- No se aplica automaticamente: no hay volumen docker-entrypoint-initdb.d configurado.
-- Aplicar manualmente, p.ej.:
--   docker compose exec -T db psql -U erpuser -d erp_moda < docs/ventas/schema.sql

BEGIN;

-- ===========================================================================
-- ventas: cabecera de la venta.
--   ventas  1:N -> detalle_venta  (una venta tiene N lineas de producto, RN-001)
--   ventas  1:1 -> facturas       (una venta genera a lo sumo una factura, RN-004)
-- estado es espejo del enum EstadoVenta (Ventas/Models/EstadoVenta.cs):
--   Pendiente, Confirmada, Anulada.
-- La venta NUNCA se borra (RN-005): se marca Anulada conservando el motivo.
-- ===========================================================================
CREATE TABLE ventas (
    id                SERIAL PRIMARY KEY,
    fecha             TIMESTAMPTZ   NOT NULL,            -- la establece la aplicacion en UTC (VentasService)
    subtotal          NUMERIC(12,2) NOT NULL CHECK (subtotal >= 0),   -- HU-001: se calcula automaticamente
    impuesto          NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (impuesto >= 0),
    total             NUMERIC(12,2) NOT NULL CHECK (total > 0),       -- RN-002: el total debe ser mayor a cero
    metodo_pago       VARCHAR(20)   NOT NULL
        CHECK (metodo_pago IN ('Efectivo', 'Tarjeta', 'Transferencia')), -- espejo MetodoPago.cs (HU-001)
    estado            VARCHAR(20)   NOT NULL
        CHECK (estado IN ('Pendiente', 'Confirmada', 'Anulada')),      -- espejo EstadoVenta.cs
    motivo_anulacion  VARCHAR(255),
    creado_en         TIMESTAMPTZ   NOT NULL DEFAULT now(),
    actualizado_en    TIMESTAMPTZ   NOT NULL DEFAULT now(),

    -- Coherencia monetaria: el total DEBE ser la suma exacta de subtotal + impuesto.
    -- NUMERIC es decimal exacto en Postgres, esta igualdad nunca falla por redondeo.
    CONSTRAINT ck_ventas_total_coherente CHECK (subtotal + impuesto = total),
    -- La anulacion exige justificacion (HU-004) y la venta no se elimina (RN-005).
    -- El estado es NOT NULL, asi que 'estado <> ''Anulada''' nunca evalua a NULL
    -- (no aplica la trampa de CHECK + NULL; ver docs/inventario/esquema-bd.md 3.4).
    CONSTRAINT ck_ventas_motivo_requerido
        CHECK (estado <> 'Anulada' OR motivo_anulacion IS NOT NULL)
);

CREATE INDEX ix_ventas_fecha  ON ventas (fecha);   -- historial por rango de fechas (HU-003)
CREATE INDEX ix_ventas_estado ON ventas (estado);  -- resumen y filtros por estado

-- ===========================================================================
-- detalle_venta: una linea de producto de la venta. Requiere al menos un detalle
-- (RN-001), el que esta tabulando es entrega de Inventario/negocio y Ventas al crear.
-- variante_id es una REFERENCIA SUAVE a inventario.variantes(id): la tabla variantes
-- pertenece al esquema de Inventario (docs/inventario/schema.sql) y no vive en este
-- script (se ejecuta en cualquier orden). Al consolidar los esquemas (p.ej. EF Core)
-- se puede endurecer como REFERENCES variantes(id) ON DELETE RESTRICT.
-- Nota: Ventas descuenta stock SIEMPRE a nivel de variante, nunca de producto general
-- (docs/requerimientos/inventario.md, seccion 1.1; contrato docs/contratos/ventas-inventario.md).
-- ===========================================================================
CREATE TABLE detalle_venta (
    id              SERIAL PRIMARY KEY,
    venta_id        INTEGER       NOT NULL REFERENCES ventas(id) ON DELETE RESTRICT,
    variante_id     INTEGER       NOT NULL,  -- referencia suave -> inventario.variantes(id)
    cantidad        INTEGER       NOT NULL CHECK (cantidad > 0),           -- RN-001: al menos 1 unidad
    precio_unitario NUMERIC(12,2) NOT NULL CHECK (precio_unitario >= 0),
    subtotal        NUMERIC(12,2) NOT NULL CHECK (subtotal >= 0),

    -- Coherencia: el subtotal de la linea DEBE ser cantidad * precio_unitario.
    CONSTRAINT ck_detalle_subtotal_coherente CHECK (subtotal = cantidad * precio_unitario)
);

CREATE INDEX ix_detalle_venta_venta_id    ON detalle_venta (venta_id);    -- join con la cabecera
CREATE INDEX ix_detalle_venta_variante_id ON detalle_venta (variante_id); -- historial por producto/variante (HU-003) y contrato Ventas->Inventario

-- ===========================================================================
-- facturas: factura fiscal generada automaticamente al CONFIRMAR la venta (RN-004,
-- CU-001 ventas.md paso 7). Relacion 1:1 con ventas: UNIQUE sobre venta_id impide
-- que una venta tenga varias facturas (la FK sola permitiria N filas por venta).
-- Estados: emitida (se genero junto a la venta) / pagada (seguimiento de pagos,
-- descripcion del modulo en docs/requerimientos/ventas.md) / anulada (la venta se
-- anulo, HU-004). La coherencia factura.anulada <-> venta.anulada se garantiza en la
-- capa de aplicacion: un CHECK de fila no puede validar el estado de otra tabla.
-- ===========================================================================
CREATE TABLE facturas (
    id                SERIAL PRIMARY KEY,
    venta_id          INTEGER       NOT NULL REFERENCES ventas(id) ON DELETE RESTRICT,
    numero            VARCHAR(30)   NOT NULL,  -- formato de numeracion lo define la aplicacion
    fecha             TIMESTAMPTZ   NOT NULL DEFAULT now(),   -- fecha de emision (HU-002)
    -- Datos de cliente (HU-002: la factura incluye "cliente"). NO existe modulo de
    -- clientes en el repo todavia, por eso NO se crea tabla clientes y estas tres
    -- columnas son NULLables/preparadas (decision documentada en docs/ventas/esquema-bd.md):
    --   - cliente_id:      referencia suave al futuro modulo de clientes
    --   - cliente_nombre:  snapshot del cliente al momento de emitir la factura
    --   - cliente_documento: documento fiscal (RUT/NIT/CC) si el cliente lo provee
    cliente_id        INTEGER,      -- referencia suave, se endurece cuando exista clientes
    cliente_nombre    VARCHAR(150),
    cliente_documento VARCHAR(30),
    subtotal          NUMERIC(12,2) NOT NULL CHECK (subtotal >= 0),
    impuesto          NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (impuesto >= 0),
    total             NUMERIC(12,2) NOT NULL CHECK (total > 0),
    estado            VARCHAR(20)   NOT NULL DEFAULT 'emitida'
        CHECK (estado IN ('emitida', 'pagada', 'anulada')),
    creado_en         TIMESTAMPTZ   NOT NULL DEFAULT now(),

    CONSTRAINT uq_facturas_venta_id UNIQUE (venta_id),              -- 1:1 venta -> factura (RN-004)
    CONSTRAINT uq_facturas_numero  UNIQUE (numero),                 -- numero de factura unico (fiscal)
    CONSTRAINT ck_facturas_total_coherente CHECK (subtotal + impuesto = total)
);

CREATE INDEX ix_facturas_fecha ON facturas (fecha);   -- historial de facturas (HU-002/HU-003)

-- ===========================================================================
-- contador_factura: contador atómico para generación de números de factura.
-- Una sola fila (id=1) actualizada con UPDATE ... SET valor = valor + 1
-- dentro de la transacción de confirmación, evitando race conditions.
-- ===========================================================================
CREATE TABLE contador_factura (
    id                INTEGER       PRIMARY KEY DEFAULT 1,
    valor             BIGINT        NOT NULL DEFAULT 0,
    prefijo           VARCHAR(10)   NOT NULL DEFAULT 'FAC-',
    formato           VARCHAR(20)   NOT NULL DEFAULT 'D6',
    actualizado_en    TIMESTAMPTZ   NOT NULL DEFAULT now(),

    CONSTRAINT ck_contador_factura_singleton CHECK (id = 1)
);

-- Inicializar la fila singleton
INSERT INTO contador_factura (id, valor, prefijo, formato, actualizado_en)
VALUES (1, 0, 'FAC-', 'D6', now())
ON CONFLICT (id) DO NOTHING;

COMMIT;