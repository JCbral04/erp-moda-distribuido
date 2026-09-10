# Requerimientos — Ventas y Facturación

**Responsable:** Andres Felipe Vargas Serrato  
**Fecha:** 10/09/2026  
**Estado:** Borrador

---

## 1. Descripción del módulo

Gestiona el proceso de venta completo: registro de ventas, cálculo de totales, generación de facturas y seguimiento de pagos. Se integra con Inventario (descuenta stock) y Análisis (datos para reportes).

---

## 2. Actores

| Actor | Descripción |
|:---|:---|
| Vendedor | Empleado que atiende al cliente y registra la venta |
| Administrador | Supervisa ventas, anula si es necesario |
| Cliente | Persona que compra (no interactúa directamente con el sistema) |

---

## 3. Historias de usuario

### HU-001: Registrar venta

**Como** Vendedor  
**Quiero** registrar una venta con productos, cantidades y método de pago  
**Para** completar la transacción con el cliente

**Criterios de aceptación:**
- [ ] Se pueden agregar múltiples productos a la venta
- [ ] Se calcula subtotal, impuestos y total automáticamente
- [ ] Se selecciona método de pago (efectivo, tarjeta, transferencia)
- [ ] Al confirmar, se descuenta stock de Inventario

---

### HU-002: Generar factura

**Como** Vendedor  
**Quiero** generar una factura con los datos de la venta  
**Para** entregar comprobante al cliente

**Criterios de aceptación:**
- [ ] La factura incluye: número, fecha, cliente, productos, totales
- [ ] Se puede imprimir o enviar por correo
- [ ] Queda almacenada en el sistema

---

### HU-003: Consultar historial de ventas

**Como** Administrador  
**Quiero** ver el historial de ventas por fecha, vendedor o producto  
**Para** analizar el desempeño del negocio

**Criterios de aceptación:**
- [ ] Filtros por rango de fechas, vendedor, producto
- [ ] Muestra totales por período
- [ ] Exportar a PDF o Excel

---

### HU-004: Anular venta

**Como** Administrador  
**Quiero** anular una venta registrada por error  
**Para** corregir el inventario y los registros

**Criterios de aceptación:**
- [ ] Solo administradores pueden anular
- [ ] Se requiere justificación
- [ ] El stock se restaura automáticamente
- [ ] La factura queda marcada como anulada

---

## 4. Reglas de negocio

| ID | Regla | Módulo afectado |
|:---|:---|:---|
| RN-001 | Toda venta debe tener al menos un producto | Ventas |
| RN-002 | El total debe ser mayor a cero | Ventas |
| RN-003 | No se puede vender sin stock disponible | Inventario |
| RN-004 | Toda venta genera factura automáticamente | Facturación |
| RN-005 | Las ventas anuladas no se eliminan, se marcan | Ventas |

---

## 5. Casos de uso principales

### CU-001: Proceso de venta completo

**Actor principal:** Vendedor  
**Precondición:** Cliente en tienda con productos seleccionados

**Flujo principal:**
1. Vendedor inicia nueva venta
2. Busca y agrega productos con cantidades
3. Sistema calcula totales
4. Vendedor selecciona método de pago
5. Confirma la venta
6. Sistema descuenta stock
7. Sistema genera factura
8. Vendedor entrega factura al cliente

**Flujos alternos:**
- 2a. Producto sin stock → mostrar alerta, permitir quitar o cambiar
- 5a. Pago rechazado → cancelar venta, no afectar stock

**Postcondición:** Venta registrada, stock actualizado, factura generada

---

## 6. Contratos con otros módulos

| Módulo origen | Módulo destino | Qué se envía | Cuándo |
|:---|:---|:---|:---|
| Ventas | Inventario | ID producto, ID variante, cantidad | Al confirmar venta |
| Ventas | Facturación | Datos de venta, cliente, totales | Al confirmar venta |
| Ventas | Análisis | Datos de ventas | En tiempo real o batch |

---

## 7. Pendientes / Por definir

- [ ] ¿Manejo de descuentos o promociones?
- [ ] ¿Clientes frecuentes con historial?
- [ ] ¿Devoluciones con reembolso?