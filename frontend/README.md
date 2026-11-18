# ERP Moda — Frontend (Fashion Intelligence)

Plataforma web de gestión para tiendas y empresas de moda desarrollada con React 19, TypeScript y Vite.

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

## Configuración de Entorno (`VITE_API_URL`)

El cliente HTTP utiliza la variable de entorno `VITE_API_URL` para conectarse a la API de backend:

| Entorno | Valor recomendado | Descripción |
| :--- | :--- | :--- |
| **Desarrollo local** | `http://localhost:5286/api` | Valor por defecto en código si no se define `.env`. Conecta directo con la API local de ASP.NET Core. |
| **Producción / Docker** | `https://api.erpmoda.com/api` o `/api` | Ruta absoluta del servidor de producción o relativa si se utiliza un proxy inverso (ej. Nginx / Traefik). |

Para personalizar la variable en desarrollo:
1. Copia el archivo de ejemplo:
   ```bash
   cp .env.example .env
   ```
2. Modifica el valor de `VITE_API_URL` según el puerto o dominio activo.

## Comandos
- `npm run dev`: Servidor de desarrollo Vite con Hot Module Replacement (HMR) en `http://localhost:5173`.
- `npm run build`: Verificación de tipos TypeScript (`tsc -b`) y empaquetado optimizado para producción en `dist/`.
- `npm run lint`: Análisis estático y linter ultrarrápido con Oxlint.
