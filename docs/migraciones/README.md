# Estrategia de persistencia

La estrategia definitiva del repositorio es **SQL manual versionado** (`docs/*/schema.sql`), coherente con el PR #96 (Proveedores y Órdenes de Compra). La aplicación **no ejecuta migraciones automáticamente** al arrancar.

## Scripts oficiales (instalación nueva)

Ejecutar, en este orden:

1. `docs/inventario/schema.sql`
2. `docs/ventas/schema.sql` (incluye `ventas.vendedor_id` y `"IX_ventas_vendedor_id"`)
3. `docs/seguridad/usuarios-schema.sql` (tabla `usuarios` para JWT)
4. `docs/proveedores/schema.sql` (del PR #96, cuando ese módulo esté integrado)

## Bases existentes

Aplicar `docs/migraciones/001_auth_y_ownership.sql`. Es idempotente y no destructivo: añade `vendedor_id`, su índice y la tabla `usuarios` conservando las ventas previas.

## Migraciones de EF Core

Se conservan para entornos que decidan usar `dotnet ef database update`. En la rama de integración final se mantiene **una única migración consolidada**, `AgregarUsuarios`, que crea `usuarios` y añade `vendedor_id` a `ventas`, junto con su `Designer` y el `ModelSnapshot`.

La migración `AddVendedorIdAVentas` del PR #98 **no se incorpora**: haría redundante el cambio y dejaría dos snapshots compitiendo. Su DDL queda reflejado tal cual en el script oficial y en la migración consolidada (misma columna `VARCHAR(150) NULL` y mismo índice `"IX_ventas_vendedor_id"`).

## Dependencia con PR #96

`docs/proveedores/schema.sql` no toca `ventas`, `usuarios` ni sus índices: sus tablas propias (`proveedores`, `ordenes_compra`, `detalle_orden_compra`) son independientes. No hay conflicto de DDL con estos cambios. La integración final debe incluir el módulo de Proveedores de #96 o, si se integra después, aplicar su script siguiendo esta misma estrategia manual.
