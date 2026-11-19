# Estrategia de persistencia

La estrategia definitiva del repositorio es **SQL manual versionado** (`docs/*/schema.sql`), coherente con el PR #96 (Proveedores y Órdenes de Compra). La aplicación **no ejecuta migraciones automáticamente** al arrancar.

## Scripts oficiales (instalación nueva)

Ejecutar, en este orden:

1. `docs/inventario/schema.sql`
2. `docs/ventas/schema.sql` (incluye `ventas.vendedor_id` y `"IX_ventas_vendedor_id"`)
3. `docs/seguridad/usuarios-schema.sql` (tabla `usuarios` para JWT)
4. `docs/proveedores/schema.sql` (módulo de Proveedores, PR #96)

## Bases existentes

Aplicar `docs/migraciones/001_auth_y_ownership.sql`. Es idempotente y no destructivo: añade `vendedor_id`, su índice y la tabla `usuarios` conservando las ventas previas.

## Migraciones de EF Core

**No se conservan migraciones de EF Core en el repositorio.** El proyecto no ejecuta
`dotnet ef database update` al arrancar y los scripts SQL manuales son la única
fuente de verdad del esquema. La aplicación nunca llama a `Migrate()`.

En concreto se retiraron `AddVendedorIdAVentas`, su `Designer` y
`ErpModaDbContextModelSnapshot.cs`: mantenerlos junto a los scripts oficiales dejaría
dos definiciones compitiendo del mismo cambio y daría la impresión de que EF Core
puede reconstruir el esquema, lo cual no es cierto en este proyecto. El mismo DDL
(`ventas.vendedor_id VARCHAR(150) NULL` e índice `"IX_ventas_vendedor_id"`) vive en:

- instalación nueva → `docs/ventas/schema.sql`;
- bases existentes → `docs/migraciones/001_auth_y_ownership.sql`.

Si en el futuro se decide adoptar migraciones EF Core, debe hacerse como cambio de
estrategia explícito y documentado: elegir UNA fuente de verdad y migrar los scripts
manuales, no mantener ambas en paralelo.

## Dependencia con PR #96

`docs/proveedores/schema.sql` no toca `ventas`, `usuarios` ni sus índices: sus tablas propias (`proveedores`, `ordenes_compra`, `detalle_orden_compra`) son independientes. No hay conflicto de DDL con estos cambios. La integración final debe incluir el módulo de Proveedores de #96 o, si se integra después, aplicar su script siguiendo esta misma estrategia manual.
