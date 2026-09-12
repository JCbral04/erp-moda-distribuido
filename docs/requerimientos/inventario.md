# Requerimientos — Inventario

**Responsable:** Jair Enrique Polo Chamorro  
**Fecha:** 10/09/2026  
**Estado:** Borrador

---

## 1. Descripción del módulo

Núcleo del ERP. Gestiona productos, variantes (talla/color), stock y movimientos de entrada/salida. Se integra con Ventas (descuenta stock), Proveedores (aumenta stock) e IA (detección automática de prendas).

---

## 1.1 Modelo de producto y variante

El catálogo se organiza en dos niveles: el **producto** (la prenda en general) y sus **variantes** (cada combinación de talla y color con stock propio).

Ejemplo:

```text
Producto
├── Camiseta básica
├── Categoría: Camisetas
└── Variantes
    ├── Negro / S
    ├── Negro / M
    ├── Negro / L
    ├── Blanco / S
    └── Blanco / M
```

Reglas del modelo:

- Cada variante tiene su propio stock, SKU e identificador único
- El precio se define en el producto; las variantes pueden tener sobreprecio opcional
- Ventas siempre descuenta stock de una variante específica, nunca del producto general
- La IA identifica el producto por visión; la variante exacta se confirma manualmente o por código

---

## 2. Actores

| Actor | Descripción |
|:---|:---|
| Administrador | Dueño/gerente de la tienda, acceso total |
| Vendedor | Empleado que registra ventas y consulta stock |
| Sistema IA | Cámara que detecta prendas automáticamente |

---

## 3. Historias de usuario

### HU-001: Registrar producto nuevo

**Como** Administrador  
**Quiero** registrar un producto con su nombre, descripción, precio y variantes  
**Para** tenerlo disponible en el catálogo de venta

**Criterios de aceptación:**
- [ ] El producto guarda: nombre, descripción, precio base, categoría
- [ ] Se pueden agregar variantes (talla, color) con stock independiente
- [ ] El producto aparece en el listado de productos activos

---

### HU-002: Consultar stock disponible

**Como** Vendedor  
**Quiero** ver el stock actual de un producto por talla y color  
**Para** informar al cliente si hay disponibilidad

**Criterios de aceptación:**
- [ ] Muestra cantidad disponible por variante
- [ ] Indica si está agotado o por agotarse (&lt; 5 unidades)

---

### HU-003: Registrar entrada de mercancía

**Como** Administrador  
**Quiero** registrar la llegada de productos de un proveedor  
**Para** actualizar el stock y mantener trazabilidad

**Criterios de aceptación:**
- [ ] Se selecciona proveedor y productos recibidos
- [ ] El stock aumenta automáticamente
- [ ] Queda registro del movimiento con fecha y usuario

---

### HU-004: Ajuste manual de stock

**Como** Administrador  
**Quiero** ajustar el stock de un producto (daños, pérdidas, conteo físico)  
**Para** mantener la información precisa

**Criterios de aceptación:**
- [ ] Se puede aumentar o disminuir stock con justificación
- [ ] Queda registro del ajuste con motivo y usuario

---

## 4. Reglas de negocio

| ID | Regla | Módulo afectado |
|:---|:---|:---|
| RN-001 | No se puede vender un producto sin stock disponible | Ventas |
| RN-002 | Todo movimiento de stock debe quedar registrado (auditoría) | Inventario |
| RN-003 | Un producto puede tener múltiples variantes (talla/color) | Inventario |
| RN-004 | La IA puede agregar o quitar stock automáticamente | IA |
| RN-005 | Stock negativo no permitido | Inventario |

---

## 5. Casos de uso principales

### CU-001: Venta de producto

**Actor principal:** Vendedor  
**Precondición:** El producto existe y tiene stock

**Flujo principal:**
1. Vendedor busca producto por código o nombre
2. Selecciona variante (talla/color)
3. Ingresa cantidad
4. Sistema verifica stock disponible
5. Sistema descuenta stock
6. Sistema registra la venta

**Flujos alternos:**
- 4a. Stock insuficiente → mensaje de error, no se confirma venta
- 4b. Producto agotado → sugerir productos similares

**Postcondición:** Stock actualizado, venta registrada

---

### CU-002: Entrada de mercancía

**Actor principal:** Administrador  
**Precondición:** Existe orden de compra al proveedor

**Flujo principal:**
1. Administrador selecciona orden de compra
2. Confirma productos y cantidades recibidas
3. Sistema aumenta stock automáticamente
4. Sistema registra movimiento de entrada

**Flujos alternos:**
- 2a. Cantidad recibida diferente a la orden → registrar diferencia y notificar

**Postcondición:** Stock actualizado, movimiento registrado

---

## 6. Contratos con otros módulos

| Módulo origen | Módulo destino | Qué se envía | Cuándo |
|:---|:---|:---|:---|
| Ventas | Inventario | ID producto, ID variante, cantidad | Al confirmar venta |
| Proveedores | Inventario | ID orden, ID producto, cantidad recibida | Al registrar entrada |
| IA | Inventario | ID producto detectado, acción (+1/-1), confianza | Detección automática |
| Inventario | Análisis | Niveles de stock, movimientos | Tiempo real o batch |

---

## 7. Pendientes / Por definir

- [ ] ¿Cómo manejar devoluciones? ¿Aumentan stock automáticamente?
- [ ] ¿Alertas de stock bajo por correo o solo en dashboard?
- [ ] ¿Código de barras/QR para productos