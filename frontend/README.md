# ERP Moda — Frontend (Fashion Intelligence)

Plataforma web de gestión para tiendas y ateliers de moda desarrollada con React 19, TypeScript y Vite.

- **Issue de referencia:** [#11](https://github.com/JCbral04/erp-moda-distribuido/issues/11)
- **Requerimientos y análisis:** [analisis-dashboard.md](../docs/requerimientos/analisis-dashboard.md)
- **Sistema de diseño:** [tokens.ts](src/design/tokens.ts) y [DESIGN.md](src/design/DESIGN.md)

## Arquitectura y Módulos
- `src/design/`: Tokens de diseño centralizados (paleta dark luxury, tipografía Instrument Serif / Geist / Geist Mono, espaciado, radios y curvas de animación).
- `src/components/ui/`: Componentes base accesibles (Button, Input, Card, Table, Skeleton, EmptyState).
- `src/components/charts/`: Wrappers de analítica con Recharts (`SalesAreaChart`, `SparkChart`).
- `src/components/layout/`: AppLayout, Sidebar colapsable con accesibilidad por teclado y Header superior con disparador `⌘K`.
- `src/pages/`: Dashboard asimétrico con KPIs de ventas, productos y alertas de inventario.
- `src/api/`: Cliente HTTP tipado y endpoints alineados con los DTOs del backend.

## Comandos
- `npm run dev`: Servidor de desarrollo con HMR.
- `npm run build`: Verificación de tipos TypeScript (`tsc -b`) y empaquetado Vite.
- `npm run lint`: Linter con Oxlint.
