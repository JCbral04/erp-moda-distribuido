# Requerimientos — Módulo de IA (Visión por Computador)

**Responsable:** Jair Enrique Polo Chamorro  
**Fecha:** 10/09/2026  
**Estado:** Borrador

---

## 1. Descripción del módulo

Microservicio independiente en Python que usa visión por computador para detectar prendas frente a una cámara. Se comunica con Inventario por HTTP para agregar o quitar stock automáticamente.

---

## 2. Actores

| Actor | Descripción |
|:---|:---|
| Sistema IA | Cámara + modelo de detección (actor automático) |
| Administrador | Configura y monitorea el sistema |

---

## 3. Historias de usuario

### HU-001: Detectar prenda en cámara

**Como** Sistema IA  
**Quiero** detectar cuando una prenda está frente a la cámara  
**Para** registrar su entrada o salida del inventario

**Criterios de aceptación:**
- [ ] La cámara captura video en tiempo real
- [ ] El modelo detecta la presencia de una prenda
- [ ] Se clasifica el tipo de prenda (camiseta, pantalón, etc.)
- [ ] Se envía evento a Inventario con ID de producto y acción (+1 o -1)

---

### HU-002: Calibrar sistema de detección

**Como** Administrador  
**Quiero** calibrar la cámara y el modelo para mi tienda  
**Para** mejorar la precisión de detección

**Criterios de aceptación:**
- [ ] Se puede capturar imágenes de referencia
- [ ] Se ajustan parámetros de detección (umbral, ROI)
- [ ] Se prueba la detección en tiempo real

---

### HU-003: Revisar log de detecciones

**Como** Administrador  
**Quiero** ver el historial de detecciones de la IA  
**Para** verificar que está funcionando correctamente

**Criterios de aceptación:**
- [ ] Lista de detecciones con timestamp, tipo de prenda, acción
- [ ] Filtros por fecha y tipo
- [ ] Marcar detecciones como correctas o incorrectas (feedback)

---

## 4. Reglas de negocio

| ID | Regla | Módulo afectado |
|:---|:---|:---|
| RN-001 | La IA no puede dejar stock negativo | Inventario |
| RN-002 | Toda detección debe quedar registrada (auditoría) | IA |
| RN-003 | Detecciones con baja confianza requieren confirmación manual | IA |

---

## 5. Casos de uso principales

### CU-001: Detección automática de entrada

**Actor principal:** Sistema IA  
**Precondición:** Cámara encendida, modelo cargado

**Flujo principal:**
1. Cámara detecta movimiento
2. Modelo analiza frame y detecta prenda
3. Clasifica tipo de prenda
4. Consulta ID de producto en Inventario
5. Envía evento de entrada (+1 stock)
6. Registra detección en log

**Flujos alternos:**
- 2a. No detecta prenda → ignorar
- 3a. Baja confianza → marcar para revisión manual
- 4a. Producto no encontrado → alertar al administrador

**Postcondición:** Stock actualizado, detección registrada

---

## 6. Contratos con otros módulos

| Módulo origen | Módulo destino | Qué se envía | Cuándo |
|:---|:---|:---|:---|
| IA | Inventario | ID producto detectado, acción (+1/-1), confianza | Detección automática |
| Inventario | IA | Catálogo de productos para clasificación | Al iniciar o actualizar |

---

## 7. Pendientes / Por definir

- [ ] ¿Qué modelo usar? (YOLO, EfficientDet, custom)
- [ ] ¿Cámara IP(Generalmente Usada para vigilancia) o USB(Camara que se conecta directamente a un computador)?
- [ ] ¿Procesamiento local o en la nube?
- [ ] ¿Entrenamiento con fotos propias de la tienda?