# Contrato propuesto — Ventas → Inventario

**Propuesta:** Andres Felipe Vargas Serrato (Ventas)  
**Destinatario:** Jair Enrique Polo Chamorro (Inventario)  
**Estado:** Propuesta — NO implementado. Se define el contrato antes de escribir la integración (ver `docs/DECISIONES.md`, sección 6: *"Contratos antes que implementación"*).

---

## 1. Objetivo

Definir cómo el módulo de Ventas pedirá a Inventario validar y descontar stock al **confirmar** una venta, y restaurarlo al **anularla**. Este documento es la propuesta de contrato; la implementación queda pendiente de acuerdo del equipo.

## 2. Contexto

- Ventas actualmente valida sus propios datos (detalles, cantidades, precios, método de pago) y pasa a `Confirmada` **sin** tocar Inventario.
- Inventario sigue en memoria (`IProductosService`/`ProductosService`) y aún no expone endpoints de stock, pero su esquema (`docs/inventario/schema.sql`) ya prevé registrar estos movimientos en `movimientos_stock`:
  - `tipo='venta'`, `cantidad < 0` (salida), `referencia_tipo='venta'`, `referencia_id = VentaId`.
  - `tipo='anulacion_venta'`, `cantidad > 0` (entrada), `referencia_tipo='venta'`, `referencia_id = VentaId`.
- Ventas opera siempre a nivel de **variante** (nunca de producto general), consistente con Inventario (`docs/requerimientos/inventario.md`, sección 1.1).

## 3. Cuándo se invoca

| Momento | Acción de stock |
|:---|:---|
| `POST /api/ventas/{id}/confirmar` | Validar stock disponible y descontar (salida) |
| `POST /api/ventas/{id}/anular` | Restaurar stock (entrada) |

## 4. Propuesta de endpoint

Un único endpoint en el módulo de Inventario (dueño: Jair), con semántica de **validar y descontar en un solo paso** para evitar que Ventas confirme una venta que luego no se puede descontar:

```
POST /api/inventario/ventas/{ventaId}/descontar
Content-Type: application/json
```

### 4.1 Cuerpo de la petición

```json
{
  "detalles": [
    {
      "varianteId": 10,
      "cantidad": 2
    }
  ]
}
```

Campos mínimos del contrato:

| Campo | Tipo | Regla |
|:---|:---|:---|
| `ventaId` (en la ruta) | `int` | Id de la venta confirmada; se usará como `referencia_id` en `movimientos_stock` |
| `varianteId` | `int` | Variante sobre la que se opera el stock (`> 0`) |
| `cantidad` | `int` | Unidades a descontar (`> 0`; Inventario lo guardará como salida negativa) |

Regla adicional: la venta debe tener al menos un detalle (ya lo valida Ventas al crear; Inventario puede revalidarlo).

### 4.2 Respuestas

#### Operación exitosa — `200 OK`

```json
{
  "ventaId": 1,
  "stockValidado": true,
  "detalles": [
    { "varianteId": 10, "cantidad": -2, "stockResultante": 5 }
  ]
}
```

#### Variante inexistente — `404 Not Found`

```json
{
  "codigo": "variante_no_encontrada",
  "varianteId": 99,
  "mensaje": "La variante 99 no existe."
}
```

#### Stock insuficiente — `409 Conflict`

```json
{
  "codigo": "stock_insuficiente",
  "varianteId": 10,
  "disponible": 1,
  "solicitado": 2,
  "mensaje": "Stock insuficiente para la variante 10."
}
```

#### Petición inválida — `400 Bad Request`

Por ejemplo `cantidad <= 0`, `varianteId <= 0` o lista de detalles vacía. Mismo formato de error con `codigo` + `mensaje`.

## 5. Anulación de venta (restauración de stock)

Cuando se apruebe este contrato, `POST /api/ventas/{id}/anular` deberá invocar la operación inversa para reponer stock. Se propone reutilizar el mismo endpoint con un modo, o uno específico:

```
POST /api/inventario/ventas/{ventaId}/restaurar
```

Con el mismo cuerpo (`varianteId`, `cantidad`) y las mismas respuestas de error (`404` variante inexistente). La anulación de una venta ya conserva su `motivo` (que mapea a `movimientos_stock.motivo`, obligatorio para `anulacion_venta` según `schema.sql`).

## 6. Pendiente de acuerdo con el equipo

- Nombre y forma final del endpoint (ruta, dos modos vs. un endpoint por operación).
- **Atomicidad:** ¿cómo garantizan Ventas + Inventario que "confirmar venta" y "descontar stock" no queden a medias? Opciones: (a) Inventario ejecuta ambos pasos y Ventas trata el 404/409 como confirmación fallida; (b) confirmar la venta como último paso tras el descuento.
- **Idempotencia / reintentos:** si el descuento llega pero la respuesta se pierde (timeout), ¿cómo se detecta que ya se aplicó para no descontar dos veces?
- Granularidad: este contrato está a nivel de **variante**, como Inventario exige; el contrato de Proveedores aún tiene esa pregunta abierta (ver `docs/inventario/esquema-bd.md`, sección 5).
- `stockResultante` en la respuesta: derivable en Inventario, se propone incluirlo por trazabilidad pero es opcional.