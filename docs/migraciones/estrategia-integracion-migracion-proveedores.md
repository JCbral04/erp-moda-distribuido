# Estrategia de integración de la migración `20261111164221_AddProveedoresYOrdenesCompra`

## 1. Propósito

Este documento describe cómo integrar de forma segura la migración
`20261111164221_AddProveedoresYOrdenesCompra` sin perder datos ni recrear tablas
existentes.

## 2. Tipo de migración

La migración `20261111164221_AddProveedoresYOrdenesCompra` es una **migración inicial
o baseline** para bases PostgreSQL vacías.

Su método `Up()` crea desde cero las tablas principales del ERP, incluyendo:

- `contador_factura`
- `productos`
- `proveedores`
- `ventas`
- `variantes`
- `ordenes_compra`
- `detalle_venta`
- `facturas`
- `movimientos_stock`
- `detalles_orden_compra`

Por lo tanto, **no es una migración incremental** y no debe ejecutarse sobre una base
que ya contenga esas tablas.

## 3. Riesgo sobre bases existentes

Ejecutar `Up()` sobre una base PostgreSQL existente con tablas de Inventario, Ventas,
Proveedores u Órdenes de Compra provocará errores porque intentará crear objetos que
ya existen.

Ejemplo esperado de error:

```text
relation "productos" already exists
```

Además, la migración no es idempotente: no valida previamente la existencia de las
tablas antes de crearlas.

## 4. Procedimiento para bases nuevas

Para una base PostgreSQL vacía o recién creada, aplicar la migración normalmente:

```bash
dotnet ef database update
```

O, en código, mediante:

```csharp
context.Database.Migrate();
```

Este es el flujo correcto para entornos nuevos, CI y pruebas temporales.

## 5. Procedimiento de baseline para bases existentes

Para una base PostgreSQL que ya contiene el esquema del ERP, **no ejecutar `Up()`**.
En su lugar, realizar baseline manual:

### 5.1 Verificación previa del esquema

Antes de registrar la migración, verificar que la base existente tenga:

1. Las tablas principales creadas:
   - `contador_factura`
   - `productos`
   - `proveedores`
   - `ventas`
   - `variantes`
   - `ordenes_compra`
   - `detalle_venta`
   - `facturas`
   - `movimientos_stock`
   - `detalles_orden_compra`

2. Las columnas esperadas por el modelo EF Core, incluyendo:
   - Campos de Proveedores en `snake_case`.
   - Campos de Órdenes de Compra en `snake_case`.
   - Campos monetarios con precisión `numeric(12,2)`.
   - Tokens de concurrencia `version` donde corresponda.

3. Las relaciones y restricciones necesarias, especialmente:
   - `ordenes_compra.proveedor_id` → `proveedores.id`
   - `detalles_orden_compra.orden_compra_id` → `ordenes_compra.id`

4. El índice único de NIT en `proveedores.nit`.

Si alguna tabla o columna no coincide, corregir el esquema antes de registrar la
migración. No registrar la migración sobre una base incompleta o inconsistente.

### 5.2 Registro de la migración en `__EFMigrationsHistory`

Una vez verificado el esquema, registrar la migración como aplicada sin ejecutar
`Up()`:

```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111164221_AddProveedoresYOrdenesCompra', '9.0.22');
```

Ajustar `ProductVersion` a la versión real de Entity Framework Core utilizada por el
proyecto.

### 5.3 Verificación posterior

Después del registro, confirmar que:

```sql
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";
```

incluya:

```text
20261111164221_AddProveedoresYOrdenesCompra
```

Luego ejecutar la aplicación y las pruebas de integración para validar que el modelo
EF Core y la base existente coinciden.

## 6. Advertencia sobre `Down()`

El método `Down()` de esta migración elimina prácticamente todo el esquema del ERP:

- `contador_factura`
- `detalle_venta`
- `detalles_orden_compra`
- `facturas`
- `movimientos_stock`
- `ordenes_compra`
- `ventas`
- `variantes`
- `proveedores`
- `productos`

**Advertencia crítica:** ejecutar `Down()` en cualquier entorno con datos reales
implica pérdida total de datos de Inventario, Ventas, Proveedores y Órdenes de Compra.

No ejecutar `Down()` en producción ni en bases con datos reales. Su uso debe limitarse
a entornos temporales, pruebas controladas o desarrollo local vacío.

## 7. Integración con CI

CI debe continuar usando bases PostgreSQL temporales y vacías, como lo hace
`BasePostgreSQLTest`.

Flujo esperado en CI:

1. Crear una base temporal vacía.
2. Aplicar todas las migraciones mediante `context.Database.Migrate()`.
3. Ejecutar las pruebas.
4. Eliminar la base temporal al finalizar.

No aplicar esta migración sobre bases compartidas, persistentes o de producción
durante CI.

## 8. Recomendación final

- Usar la migración normalmente solo en bases nuevas.
- Usar baseline manual en bases existentes.
- No ejecutar `Down()` fuera de entornos desechables.
- Mantener CI sobre bases temporales vacías.
- Documentar cualquier diferencia de esquema antes de registrar la migración como
  aplicada.