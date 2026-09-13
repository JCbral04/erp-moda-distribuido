# Guía: Esqueleto del módulo de Inventario

**Issue relacionado:** #6 · **Responsable:** Jair Enrique Polo Chamorro (Inventario + IA) · **Fecha:** 12/09/2026

## ¿Qué se hizo?

Se implementó el CRUD básico de productos del módulo de Inventario en ASP.NET Core Web API, siguiendo exactamente la misma estructura de carpetas que ya usa el módulo Ventas (`Controllers/DTOs/Interfaces/Models/Services`), con almacenamiento en memoria y sin lógica de negocio todavía — el issue pedía el esqueleto, no las reglas de stock.

## Endpoints implementados

| Método | Ruta | Descripción |
|:---|:---|:---|
| GET | `/api/productos` | Lista todos los productos |
| GET | `/api/productos/{id}` | Obtiene un producto por id (404 si no existe) |
| POST | `/api/productos` | Crea un producto, con variantes opcionales en el mismo request |
| PUT | `/api/productos/{id}` | Actualiza un producto existente |
| DELETE | `/api/productos/{id}` | Elimina un producto |

## Decisiones técnicas y por qué

### 1. `Variante` como parte del modelo de `Producto` desde el esqueleto

El issue solo pedía CRUD de productos, pero `docs/requerimientos/inventario.md` (HU-001) ya define que el catálogo se organiza en producto + variantes (talla/color, stock independiente) desde el primer momento. Incluirlo ahora evita rediseñar el modelo cuando se implemente el resto del CRUD de variantes.

### 2. Almacenamiento en memoria, sin EF Core

Coincide con el estado del resto del backend: el módulo Ventas tampoco persiste en base de datos todavía. La persistencia real llega con el esquema SQL del issue #5 y, más adelante, con la adopción de EF Core (ver `docs/guias/docker-compose.md`, sección "Pendiente Sprint 1").

### 3. Servicio singleton inyectado por DI

`IProductosService`/`ProductosService` se registran como `AddSingleton` en `Program.cs`, igual que `IVentasService` — mantiene el estado en memoria vivo mientras corre el proceso, consistente con el patrón ya establecido.

## Verificación

Compilación limpia:

```bash
dotnet build backend/ErpModa.sln --configuration Release
```

Ciclo CRUD completo probado manualmente contra el binario compilado (sin `dotnet run` porque el entorno de desarrollo solo tenía el runtime .NET 10 instalado, no el 8.0 — se ejecutó con `DOTNET_ROLL_FORWARD=LatestMajor` como workaround local, sin cambiar el `TargetFramework` del proyecto):

- `POST /api/productos` → `201 Created` con el producto y sus variantes.
- `GET /api/productos` / `GET /api/productos/{id}` → reflejan los datos creados.
- `PUT /api/productos/{id}` → `200 OK` con los campos actualizados.
- `DELETE /api/productos/{id}` → `204 No Content`; `GET` posterior confirma que ya no existe.
- `GET /api/productos/{id-inexistente}` → `404 Not Found`.
- `swagger.json` en `/swagger` responde `200` y lista `/api/Productos` y `/api/Productos/{id}` junto a `/api/Ventas`.

Confirmado el 12/09/2026. Criterios de aceptación del issue #6 cumplidos.

## Pendiente

- Persistencia real contra PostgreSQL (issue #5 — esquema de base de datos).
- Lógica de negocio: descuento de stock, validación de stock disponible, registro de movimientos.
- CRUD independiente de variantes (por ahora solo se crean junto con el producto).
