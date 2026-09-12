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
