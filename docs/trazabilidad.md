# Trazabilidad — Módulo Proveedores

## Visión general

Este documento describe el flujo de trazabilidad del módulo Proveedores y Órdenes de Compra,
incluyendo los cambios de estado, la bitácora de auditoría y los servicios involucrados.

## Servicios relacionados

| Servicio | Responsabilidad |
|---|---|
| `ProveedoresService` | CRUD de proveedores, validación de NIT único, transacciones serializables |
| `OrdenesCompraService` | Creación de órdenes de compra, validación de proveedor activo (RN-003), validación de productos |
| `WorkflowService` | Cambios de estado de proveedores y órdenes de compra |
| `AuditoriaLogService` | Bitácora automática de acciones relevantes |

## Cambios de estado y WorkflowService

Todo cambio de estado en el módulo Proveedores debe pasar por `WorkflowService`:

1. **Cambiar estado de proveedor (Activo/Inactivo)**: `WorkflowService.CambiarEstadoProveedorAsync()`
   - Valida que el proveedor exista.
   - Valida que el nuevo estado sea "Activo" o "Inactivo".
   - Registra en bitácora: usuario, fecha, entidad, entidadId, operación, datos anteriores y nuevos.

2. **Cambiar estado de orden de compra**: `WorkflowService.CambiarEstadoOrdenAsync()`
   - Valida que la orden exista.
   - Valida que el nuevo estado sea uno de: Pendiente, Enviada, Parcial, Recibida, Cancelada.
   - Registra en bitácora con la misma estructura.

## Bitácora de auditoría (`AuditoriaLog`)

La tabla `auditoria_log` registra automáticamente:

| Campo | Descripción |
|---|---|
| `usuario_id` | Identidad del usuario que realizó la acción |
| `usuario_nombre` | Nombre del usuario (opcional) |
| `entidad` | Tipo de entidad afectada (Proveedor, OrdenCompra) |
| `entidad_id` | ID de la entidad afectada |
| `operacion` | Tipo de operación (Crear, Actualizar, CambiarEstado, Eliminar) |
| `datos_anteriores` | JSON con los valores antes del cambio |
| `datos_nuevos` | JSON con los valores después del cambio |
| `fecha_operacion` | Fecha y hora UTC de la operación |
| `direccion_ip` | IP del cliente (opcional) |

Acciones auditadas:
- Creación de proveedor
- Actualización de proveedor
- Cambio de estado de proveedor (Activo/Inactivo)
- Eliminación de proveedor
- Creación de orden de compra
- Cambio de estado de orden de compra

## Reglas de negocio (RN)

| ID | Regla | Implementación |
|---|---|---|
| RN-001 | No se puede recibir más de lo ordenado sin justificación | Pendiente (HU-003 no implementado) |
| RN-002 | Toda recepción actualiza Inventario automáticamente | Pendiente (HU-003 no implementado) |
| RN-003 | Un proveedor inactivo no puede recibir nuevas órdenes | `OrdenesCompraService.CreateAsync()` verifica `proveedor.Activo` |

## Integridad de datos

- **NIT único**: Índice `UNIQUE (nit)` en tabla `proveedores`. Validación de aplicación + restricción de BD.
- **Eliminación de proveedor**: Bloqueada si existen órdenes de compra asociadas (`ON DELETE RESTRICT` + validación de aplicación).
- **Concurrencia optimista**: Columna `version` (BYTEA) en `proveedores`, `ordenes_compra` y `detalles_orden_compra` para detectar actualizaciones perdidas.
- **Precisión decimal**: Campos monetarios (`subtotal`, `impuesto`, `total`, `precio_unitario`) como `NUMERIC(12,2)`.

## Autenticación y autorización

- Esquema JWT configurado en `Program.cs` (`AddAuthentication` + `AddJwtBearer`).
- Middleware `UseAuthentication()` y `UseAuthorization()` en el pipeline.
- Política "Administrador" que requiere el rol "Administrador".
- Endpoints protegidos con `[Authorize(Roles = "Administrador")]`.

## Pendientes / Por definir

- Implementar HU-003: Registro de recepción de mercancía y actualización de stock en Inventario.
- Implementar HU-004: Filtros por rango de fechas en historial de compras.
- Implementar RN-001 y RN-002.
- Crear migraciones EF Core para despliegue automático de esquema.
- Endpoint de emisión de tokens JWT (login).
