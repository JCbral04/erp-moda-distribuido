# Requerimientos — Análisis y Dashboard BI

**Responsable:** Andres Felipe Vargas Serrato  
**Fecha:** 10/09/2026  
**Estado:** Borrador

---

## 1. Descripción del módulo

Módulo de inteligencia de negocios. Consume datos de Ventas, Inventario y Proveedores para generar reportes, métricas y visualizaciones que apoyen la toma de decisiones.

---

## 2. Actores

| Actor | Descripción |
|:---|:---|
| Administrador | Visualiza reportes y toma decisiones |
| Gerente | Revisa métricas de desempeño |

---

## 3. Historias de usuario

### HU-001: Dashboard de ventas del día

**Como** Administrador  
**Quiero** ver un resumen de las ventas del día en tiempo real  
**Para** monitorear el desempeño del negocio

**Criterios de aceptación:**
- [ ] Total vendido hoy, comparado con ayer y promedio semanal
- [ ] Número de transacciones
- [ ] Productos más vendidos del día
- [ ] Gráfico de ventas por hora

---

### HU-002: Reporte de inventario

**Como** Administrador  
**Quiero** ver el estado actual del inventario  
**Para** identificar productos agotados o de baja rotación

**Criterios de aceptación:**
- [ ] Lista de productos con stock actual
- [ ] Alertas de stock bajo (&lt; 5 unidades)
- [ ] Productos sin movimiento en 30 días
- [ ] Valor total del inventario

---

### HU-003: Análisis de ventas por período

**Como** Gerente  
**Quiero** analizar ventas por semana, mes o trimestre  
**Para** identificar tendencias y planificar

**Criterios de aceptación:**
- [ ] Gráfico de ventas por período seleccionado
- [ ] Comparación con período anterior
- [ ] Top 10 productos más vendidos
- [ ] Ventas por categoría o tipo de prenda

---

### HU-004: Métricas de proveedores

**Como** Administrador  
**Quiero** ver métricas de desempeño de proveedores  
**Para** evaluar y negociar con ellos

**Criterios de aceptación:**
- [ ] Tiempo promedio de entrega por proveedor
- [ ] Porcentaje de órdenes completas vs parciales
- [ ] Total gastado por proveedor

---

## 4. Reglas de negocio

| ID | Regla | Módulo afectado |
|:---|:---|:---|
| RN-001 | Los datos del dashboard se actualizan cada 5 minutos | Dashboard |
| RN-002 | Los reportes históricos no cambian una vez generados | Análisis |
| RN-003 | Solo administradores pueden ver métricas de costos | Dashboard |

---

## 5. Casos de uso principales

### CU-001: Revisión diaria del negocio

**Actor principal:** Administrador  
**Precondición:** Hay ventas registradas

**Flujo principal:**
1. Administrador abre el dashboard
2. Ve resumen de ventas del día
3. Revisa alertas de stock bajo
4. Identifica productos más vendidos
5. Toma decisiones (ej: hacer pedido a proveedor)

**Postcondición:** Decisiones informadas

---

## 6. Contratos con otros módulos

| Módulo origen | Módulo destino | Qué se envía | Cuándo |
|:---|:---|:---|:---|
| Ventas | Análisis | Datos de ventas | Tiempo real o batch |
| Inventario | Análisis | Niveles de stock, movimientos | Tiempo real o batch |
| Proveedores | Análisis | Órdenes, tiempos de entrega | Batch |
| Análisis | Dashboard | Métricas calculadas | Cada 5 minutos |

---

## 7. Pendientes / Por definir

- [ ] ¿Exportar reportes a PDF/Excel?
- [ ] ¿Alertas automáticas por correo?
- [ ] ¿Predicción de demanda con ML?
