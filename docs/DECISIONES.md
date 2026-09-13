# DECISIONES.md — Acuerdos del equipo

Proyecto: erp-moda-distribuido — Sistema ERP distribuido para tiendas de moda
Equipo: Juan Esteban Cabral Bautista, Jair Enrique Polo Chamorro, Andres Felipe Vargas Serrato

## 1. Stack tecnológico

- **Backend:** .NET 8 (ASP.NET Core Web API)
- **Módulo de IA:** Python + OpenCV, como microservicio independiente que se comunica con Inventario por HTTP
- **Frontend:** React + TypeScript (Vite)
- **Base de datos:** PostgreSQL
- **Caché/colas:** Redis
- **Infraestructura local:** Docker + Docker Compose

*Decisión tomada el 11/09/2026: se elige .NET 8 para el backend por su tipado fuerte, su ecosistema empresarial maduro para sistemas ERP, su soporte multiplataforma (el equipo trabaja en Linux y Windows) y su relevancia en el mercado laboral. La IA permanece en Python como microservicio independiente, lo que refuerza la naturaleza distribuida del sistema. Esta decisión se ratificará al inicio del Sprint 1 una vez aprobados los requerimientos.*

## 1.1 Proceso de decisión de stack

- Levantamiento de requerimientos (Sprint 0).
- Evaluación de alternativas con base en los requerimientos (inicio del Sprint 1).
- Ratificación o ajuste de la decisión en este documento.

## 2. División de módulos

| Integrante | Módulos |
|---|---|
| Juan Esteban (líder) | Arquitectura, integración, Dashboard BI |
| Jair | Inventario (núcleo) + IA |
| Andres | Ventas, Facturación, Proveedores, Análisis |

Cada quien es dueño del código de sus módulos: sus PRs los revisa otro, y los PRs de otros que toquen sus módulos los revisa él.

## 3. Convención de ramas

- `main` — protegida, solo entra por PR aprobado
- `feature/nombre-corto` — funcionalidades nuevas
- `fix/nombre-corto` — corrección de errores
- `docs/nombre-corto` — documentación
- `chore/nombre-corto` — configuración y mantenimiento

## 4. Convención de commits

`tipo: descripción corta en presente`

Tipos: `feat`, `fix`, `docs`, `chore`, `test`, `refactor`

Ejemplo: `feat: agregar endpoint de registro de ventas`

## 5. Reglas de trabajo

- Todo cambio a main entra por PR con **1 aprobación** mínima
- Los PRs se vinculan a su tarjeta con `Closes #N` en la descripción
- Máximo 1 tarjeta en "En curso" por persona
- Nada pasa más de 2 días en "En revisión"
- Daily corta por WhatsApp/llamada: qué hice, qué haré, qué me bloquea
- Compromiso de horas semanales por persona: [acordar en la llamada]

## 6. Contratos antes que implementación

Antes de integrar dos módulos, se define primero el contrato (endpoint, formato de datos) en este documento o en docs/. Nadie implementa contra suposiciones.

## 7. Seguridad de la cadena de suministro (issue #20)

- **Dependabot alerts + security updates:** activados en Settings — alertas de CVEs y PRs automáticos con parches
- **CodeQL (default setup):** análisis estático de C# y Python en cada push/PR a `main` + escaneo semanal
- **Secret scanning + push protection:** bloquea secretos *antes* de que entren al historial (protege mientras migramos las credenciales del compose a `.env`)
- **`dependabot.yml`:** actualizaciones semanales (lunes), máx. 5 PRs abiertos por ecosistema, sin reviewers fijos (el equipo rota), sin agrupar (un PR por dependencia para identificar qué rompe)
- **Ecosistemas:** NuGet (`/backend`), pip (`/ai`), GitHub Actions (`/`). Pendiente: npm (`/frontend`) cuando se cierre el issue #11
- Los PRs de Dependabot pasan por la protección de `main` como cualquier PR (1 aprobación)
- El check de CodeQL se incluirá en "Require status checks" cuando se active (pendiente del issue #3)

*Decisión tomada el 12/09/2026: el sistema ya tiene 3 ecosistemas de dependencias (pronto 4 con el frontend) y sin gestión de vulnerabilidades un CVE puede comprometer todo el sistema distribuido. Se elige la configuración por defecto/automática en todo lo posible: en un equipo pequeño, la mejor seguridad es la que funciona sin mantenimiento manual.*