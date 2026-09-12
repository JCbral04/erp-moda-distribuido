# Requerimientos — Proveedores

**Responsable:** Andres Felipe Vargas Serrato  
**Fecha:** 10/09/2026  
**Estado:** Borrador

---

## 1. Descripción del módulo

Gestiona el catálogo de proveedores, órdenes de compra y recepción de mercancía. Se integra con Inventario (aumenta stock al recibir) y mantiene historial de compras.

---

## 2. Actores

| Actor | Descripción |
|:---|:---|
| Administrador | Gestiona proveedores y órdenes de compra |
| Proveedor | Empresa/persona que suministra productos (no usa el sistema) |

---

## 3. Historias de usuario

### HU-001: Registrar proveedor

**Como** Administrador  
**Quiero** registrar un proveedor con sus datos de contacto y condiciones  
**Para** tenerlo disponible para órdenes de compra

**Criterios de aceptación:**
- [ ] Guarda: nombre, NIT, contacto, teléfono, correo, dirección
- [ ] Condiciones comerciales: tiempo de entrega, forma de pago
- [ ] Estado activo/inactivo

---

### HU-002: Crear orden de compra

**Como** Administrador  
**Quiero** crear una orden de compra a un proveedor  
**Para** reabastecer inventario

**Criterios de aceptación:**
- [ ] Se selecciona proveedor
- [ ] Se agregan productos con cantidades y precios acordados
- [ ] Se calcula total de la orden
- [ ] Estado: pendiente, enviada, parcial, recibida, cancelada

---

### HU-003: Registrar recepción de mercancía

**Como** Administrador  
**Quiero** registrar la llegada de productos de una orden  
**Para** actualizar inventario y cerrar la orden

**Criterios de aceptación:**
- [ ] Se selecciona orden de compra pendiente
- [ ] Se confirman cantidades recibidas (pueden diferir de la orden)
- [ ] El stock aumenta automáticamente en Inventario
- [ ] La orden cambia a estado "recibida" cuando se completa; si quedan cantidades pendientes, conserva estado "parcial"

---

### HU-004: Consultar historial de compras

**Como** Administrador  
**Quiero** ver el historial de órdenes por proveedor o fecha  
**Para** evaluar proveedores y planificar compras

**Criterios de aceptación:**
- [ ] Filtros por proveedor, estado, rango de fechas
- [ ] Muestra totales gastados por período

---

## 4. Reglas de negocio

| ID | Regla | Módulo afectado |
|:---|:---|:---|
| RN-001 | No se puede recibir más de lo ordenado sin justificación | Proveedores |
| RN-002 | Toda recepción actualiza Inventario automáticamente | Inventario |
| RN-003 | Un proveedor inactivo no puede recibir nuevas órdenes | Proveedores |

---

## 5. Casos de uso principales

### CU-001: Ciclo completo de compra

**Actor principal:** Administrador  
**Precondición:** Stock bajo en algún producto

**Flujo principal:**
1. Administrador revisa stock bajo
2. Selecciona proveedor y crea orden de compra
3. Envía orden al proveedor (fuera del sistema)
4. Recibe mercancía
5. Registra recepción en el sistema
6. Sistema actualiza stock
7. Orden queda cerrada

**Flujos alternos:**
- 5a. Cantidad recibida diferente → registrar diferencia, contactar proveedor
- 5b. Producto dañado → registrar rechazo, no aumentar stock

**Postcondición:** Stock actualizado, orden cerrada

---

## 6. Contratos con otros módulos

| Módulo origen | Módulo destino | Qué se envía | Cuándo |
|:---|:---|:---|:---|
| Proveedores | Inventario | ID orden, ID producto, cantidad recibida | Al registrar recepción |
| Inventario | Proveedores | Alerta de stock bajo | Cuando stock &lt; mínimo |
| Proveedores | Análisis | Órdenes, tiempos de entrega | Batch |

---

## 7. Pendientes / Por definir

- [ ] ¿Comparador de precios entre proveedores?
- [ ] ¿Evaluación de desempeño de proveedores?
- [ ] INCONSISTENCIA: El flujo alterno contempla cantidades recibidas diferentes a la orden, pero la definición original de estados no distinguía una recepción parcial. Se propone el estado "parcial" y queda pendiente su validación.