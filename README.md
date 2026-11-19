# erp-moda-distribuido
Sistema ERP distribuido para la gestión integral de tiendas de moda — Proyecto de Sistemas Distribuidos, Universidad Manuela Beltrán

## Estructura del repositorio

    erp-moda-distribuido/
    ├── backend/       Solución .NET 8 (ErpModa.sln + ErpModa.Api) — módulos del ERP
    ├── ai/            Microservicio Python + OpenCV — detección de prendas por cámara
    ├── frontend/      React + TypeScript (Vite) — interfaz y Dashboard BI
    └── docs/          Documentación: decisiones, requerimientos, contratos

## Cómo abrir el proyecto

- **Visual Studio (Windows):** abrir `backend/ErpModa.sln`
- **VS Code (Linux/Windows/macOS):** abrir la carpeta raíz y compilar con `dotnet build backend/ErpModa.sln`

## Configuración y autenticación

La conexión a la base de datos y la autenticación JWT se configuran **fuera del
repositorio** (User Secrets en desarrollo, variables de entorno en producción).
Consulta [`backend/ErpModa.Api/SETUP_INSTRUCTIONS.md`](backend/ErpModa.Api/SETUP_INSTRUCTIONS.md)
para:

- Configurar `Jwt__Key` y `Auth__BootstrapToken`.
- Aplicar los scripts SQL manuales (incluido `docs/seguridad/usuarios-schema.sql`).
- Crear el primer administrador con `POST /api/auth/inicializar` y emitir JWTs con
  `POST /api/auth/login`.

No hay migraciones EF Core: las tablas se crean con los scripts SQL de `docs/`.
