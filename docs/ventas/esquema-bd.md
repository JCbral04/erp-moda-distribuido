# Esquema de base de datos — Ventas y Facturación

**Issue:** #8 — Diseñar esquema de base de datos de Ventas y Facturación
**Responsable:** Andres Felipe Vargas Serrato
**Estado:** Implementado (rama `feature/modulo-ventas`). EF Core está configurado para PostgreSQL/Supabase; la creación de tablas se realiza mediante el script SQL manual (`docs/ventas/schema.sql`) y la conexión debe configurarse mediante User Secrets.

---

## 1. Resumen

El esquema tiene 4 tablas:

| Tabla | Propósito |
|---|---|
| `ventas` | Cabecera de la venta: fecha, subtotal, impuesto, total, método de pago, estado y motivo de anulación. |
| `detalle_venta` | Líneas de producto de la venta: variante, cantidad, precio unitario y subtotal. Un mínimo de una por venta (RN-001). |
| `facturas` | Factura fiscal generada automáticamente al confirmar la venta (RN-004), con relación 1:1 a la venta. |
| `contador_factura` | Contador atómico (singleton) para generación segura de números de factura secuenciales bajo concurrencia. |

Es un script SQL de creación manual (`docs/ventas/schema.sql`). EF Core está configurado para PostgreSQL/Supabase; las tablas se crean ejecutando este script manualmente y la conexión se configura mediante User Secrets (ver `backend/ErpModa.Api/USER_SECRETS_CONFIG.md`).

---

## 2. Diagrama entidad-relación

```mermaid
erDiagram
    VENTAS ||--o{ DETALLE_VENTA : "tiene lineas (1:N)"
    VENTAS ||--o| FACTURAS : "genera (1:1)"
    DETALLE_VENTA }o--|| VARIANTES : "referencia variante (N:1)"

    VENTAS {
        int id PK
        timestamptz fecha
        numeric subtotal
        numeric impuesto
        numeric total
        varchar metodo_pago
        varchar estado
        varchar motivo_anulacion
        timestamptz creado_en
        timestamptz actualizado_en
    }

    DETALLE_VENTA {
        int id PK
        int venta_id FK
        int variante_id "FK suave -> inventario.variantes"
        int cantidad
        numeric precio_unitario
        numeric subtotal
    }

    FACTURAS {
        int id PK
        int venta_id FK UK
        varchar numero UK
        timestamptz fecha
        int cliente_id "FK suave, futura"
        varchar cliente_nombre
        varchar cliente_documento
        numeric subtotal
        numeric impuesto
        numeric total
        varchar estado
        timestamptz creado_en
    }

    CONTADOR_FACTURA {
        int id PK
        bigint valor
        varchar prefijo
        varchar formato
        timestamptz actualizado_en
    }

    VARIANTES {
        int id PK
        int producto_id FK
        varchar talla
        varchar color
        varchar sku
        int stock
        numeric sobreprecio
        int stock_minimo
        timestamptz creado_en
        timestamptz actualizado_en
    }
```

**Nota:** La tabla `VARIANTES` pertenece al módulo Inventario (`docs/inventario/schema.sql`). `detalle_venta.variante_id` es una referencia suave (sin FK dura) porque los esquemas se ejecutan independientemente. Cuando los esquemas se consoliden se endurecerá como `REFERENCES variantes(id) ON DELETE RESTRICT`.

---

## 3. Decisiones de diseño

### 3.1 `variante_id` como referencia suave a `inventario.variantes`

`detalle_venta.variante_id` es `INTEGER NOT NULL` **sin** `REFERENCES`: la tabla `variantes` pertenece al esquema de Inventario (`docs/inventario/schema.sql`) y no vive en este script, que debe poder ejecutarse en cualquier orden e independientemente. Es el mismo patrón de referencia suave que Inventario usa para `movimientos_stock.referencia_id` (ver `docs/inventario/esquema-bd.md`, sección 3.4). Cuando los esquemas se consoliden (p. ej. con EF Core) se endurece como `REFERENCES variantes(id) ON DELETE RESTRICT`. Se agrega índice `ix_detalle_venta_variante_id` porque este es el punto de unión con el contrato Ventas→Inventario (`docs/contratos/ventas-inventario.md`) y el historial por producto/variante (HU-003).

### 3.2 Relación 1:1 `ventas` ↔ `facturas` (RN-004)

La factura se genera automáticamente al **confirmar** la venta (CU-001, paso 7). Por eso `facturas.venta_id` es `NOT NULL` con FK a `ventas` **y** `UNIQUE`: la FK sola permitiría varias facturas por venta; el `UNIQUE (venta_id)` garantiza que una venta tenga **a lo sumo** una factura. Los requerimientos actuales no contemplan reemisiones ni facturas múltiples, así que la restricción es estricta.

### 3.3 Datos de cliente en `facturas`: preparados, sin tabla propia

HU-002 exige que la factura incluya el *cliente*. **No existe módulo de clientes en el repositorio** (igual que no existe para empleados/vendedores), y `docs/DECISIONES.md` sección 6 prohíbe implementar contra suposiciones. Por lo tanto:

- **No se crea tabla `clientes`.**
- Se dejan **tres columnas NULLables preparadas** en `facturas`:
  - `cliente_id` — referencia suave (sin FK). Cuando nazca el módulo de clientes se convierte en `cliente_id INTEGER REFERENCES clientes(id)`.
  - `cliente_nombre` / `cliente_documento` — snapshot del cliente al momento de emitir la factura (práctica fiscal estándar: la factura conserva los datos que tenía el cliente en la fecha de emisión, aunque después cambie).

Dejar estos campos en la factura (y no en `ventas`) respeta que el único uso de cliente soportado hoy es el comprobante (HU-002). Cuando exista el módulo de clientes se decide si `ventas` también lo referencia.

### 3.4 `VARCHAR + CHECK` en vez de `ENUM` nativo

`metodo_pago`, `estado` (ventas) y `estado` (facturas) usan `VARCHAR` con `CHECK`, consistente con la decisión 3.6 de `docs/inventario/esquema-bd.md`: agregar valores a un `ENUM` de Postgres requiere `ALTER TYPE`, y este esquema es interino pre-EF Core.

Los valores de `ventas.metodo_pago` y `ventas.estado` son **espejo exacto** de los enums C# `MetodoPago` (`Efectivo`, `Tarjeta`, `Transferencia`) y `EstadoVenta` (`Pendiente`, `Confirmada`, `Anulada`) — mapeo 1:1 con `VentasService.MapToResponse`. Los estados de `facturas` (`emitida`, `pagada`, `anulada`) son independentes de los de la venta: `emitida` (se generó con la confirmación, RN-004), `pagada` (el módulo se describe en `ventas.md` como *"generación de facturas y seguimiento de pagos"`; es el caso mínimo soportado, extensible a una tabla `pagos` más adelante), `anulada` (HU-004: la factura queda marcada como anulada).

### 3.5 Chequeos cruzados de coherencia monetaria

- `ventas`: `CHECK (subtotal + impuesto = total)` — elimina la posibilidad de guardar un total que no sea la suma de sus partes.
- `detalle_venta`: `CHECK (subtotal = cantidad * precio_unitario)` — elimina subtotales arbitrarios por línea.
- `facturas`: `CHECK (subtotal + impuesto = total)` — igual que la venta.

`NUMERIC(12,2)` es decimal exacto en Postgres, así que estas igualdades nunca fallan por redondeo de punto flotante. Los totales de la factura replican los de la venta (denormalización fiscal intencional); la igualdad entre factura y venta (subtotal/total) se garantiza en la capa de aplicación porque es una relación entre filas de tablas distintas.

### 3.6 Anulación: la venta no se elimina, exige motivo (RN-005, HU-004)

`ventas.estado` nunca se borra la fila; pasa a `Anulada` conservando `motivo_anulacion`, y el `CHECK (estado <> 'Anulada' OR motivo_anulacion IS NOT NULL)` exige justificación (HU-004). El `estado` es `NOT NULL`, por lo que la expresión nunca evalúa a `NULL` en el `CHECK` (la misma trampa de `CHECK`+`NULL` documentada en `docs/guias/esquema-bd-inventario.md` no aplica aquí). Las FK de `detalle_venta` y `facturas` usan `ON DELETE RESTRICT`, que junto a RN-005 le da a la anulación el rol de borrado lógico.

La coherencia cruzada `factura.anulada` ↔ `venta.anulada` (cuando la venta se anula, su factura también, HU-004) no se puede expresar en un `CHECK` de una sola fila y se garantiza en la capa de aplicación (`VentasService.AnularAsync`).

### 3.7 Índices

| Índice | Tabla | Justificación |
|---|---|---|
| `ix_ventas_fecha` | ventas | Historial por rango de fechas (HU-003) |
| `ix_ventas_estado` | ventas | Resumen y filtros por estado (endpoint `/resumen`) |
| `ix_detalle_venta_venta_id` | detalle_venta | Join cabecera↔líneas |
| `ix_detalle_venta_variante_id` | detalle_venta | Historial por producto/variante (HU-003) y contrato Ventas→Inventario |
| `ix_facturas_fecha` | facturas | Historial de facturas (HU-002/HU-003) |

Los índices únicos de `facturas` (`venta_id`, `numero`) los crea Postgres automáticamente por `UNIQUE` y no se repiten en la lista.

---

## 4. Relaciones

| Relación | Cardinalidad | Garantizada por | Notas |
|---|---|---|---|
| `ventas` → `detalle_venta` | 1:N | FK `detalle_venta.venta_id` + validación servicio | Una venta tiene una o más líneas (RN-001); la BD no impide venta sin detalles (FK sola no cuenta hijos), la garantía está en `VentasService.CreateAsync` que lanza `ArgumentException` si no hay detalles. |
| `ventas` → `facturas` | 1:1 | FK + `UNIQUE (venta_id)` | Una venta genera a lo sumo una factura (RN-004) |
| `detalle_venta` → `inventario.variantes` | N:1 | Ninguna (referencia suave) | `variante_id` apunta a la tabla de Inventario; se endurece al consolidar esquemas |
| `facturas` → futura `clientes` | N:1 (preparada) | Ninguna (referencia suave) | `cliente_id` NULLable hasta que exista el módulo de clientes |
| `contador_factura` | 1:1 (singleton) | `CHECK (id = 1)` | Una sola fila (`id=1`) para contador atómico de facturas |

---

## 5. Flujo de integración con Inventario

Esta sección documenta el flujo de datos entre Ventas e Inventario definido en el contrato `docs/contratos/ventas-inventario.md`. **Implementado** en `VentasService.ConfirmarAsync` y `AnularAsync` usando el `ErpModaDbContext` compartido con transacciones atómicas (`IsolationLevel.Serializable` en PostgreSQL).

### 5.1 Confirmación de venta

Cuando una venta se confirma (`ventas.estado = 'Confirmada'`), el módulo Ventas invoca al módulo Inventario para validar y descontar stock:

```
Venta confirmada (ventas.estado = 'Confirmada')
→ Inventario valida stock disponible por variante
→ Inventario descuenta stock de variantes (salida)
→ Inventario registra movimiento en movimientos_stock
→ movimientos_stock.tipo = 'venta'
→ movimientos_stock.cantidad = negativo (salida)
→ movimientos_stock.referencia_tipo = 'venta'
→ movimientos_stock.referencia_id = ventas.id
```

**Ejemplo de movimiento generado:**
```sql
INSERT INTO movimientos_stock (
    variante_id,
    tipo,
    cantidad,
    referencia_tipo,
    referencia_id,
    usuario,
    creado_en
) VALUES
(10, 'venta', -2, 'venta', 1, 'vendedor_1', now());
```

### 5.2 Anulación de venta

Cuando una venta se anula (`ventas.estado = 'Anulada'`), el módulo Ventas invoca al módulo Inventario para restaurar el stock:

```
Venta anulada (ventas.estado = 'Anulada', motivo_anulacion proporcionado)
→ Inventario restaura stock de variantes (entrada)
→ Inventario registra movimiento en movimientos_stock
→ movimientos_stock.tipo = 'anulacion_venta'
→ movimientos_stock.cantidad = positivo (entrada)
→ movimientos_stock.referencia_tipo = 'venta'
→ movimientos_stock.referencia_id = ventas.id
→ movimientos_stock.motivo = ventas.motivo_anulacion
```

**Ejemplo de movimiento generado:**
```sql
INSERT INTO movimientos_stock (
    variante_id,
    tipo,
    cantidad,
    referencia_tipo,
    referencia_id,
    motivo,
    usuario,
    creado_en
) VALUES
(10, 'anulacion_venta', 2, 'venta', 1, 'Error en registro', 'admin_1', now());
```

### 5.3 Referencias cruzadas

- `movimientos_stock.referencia_id` es una referencia suave a `ventas.id` (sin FK dura)
- `detalle_venta.variante_id` es una referencia suave a `inventario.variantes.id` (sin FK dura)
- Estas referencias se endurecerán como FKs cuando se adopte EF Core y se consoliden los esquemas

---

## 6. Estados válidos

| Columna | Valores | Fuente |
|---|---|---|
| `ventas.estado` | `Pendiente`, `Confirmada`, `Anulada` | enum `EstadoVenta.cs` |
| `ventas.metodo_pago` | `Efectivo`, `Tarjeta`, `Transferencia` | enum `MetodoPago.cs` |
| `facturas.estado` | `emitida`, `pagada`, `anulada` | RN-004 / descripción del módulo / HU-004 |

---

## 7. Alcance explícito

**NO se crean tablas** de `clientes` ni de `empleados`/`vendedores`: esos módulos no existen en el repositorio (ver `docs/DECISIONES.md`, sección 6 y la justificación en 3.3). El filtro por *vendedor* de HU-003 y la participación del *cliente* en la venta quedan como pendientes (sección 8), no como columnas inventadas.

**NO se crea otra tabla de variantes:** `detalle_venta.variante_id` reutiliza la variante del módulo Inventario (decisión 3.1).

---

## 8. Cómo aplicar el script

```bash
docker compose up -d db
docker compose exec -T db psql -U erpuser -d erp_moda < docs/ventas/schema.sql
```

No se aplica automáticamente al levantar `docker compose up` — no hay volumen `docker-entrypoint-initdb.d` configurado en `docker-compose.yml` (fuera de alcance de este issue). Se verifica de forma aislada en un contenedor descartable, sin aplicar cambios a la base de datos real.

---

## 9. Coordinación con el equipo

**Para revisión de Jair / Juan Esteban:**
- ¿El `UNIQUE (facturas.venta_id)` es suficiente, o la factura electrónica exigirá soportar **reemisiones** (una venta → varias facturas en el tiempo)? Hoy no está contemplado y el esquema lo impide a propósito.
- ¿El estado `pagada` de la factura debe venir con una tabla `pagos`, o el caso mínimo (columna de estado) es suficiente por ahora?
- ¿Número de factura con secuencia anual (`FAC-2026-00001`) o secuencia global? El `UNIQUE (numero)` plantea el formato en la capa de aplicación.

## 10. Pendiente

- Cuando exista el módulo de clientes: endurecer `facturas.cliente_id` como FK y decidir si `ventas.cliente_id` es necesario (hoy no hay justificación).
- Cuando exista el módulo de empleados: el filtro de historial por *vendedor* (HU-003) requerirá una columna `vendedor_id` en `ventas`.

## 11. Implementación EF Core (Completada)

El modelo C# incluye `subtotal` e `impuesto` en `Venta`, coherente con el schema SQL y HU-001. La implementación EF Core está completa en `backend/ErpModa.Api/Data/ErpModaDbContext.cs` con mapeo exacto a las tablas SQL. La integración con PostgreSQL/Supabase está configurada; la conexión debe establecerse mediante User Secrets (ver `backend/ErpModa.Api/USER_SECRETS_CONFIG.md`). No se usa `Database.Migrate()` al iniciar la aplicación.