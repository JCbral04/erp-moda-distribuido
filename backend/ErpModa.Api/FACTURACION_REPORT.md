# Reporte de Facturación - HU-002 y RN-004

## Análisis de Cumplimiento de Requerimientos

### HU-002: Generar factura

**Criterios de aceptación:**
- [x] La factura incluye: número, fecha, cliente, productos, totales
- [ ] Se puede imprimir o enviar por correo (NO implementado - fuera de alcance)
- [x] Queda almacenada en el sistema

### RN-004: Toda venta genera factura automáticamente

**Estado:** ✅ CUMPLIDO

## Verificación Detallada

### 1. Número de factura
- **Implementación:** ✅ Generado automáticamente en `GenerarNumeroFacturaAsync()`
- **Formato:** `FAC-000001`, `FAC-000002`, etc.
- **Unicidad:** ✅ `UNIQUE(numero)` en schema SQL
- **Estado:** ✅ Coherente con requerimientos

### 2. Fecha
- **Implementación:** ✅ `Factura.Fecha = DateTime.UtcNow` al generar
- **Schema:** ✅ `TIMESTAMPTZ NOT NULL DEFAULT now()`
- **Estado:** ✅ Coherente con requerimientos

### 3. Cliente
- **Implementación:** ✅ Campos preparados: `ClienteId`, `ClienteNombre`, `ClienteDocumento`
- **Schema:** ✅ Todos NULLables, preparados para módulo futuro
- **Estado:** ✅ Coherente con requerimientos (HU-002 pide cliente, módulo no existe aún)

### 4. Productos/detalles de la venta
- **Implementación:** ⚠️ PARCIALMENTE
  - La factura NO almacena directamente los detalles
  - Tiene navegación a través de `Factura.Venta.Detalles`
  - Se puede acceder a los detalles mediante la relación Venta 1:1 Factura
- **Schema:** ✅ Coherente (detalles están en `detalle_venta`, no en `facturas`)
- **Estado:** ⚠️ Técnicamente cumple HU-002 (la factura "incluye" productos vía relación), pero podría mejorarse
- **Corrección realizada:** Agregué propiedad de navegación `Detalles` `[NotMapped]` en `Factura.cs` para facilitar acceso

### 5. Subtotal
- **Implementación:** ✅ `Factura.Subtotal = venta.Subtotal`
- **Schema:** ✅ `NUMERIC(12,2) NOT NULL`
- **Estado:** ✅ Coherente con requerimientos

### 6. Impuesto
- **Implementación:** ✅ `Factura.Impuesto = venta.Impuesto`
- **Schema:** ✅ `NUMERIC(12,2) NOT NULL DEFAULT 0`
- **Estado:** ✅ Coherente con requerimientos

### 7. Total
- **Implementación:** ✅ `Factura.Total = venta.Total`
- **Schema:** ✅ `NUMERIC(12,2) NOT NULL`
- **Estado:** ✅ Coherente con requerimientos

### 8. Estado
- **Implementación:** ✅ `"emitida"` al generar, `"anulada"` al anular venta
- **Schema:** ✅ `VARCHAR(20) NOT NULL DEFAULT 'emitida'`
- **Estados válidos:** `emitida`, `pagada`, `anulada`
- **Estado:** ✅ Coherente con requerimientos

### 9. Almacenamiento
- **Implementación:** ✅ Tabla `facturas` en PostgreSQL
- **Persistencia:** ✅ EF Core con Npgsql
- **Estado:** ✅ Coherente con requerimientos

### 10. Relación 1:1 con Venta
- **SQL:** ✅ `UNIQUE(venta_id)` + FK
- **EF Core:** ✅ Configurado en `ErpModaDbContext`
- **Estado:** ✅ Coherente con requerimientos

### 11. Generación automática al confirmar
- **Implementación:** ✅ En `ConfirmarAsync()` línea 153-167
- **Condición:** ✅ Solo cuando `venta.Estado == EstadoVenta.Pendiente`
- **Estado:** ✅ Coherente con RN-004

### 12. Anulación de factura cuando se anula una venta
- **Implementación:** ✅ En `AnularAsync()` línea 192-197
- **Lógica:** ✅ Busca factura por `VentaId` y marca estado como `"anulada"`
- **Estado:** ✅ Coherente con HU-004

### 13. Venta pendiente NO genera factura
- **Implementación:** ✅ Verificado en `ConfirmarAsync()` línea 141-143
- **Lógica:** ✅ Solo confirma ventas en estado `Pendiente`
- **Estado:** ✅ Coherente con requerimientos

## Archivos Modificados

### 1. backend/ErpModa.Api/Ventas/Models/Factura.cs
- **Cambio:** Agregada propiedad de navegación `Detalles [NotMapped]`
- **Justificación:** Facilita acceso a productos/detalles de la venta para cumplir HU-002
- **Líneas:** 68 → 74

## Tests

### Estado actual:
- ⚠️ Los tests existentes (`VentasServiceTests.cs`) usan el constructor antiguo `new VentasService()`
- ⚠️ El nuevo `VentasService` requiere `ErpModaDbContext` en el constructor
- ⚠️ Los tests necesitan actualización para trabajar con EF Core

### Comandos ejecutados:
- ❌ `dotnet restore` - NO ejecutado (SDK no disponible en entorno)
- ❌ `dotnet build` - NO ejecutado (SDK no disponible en entorno)
- ❌ `dotnet test` - NO ejecutado (SDK no disponible en entorno)

### Recomendación:
Los tests deben actualizarse para:
1. Inyectar `ErpModaDbContext` en los tests
2. Usar `InMemoryDatabase` de EF Core para testing
3. Actualizar el método `CreateService()` para proporcionar el contexto

Esto está fuera del alcance de la corrección de facturación básica solicitada.

## Recomendación sobre Separación de Módulo

### ¿Debería Facturación separarse como módulo independiente?

**Recomendación:** SÍ, pero NO en este momento.

**Justificación:**
1. **Escalabilidad futura:** Cuando se implemente:
   - PDF de facturas
   - Envío por correo
   - Facturación electrónica (DIAN)
   - Seguimiento de pagos externos
   - Notificaciones de vencimiento

2. **Complejidad:** Facturación puede crecer significativamente:
   - Generación de PDF con plantillas
   - Integración con servicios externos (email, DIAN)
   - Colas de procesamiento para facturación electrónica
   - Retenciones de facturación

3. **Arquitectura actual:** Ventas y Facturación están acopladas porque:
   - HU-002 es básica: "generar factura con datos de la venta"
   - RN-004 exige generación automática
   - No hay funcionalidad compleja de facturación aún

**Cuándo separar:**
- Cuando se implemente PDF o facturación electrónica
- Cuando se requiera lógica de negocio específica de facturación
- Cuando el módulo tenga su propio conjunto de historias de usuario
- Cuando se requiera endpoints específicos de facturación (reemitir, consultar historial, etc.)

**Propuesta de separación futura:**
- Módulo `Facturacion` con sus propios DTOs, servicios y controllers
- Mantener relación 1:1 con Ventas (una venta genera una factura)
- Ventas sigue generando la factura, pero delega lógica compleja al módulo
- Contrato Ventas→Facturación similar al actual Ventas→Inventario

## Conclusión

### Estado de Facturación Básica:
✅ **CUMPLE** con HU-002 y RN-004 en su forma básica actual

### Lo que falta (fuera de alcance solicitado):
- ❌ PDF (explícitamente solicitado NO implementar)
- ❌ Excel (explícitamente solicitado NO implementar)
- ❌ Correo (explícitamente solicitado NO implementar)
- ❌ Facturación electrónica DIAN (explícitamente solicitado NO implementar)
- ❌ Pagos externos (explícitamente solicitado NO implementar)
- ❌ Frontend (explícitamente solicitado NO implementar)

### Pendiente:
- ⏳ Actualización de tests para trabajar con EF Core
- ⏳ Consideración de separación de módulo cuando crezca en complejidad

**La implementación actual de facturación es suficiente para cumplir HU-002 y RN-004 en su forma básica.**
