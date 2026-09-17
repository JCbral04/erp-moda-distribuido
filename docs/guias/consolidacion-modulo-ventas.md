# Guía: Consolidación del módulo de Ventas

**Issue relacionado:** #<issue> · **Responsable:** Andres Felipe Vargas Serrato (Ventas, Facturación, Proveedores) · **Fecha:** 15/09/2026

## ¿Qué se hizo?

Se consolidó el esqueleto del módulo de Ventas: ciclo de vida de la venta (Pendiente → Confirmada / Anulada), método de pago validado, separación entre crear y confirmar la venta, historial con filtros y resumen de ventas. Se mantuvo la arquitectura existente `Controller → Interface → Service → Models/DTOs` y el almacenamiento en memoria (C# `List<Venta>`), sin base de datos, EF Core, PostgreSQL ni frontend. **No se integró Inventario**: la validación/descuento de stock queda preparada pero sin implementar, pendiente de aprobar el contrato propuesto en `docs/contratos/ventas-inventario.md`.

## Endpoints

| Método | Ruta | Descripción |
|:---|:---|:---|
| GET | `/api/ventas` | Lista todas las ventas, con filtros opcionales (`?estado=`, `?fecha=`) |
| GET | `/api/ventas/resumen` | Resumen calculado de las ventas almacenadas |
| GET | `/api/ventas/{id}` | Obtiene una venta por id (404 si no existe) |
| POST | `/api/ventas` | Crea una venta en estado **Pendiente** (201) |
| POST | `/api/ventas/{id}/confirmar` | Confirma una venta pendiente → **Confirmada** (200) |
| POST | `/api/ventas/{id}/anular` | Anula una venta con motivo → **Anulada**, sin eliminar (200) |

`POST /api/ventas` ya no crea la venta "directamente": la deja en `Pendiente`, y el descuento de stock se disparará recién al confirmar (cuando exista el contrato con Inventario).

### GET `/api/ventas` con filtros

Los parámetros `estado` y `fecha` son opcionales y combinables. Sin parámetros se devuelven todas las ventas.

| Parámetro | Ejemplo | Comportamiento |
|:---|:---|:---|
| `estado` | `?estado=Confirmada`, `?estado=Anulada`, `?estado=Pendiente` | Filtra por estado (coincidencia por nombre, sin distinguir mayúsculas). Un valor que no sea uno de esos tres devuelve `400 Bad Request`; vacío se trata como ausencia de filtro |
| `fecha` | `?fecha=2026-09-14` | Filtra las ventas de ese día (cualquier hora). Una fecha sin coincidencias devuelve lista vacía |

Ejemplos:

```text
GET /api/ventas?estado=Confirmada
GET /api/ventas?estado=Anulada
GET /api/ventas?fecha=2026-09-14
GET /api/ventas?estado=Confirmada&fecha=2026-09-14
```

No se agregaron filtros por vendedor ni cliente porque esos modelos no existen todavía en el repositorio.

### GET `/api/ventas/resumen`

Calcula y devuelve desde las ventas en memoria:

```json
{
  "cantidadVentas": 5,
  "ventasConfirmadas": 4,
  "ventasAnuladas": 1,
  "totalVentasConfirmadas": 350000
}
```

- `cantidadVentas`: total de ventas (incluye pendientes, confirmadas y anuladas).
- `ventasConfirmadas` / `ventasAnuladas`: conteo por estado.
- `totalVentasConfirmadas`: suma solo de ventas en estado **Confirmada**. Las anuladas (y pendientes) no se suman.

## Decisiones técnicas y por qué

### 1. Ciclo de vida tipado con enums (`EstadoVenta`, `MetodoPago`)

`EstadoVenta` (`Pendiente`, `Confirmada`, `Anulada`) y `MetodoPago` (`Efectivo`, `Tarjeta`, `Transferencia`) viven en `Ventas/Models/`. Centralizan los valores válidos y las validaciones de estado/método de pago pedidas en los requerimientos. Los DTOs de respuesta exponen los nombres en texto plano (`"Pendiente"`, `"Efectivo"`) para que la API (Swagger/frontend) los muestre con claridad, sin números de enum. La colección `Venta.Detalles` no es nullable y se inicializa con `new()`, de modo que el modelo nunca expone `null` donde se espera una lista.

### 2. Método de pago como string en el DTO de entrada, validado por nombre

`CrearVentaDto.MetodoPago` se envía como texto y el servicio lo valida contra los nombres exactos del enum (aceptando minúsculas), rechazando también valores numéricos como `"1"` y el vacío. En sitios de negocio la regla es explicita en `docs/requerimientos/ventas.md` (HU-001): efectivo, tarjeta, transferencia.

### 3. Separación crear/confirmar

`POST /api/ventas` valida datos y deja la venta en `Pendiente`. `POST /api/ventas/{id}/confirmar` verifica que exista y esté `Pendiente`, deja el punto de integración de stock claramente marcado en `VentasService.ConfirmarAsync` (sin llamada real) y recién entonces pasa a `Confirmada`. Esto refleja el flujo CU-001 de `ventas.md`: primero se arma la venta, al confirmar se valida/descuenta stock.

### 4. Anulación sin borrado (RN-005)

`POST /api/ventas/{id}/anular` exige `motivo` (se conserva en la venta, mapea a `movimientos_stock.motivo` cuando exista el contrato), no elimina la venta, y rechaza re-anular o anular una venta inexistente. El stock se restaurará recién cuando se implemente el contrato.

### 5. Ni `vendedor` ni `cliente` todavía

Los requerimientos mencionan a ambos como actores, pero **no existe aún ningún modelo/contrato** de vendedor o cliente en el repositorio (no hay módulo de usuarios ni de clientes). Agregarlos ahora sería inventar entidades externas sin contrato, por lo que se omitieron (ver `docs/DECISIONES.md`, sección 6).

### 6. Mapeo de errores en el Controller

El controller solo orquesta; toda validación vive en el servicio:
- `ArgumentException` → `400 Bad Request` (validación de datos)
- `InvalidOperationException` → `400 Bad Request` (estado inválido: ya confirmada, ya anulada)
- `KeyNotFoundException` → `404 Not Found` (venta inexistente)

### 7. Proyecto de pruebas nuevo

Se creó `backend/ErpModa.Api.Tests` (xUnit, agregado a `ErpModa.sln`, target `net8.0`) con pruebas del servicio, no del controller, porque la lógica de negocio está en el servicio. CI (`dotnet test backend/ErpModa.sln`) ya lo ejecuta.

### 8. Filtros de historial y resumen sin lógica en el Controller

`GET /api/ventas` recibe `estado` y `fecha` como query parameters opcionales y el Controller los delega tal cual al servicio. La conversión de "texto de estado" a `EstadoVenta` (con validación y `400` para valores inválidos) y la aplicación de filtros viven en el servicio. El resumen solo cuenta y suma estados definidos: `cantidadVentas` es el conteo total, `totalVentasConfirmadas` suma únicamente `Confirmada`, y las anuladas nunca aportan al total vendido.

### 9. Refactor por Code Review (concurrencia, filtro de fecha, helpers, stub)

Cambios aplicados en esta consolidación para endurecer el `VentasService` en memoria:

- **Concurrencia:** todas las lecturas/escrituras sobre `_ventas` y los contadores se serializan con `lock (_syncLock)`. Los ids usan `Interlocked.Increment` (por eso los contadores arrancan en `0`: el primer valor asignado es `1`). `GetAllAsync` materializa con `.ToList()` dentro del `lock` para evitar el error *"Collection was modified"* si otro hilo muta la lista mientras se serializa la respuesta.
- **Filtro de fecha por rango UTC (zona local UTC-5):** `?fecha=` se interpreta como el día calendario en la zona del negocio (Colombia, UTC-5) y se convierte en un rango UTC `[inicioUtc, inicioUtc + 1 día)` — es decir `05:00Z` a `05:00Z` del día siguiente. La comparación es `v.Fecha >= inicio && v.Fecha < fin`, no `v.Fecha.Date == fecha`. Así el filtro es determinista aunque `Venta.Fecha` se almacene como `DateTime.UtcNow` (en memoria no existe timezone aplicada).
- **Helpers:** `GetVentaOrThrow(id)` centraliza el "no existe la venta" que se repetía en confirmar/anular; `ParseEnumByName<TEnum>` (genérico, `where TEnum : struct, Enum`) unifica el parsing/validación de `MetodoPago` y del filtro `EstadoVenta` que antes vivía duplicado.
- **Integración con Inventario (implementada):** `ConfirmarAsync` valida y descuenta stock (validación de todas las líneas antes de mutar, sin descuentos parciales) y registra los movimientos `movimientos_stock` tipo `venta`/`anulacion_venta` tal como define `docs/inventario/schema.sql` y `docs/contratos/ventas-inventario.md` (opción b). Toda la operación (descuento + estado + factura, o restauración + estado + factura anulada) corre en una **transacción EF Core** (`IsolationLevel.Serializable` cuando hay proveedor relacional; el guard de estado garantiza no descontar/restaurar dos veces). El antiguo stub `DescontarStockInventarioAsync`/`RestaurarStockInventarioAsync` fue eliminado.

## Verificación

```bash
dotnet build backend/ErpModa.sln --configuration Release                    # 0 warnings, 0 errores
dotnet test  backend/ErpModa.sln --configuration Release --no-build         # 54 pruebas, todas correctas
```

Swagger verificado: al levantar la API, `/swagger` lista los endpoints de Ventas en `/api/Ventas` (incluidos `GET /api/Ventas/resumen` y el `GET /api/Ventas` con query params `estado`/`fecha`), y `/swagger/v1/swagger.json` responde `200` describiendo los DTOs `CrearVentaDto`, `AnularVentaDto`, `VentaResponseDto` y `VentasResumenDto`. `POST /api/Ventas/{id}/confirmar` responde `409 conflict` con `codigo: "stock_insuficiente"` cuando el stock no alcanza.

## Pendiente

- Definir cuándo nace el modelo de `cliente` y `vendedor` para incorporarlos a la venta (HU-002, HU-003).
- Verificar la integración contra PostgreSQL real (los tests usan EF Core InMemory; la transacción `Serializable` solo se activa con proveedor relacional). No se usa `Database.Migrate()` al iniciar.