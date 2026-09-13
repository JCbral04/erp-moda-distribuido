# Guía: Esquema de base de datos de Inventario

**Issue relacionado:** #5 · **Responsable:** Jair Enrique Polo Chamorro (Inventario + IA) · **Fecha:** 13/09/2026

## ¿Qué se hizo?

Se diseñó el esquema de base de datos (PostgreSQL 16) para el núcleo de Inventario: `productos`, `variantes` y una tabla nueva, `movimientos_stock`, que registra la auditoría de todo cambio de stock. El diagrama entidad-relación (Mermaid) y la narrativa completa de decisiones de diseño viven en `docs/inventario/esquema-bd.md`; el DDL ejecutable está en `docs/inventario/schema.sql`. Esta guía documenta el *proceso* — cómo se llegó a ese diseño, qué problema real se encontró al verificarlo, y cómo se probó — no el diseño en sí.

## Decisiones técnicas y por qué

(Resumen — el detalle completo con cada constraint está en `docs/inventario/esquema-bd.md`)

### 1. `cantidad` firmada en `movimientos_stock`

Positivo = entrada, negativo = salida, en vez de cantidad sin signo + columna de dirección separada. Mapea directo con el contrato de IA (`+1/-1`) y permite `SUM(cantidad)` para el stock neto sin lógica adicional.

### 2. `variante_id` obligatorio en todo movimiento

Incluida la recepción de proveedor, aunque el contrato de Proveedores (`docs/requerimientos/proveedores.md`) solo declara "ID producto" — inconsistencia real, documentada como pregunta abierta para Andrés en el PR, no resuelta unilateralmente.

### 3. Columnas opcionales: se incluyeron algunas, se descartaron otras

Se confirmó explícitamente conmigo antes de implementar: se incluyeron `stock_minimo` por variante y timestamps de auditoría; se descartaron `stock_resultante` (derivable) y `requiere_confirmacion` (la lógica de confirmación manual de IA no existe todavía, agregar el campo solo no resuelve nada).

## Incidencia durante la implementación

Al revisar el script SQL manualmente (sin ejecutarlo todavía) se detectó un bug real en el constraint `ck_mov_referencia_coherente`: usaba comparaciones normales (`referencia_tipo = 'venta'`) para validar que cada tipo de movimiento tuviera la referencia correcta. En SQL, una comparación contra `NULL` evalúa a `NULL` (no a `FALSE`), y Postgres considera **satisfecho** un `CHECK` cuyo resultado es `NULL` — no solo cuando es `TRUE`. Esto significaba que un movimiento `tipo='venta'` con `referencia_tipo` vacío habría pasado el constraint sin ser rechazado, dejando pasar datos inconsistentes silenciosamente.

**Solución:** reemplazar las comparaciones por `IS NOT DISTINCT FROM`, que siempre evalúa a `TRUE`/`FALSE` y nunca a `NULL`. Lección: cualquier `CHECK` que combine una columna nullable con `=` necesita revisarse contra el caso `NULL` explícitamente — no basta con probar los casos "felices".

## Verificación

Instalación de Docker Desktop + WSL2 en la máquina de desarrollo (no estaban instalados previamente), luego:

```powershell
docker compose up -d db
Get-Content docs\inventario\schema.sql -Raw | docker compose exec -T db psql -U erpuser -d erp_moda
docker compose exec db psql -U erpuser -d erp_moda -c "\dt"
```

Las 3 tablas se crearon sin errores. Se probaron manualmente, con `psql` interactivo dentro de una transacción con `SAVEPOINT`/`ROLLBACK TO SAVEPOINT` (para poder seguir probando después de cada falla esperada sin que Postgres abortara toda la transacción), los 8 casos límite del esquema:

| Prueba | Esperado | Resultado |
|:---|:---|:---|
| Stock negativo | Falla | ✅ `variantes_stock_check` |
| Ajuste manual sin motivo | Falla | ✅ `ck_mov_motivo_requerido` |
| Ajuste manual con motivo | Funciona | ✅ |
| Confianza fuera de rango (1.5) | Falla | ✅ `movimientos_stock_confianza_check` |
| Confianza válida en detección IA (0.92) | Funciona | ✅ |
| Venta sin `referencia_tipo` | Falla | ✅ `ck_mov_referencia_coherente` — confirma la corrección del bug anterior |
| Venta con `referencia_tipo` correcto | Funciona | ✅ |
| Variante duplicada (mismo producto+talla+color) | Falla | ✅ `uq_variantes_producto_talla_color` |

Todo se deshizo con `ROLLBACK` final — no quedaron datos de prueba en la base. Confirmado el 13/09/2026.

## Pendiente

- Respuesta de Andrés sobre cómo Proveedores va a resolver la granularidad de variante al registrar una recepción de mercancía.
- Adopción de EF Core (fuera de alcance, ver `docs/guias/docker-compose.md`, "Pendiente Sprint 1").
- Conectar `schema.sql` a Docker Compose automáticamente — no existe montaje `docker-entrypoint-initdb.d` todavía; por ahora se aplica manualmente.
- Implementar los endpoints/lógica de negocio que usarán este esquema (Inventario sigue en memoria hasta entonces).
