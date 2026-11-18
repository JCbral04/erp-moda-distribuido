# FASHION INTELLIGENCE — DESIGN SYSTEM SPECIFICATION
## ERP Moda Distribuido · Sistema de Diseño v1.0

---

### 1. Filosofía & Visión de Marca

**Fashion Intelligence** no es una plantilla genérica de administración ni una aplicación cosmética superficial. Es la infraestructura operativa digital para casas de moda de alta gama, ateliers y minoristas de confección premium.

El lenguaje visual combina:
- **Elegancia y Precisión:** Tipografía editorial (*Instrument Serif*), tonos de superficie profundos (`#090A0C` base), y detalles en oro champagne (`#D6B77A`) aplicados con disciplina artesanal (≤ 15% de cobertura visual).
- **Rigor Operativo:** Tipografía monoespaciada de alta legibilidad (*Geist Mono*) para todos los valores cuantitativos con `font-variant-numeric: tabular-nums`, métricas financieras asimétricas, y jerarquía visual estricta.
- **Rendimiento y Ligereza:** Transiciones acotadas exclusivamente a `transform` y `opacity`, cero dependencias duplicadas, renderizado instantáneo y accesibilidad universal (WCAG AA).

---

### 2. Tokens de Diseño (`src/design/tokens.ts`)

#### 2.1 Superficies y Fondos
| Token | Valor Hex/RGBA | Semántica de Uso |
| :--- | :--- | :--- |
| `bg.base` | `#090A0C` | Fondo raíz de la ventana y viewport general |
| `bg.surface` | `#111317` | Sidebar lateral, header superior y contenedores primarios |
| `bg.raised` | `#191C21` | Tarjetas KPI elevadas, headers de tabla y estados activos |
| `bg.overlay` | `#1E2229` | Modales, flyouts, dropdowns y tooltips flotantes |

#### 2.2 Bordes
| Token | Valor RGBA | Semántica de Uso |
| :--- | :--- | :--- |
| `border.subtle` | `rgba(255,255,255,0.06)` | Líneas divisoras sutiles, bordes de tarjeta estándar |
| `border.default` | `rgba(255,255,255,0.10)` | Contornos de botones secundarios y campos de formulario |
| `border.strong` | `rgba(255,255,255,0.18)` | Separadores de sección destacados y estados interactivos |

#### 2.3 Tipografía de Color
| Token | Valor Hex | Semántica de Uso |
| :--- | :--- | :--- |
| `text.primary` | `#F5F1E9` | Títulos, valores de KPI y texto preponderante (contraste alto) |
| `text.secondary` | `#92949B` | Etiquetas secundarias, descripciones y placeholders activos |
| `text.tertiary` | `#5A5C63` | Categorías en mayúsculas, leyendas y timestamps auxiliares |
| `text.inverse` | `#090A0C` | Texto dentro de botones de acento dorado |

#### 2.4 Acentos y Estado Semántico
| Token | Valor Hex | Semántica de Uso |
| :--- | :--- | :--- |
| `accent.champagne` | `#D6B77A` | Acento principal de marca (botones primarios, cursor activo) |
| `accent.champagne2`| `#A58B62` | Tonalidad muted champagne para bordes o estados hover |
| `accent.danger` | `#E05252` | Alertas de stock crítico, errores y anulaciones |
| `accent.success` | `#52C07A` | Ventas confirmadas, métricas positivas y confirmaciones |
| `accent.warning` | `#D4A035` | Advertencias de inventario moderado y estados pendientes |
| `accent.info` | `#5B9BD5` | Información del sistema y notas operativas |

---

### 3. Reglas Tipográficas Obligatorias

1. **Instrument Serif:**
   - Exclusivo para: Logo corporativo, titulares editoriales de sección (`<h1>`), saludo contextual y marca del sistema.
   - **PROHIBIDO:** En datos numéricos, precios, tablas, campos de formulario o botones.
2. **Geist UI:**
   - Familia de trabajo para toda la interfaz gráfica: etiquetas de navegación, contenido de tarjetas, botones y modales.
3. **Geist Mono:**
   - Obligatoria para: Precios, SKUs, códigos de referencia, fechas de transacción y cantidades.
   - Siempre acompañada de `font-variant-numeric: tabular-nums` para alineación columnar vertical estricta.
4. **Equilibrio de texto:**
   - `text-wrap: balance` en encabezados y títulos de más de una línea.

---

### 4. Directrices de Movimiento y Animaciones

- **Propiedades animables:** Estrictamente `transform` y `opacity`. Queda prohibido animar `width`, `height`, `top`, `left`, `margin` o `color` (excepto la transición de ancho del sidebar especificada en el requerimiento).
- **Duraciones máximas:** Ninguna animación excede los 400ms.
- **Entrada escalonada (Stagger):**
  - Header: Inmediato (0ms).
  - Tarjetas KPI: 50ms de retardo acumulado por tarjeta (`opacity 0→1`, `translateY(8px)→0`).
  - Analítica y actividades: 250ms y 320ms de retardo respectivamente.
- **Micro-interacciones:**
  - Hover en cards: `transform: translateY(-2px)` en 150ms.
  - Hover en botones principales: `transform: scale(1.02)` en 80ms.
- **Accesibilidad motriz:** Respeto incondicional de `@media (prefers-reduced-motion: reduce)` con duración e interpolación fijadas en 0ms.

---

### 5. Prohibiciones Visuales (Anti-AI Slop)

1. Cero gradientes chillones o purpura/magenta tipo web3.
2. Cero bordes con resplandores luminosos falsos (*glow effects*) alrededor de tarjetas.
3. Máximo un elemento con desenfoque de fondo (*glassmorphism*) por pantalla (el Header sticky con `backdrop-filter: blur(12px)`).
4. Ninguna cuadrícula uniforme aburrida: jerarquía asimétrica con ratio 60/40 en analítica y distribución auto-fill en KPIs.
5. Iconografía exclusivamente semántica de Lucide React, sin iconos decorativos flotantes sin sentido.
6. Acento champagne limitado a ≤ 15% de la superficie visual para mantener exclusividad y alta gama.

---

### 6. Cumplimiento de Accesibilidad (WCAG AA)

- **Skip Link:** Elemento accesible `#main` posicionado como primer foco ejecutable por teclado.
- **Navegación por Teclado:** Flechas arriba/abajo soportadas en sidebar, escape en modal y shortcut global `⌘K` / `Ctrl+K`.
- **Contraste de Color:** Todos los pares texto/fondo superan el ratio 4.5:1 para texto normal y 3:1 para texto grande.
- **Etiquetas y Roles:** Todo botón con icono dispone de `aria-label`; inputs con `<label>` vinculado mediante `htmlFor`; estados de error con `role="alert"`.
