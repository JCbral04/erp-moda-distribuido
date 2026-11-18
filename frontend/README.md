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

## Arranque local (frontend + backend)

El dashboard consume estos endpoints del backend: `GET /api/ventas`, `GET /api/ventas/resumen` y `GET /api/productos`.

### 1. Backend (ASP.NET Core, .NET 8)

```bash
# Base de datos (requiere POSTGRES_PASSWORD en el entorno)
docker compose up -d db

# API en el puerto que espera el proxy de Vite
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=erp_moda;Username=erpuser;Password=<POSTGRES_PASSWORD>" \
  dotnet run --project backend/ErpModa.Api --urls http://localhost:5286
```

Comprueba que responde `GET http://localhost:5286/api/ventas/resumen` (o Swagger en Development).

### 2. Frontend

```bash
cd frontend
npm ci
npm run dev
```

Abre `http://localhost:5173`. No hace falta crear `.env` para desarrollar en local.

### 3. Cómo funciona el proxy de Vite

`vite.config.ts` declara `server.proxy['/api']` con `target: http://localhost:5286` y `changeOrigin: true`. El navegador pide siempre rutas relativas `/api/...` al origen `http://localhost:5173`; Vite reenvía la petición al backend en el mismo proceso de desarrollo. Por eso **no se configura CORS** en el backend para este flujo y no deben habilitarse todos los orígenes. El proxy solo existe en `npm run dev`.

## Configuración de Entorno (`VITE_API_URL`)

El cliente HTTP (`src/api/client.ts`) usa `VITE_API_URL` si está definida y, si no, `/api`:

| Entorno | Valor recomendado | Descripción |
| :--- | :--- | :--- |
| **Desarrollo local** | `/api` (por defecto) | Vite redirige al backend local mediante el proxy. |
| **Producción con proxy inverso** | `/api` | API y frontend bajo el mismo origen (Nginx/Traefik). |
| **Producción en otro servidor/dominio** | `https://api.tudominio.com/api` | El navegador contacta directamente al API; el despliegue debe configurar una política CORS **restrictiva** con ese origen exacto. |

Para personalizarla, copia `.env.example` a `.env` y edítalo. En producción la variable se lee durante el build (`VITE_...` se sustituye en tiempo de compilación), así que debe definirse **antes** de `npm run build`; cambiar el `.env` después no afecta a `dist/`. Si el frontend se despliega en otro servidor, configura `VITE_API_URL` (o el proxy inverso delante del API) y, si hay dominios distintos, autoriza únicamente el origen del frontend en el backend.

## Comandos
- `npm run dev`: Servidor de desarrollo Vite con HMR en `http://localhost:5173` (proxy `/api` activo).
- `npm run build`: Verificación de tipos TypeScript (`tsc -b`) y empaquetado optimizado para producción en `dist/`.
- `npm run lint`: Análisis estático con Oxlint.
- `npm test`: Pruebas unitarias del dashboard con el test runner integrado de Node (sin dependencias extra).

## Estado y errores del dashboard

Cada servicio (`ventas/resumen`, `ventas`, `productos`) se carga de forma independiente con `Promise.allSettled`. Si uno falla se muestra un aviso con el nombre del servicio, los indicadores afectados muestran `N/D` y los servicios que sí respondieron siguen visibles. El botón «Sincronizar» vuelve a consultar los tres endpoints y refleja el resultado del nuevo intento. Las ventas que cuentan para facturación son únicamente las que el backend devuelve con estado `Confirmada` (`EstadoVenta.Confirmada`); pendientes y anuladas aparecen solo en «Actividad Reciente». El umbral de alerta de stock es `< 5` unidades (HU-002).
