# Solvas AM Modern Platform — Tech Stack Knowledge Base

## 1. Architecture Overview

The modern Solvas AM platform is a **micro-frontend + microservices** architecture that progressively replaces the Classic ASP monolith. It follows a clear separation:

- **Frontend**: React-based micro-frontends using Single-SPA, deployed as independent modules
- **Backend**: .NET microservices exposing gRPC APIs, fronted by BFF (Backend-for-Frontend) gateway services
- **Infrastructure**: Docker containers orchestrated by Kubernetes (via Helm charts), running on Azure (ACR for images)
- **Communication**: gRPC between services, REST/HTTP between frontend and BFF layer
- **Authentication**: Keycloak (primary) / Auth0 (alternative) for identity management
- **Database**: SQL Server with Entity Framework Core for new services, direct stored procedure access for legacy integration

---

## 2. Repository Map & Responsibilities

| Repository | Type | Role |
|-----------|------|------|
| `textile-shell` | Frontend | The shell/host application — orchestrates all micro-frontends |
| `textile-biz-components` | Frontend Library | Shared UI components for the Textile design system (charts, forms, PDF export) |
| `solvas-am-biz-components` | Frontend Library | Solvas AM domain-specific UI components (Monaco editor, grids, domain widgets) |
| `solvas-am-compliance-snapshots-ui` | Frontend Module | Compliance Snapshots micro-frontend |
| `solvas-am-compliance-modeling-ui` | Frontend Module | Compliance Modeling micro-frontend |
| `solvas-am-domaindata-ui` | Frontend Module | Domain Data management micro-frontend |
| `solvas-am-bff` | Backend (BFF) | Main AM Backend-for-Frontend — gateway for portfolio operations |
| `solvas-am-compliance-bff` | Backend (BFF) | Compliance-specific Backend-for-Frontend gateway |
| `solvas-am-portfolio-bff` | Backend (BFF) | Portfolio-specific Backend-for-Frontend gateway |
| `solvas-am-compliance-snapshots` | Backend Service | Compliance Snapshots domain microservice (owns snapshot data) |
| `solvas-am-compliance-modeling` | Backend Service | Compliance Modeling domain microservice (owns models/calculations) |
| `solvas-am-domaindata` | Backend Service | Domain Data microservice (reference data, lookup tables) |
| `solvas-am-portfolio-organizations` | Backend Service | Portfolio Organizations microservice (entities, deal structure) |
| `solvas-am-common` | Backend Library | Shared .NET library consumed by all backend services |

---

## 3. Frontend Architecture (Detailed)

### 3.1 Micro-Frontend Framework: Single-SPA

The platform uses **Single-SPA** to compose multiple independently deployable frontend modules into a single user experience.

**How it works:**
- `textile-shell` is the **root application** — it boots the Single-SPA framework, mounts the navigation bar, app bar, and routes to child micro-frontends
- Each micro-frontend (snapshots-ui, modeling-ui, domaindata-ui, biz-components) registers itself as a Single-SPA application
- Each module is built as a standalone webpack bundle and served from its own container (via nginx)
- At runtime, the shell dynamically loads modules using SystemJS based on the current route
- Modules share React, React Router, and Textile libraries via import maps to avoid duplication

**Key benefits:**
- Independent deployment — each module can be released without rebuilding others
- Team autonomy — different teams can own different modules
- Incremental migration — new modules replace Classic ASP screens one at a time

### 3.2 Core Frontend Tech Stack

| Technology | Version | Purpose |
|-----------|---------|---------|
| **React** | 18.3.x | UI component framework |
| **TypeScript** | 5.5.x | Type-safe JavaScript |
| **React Router** | 6.x | Client-side routing |
| **Single-SPA** | 6.x | Micro-frontend orchestration |
| **Webpack** | 5.x | Module bundling (with `webpack-config-single-spa-react-ts`) |
| **MUI (Material UI)** | 6.x | Base component library (via `@mui/icons-material`) |
| **Zustand** | 5.x | Lightweight state management |
| **React Query (TanStack)** | 5.x | Server state management & caching (in biz-components) |
| **Axios** | 1.x | HTTP client for API communication |
| **AG Grid** | via `@textile/ag-grid` | Enterprise data grid for tabular displays |
| **Monaco Editor** | 0.55.x | Code editor (for SQL/expression editing in compliance) |
| **Chart.js / react-chartjs-2** | 4.x / 5.x | Data visualization & charts |
| **Yup** | 1.x | Form validation schema |
| **Formik** | 2.x | Form state management (in textile-biz-components) |
| **Day.js** | 1.x | Date manipulation |
| **Numeral.js** | 2.x | Number formatting |
| **PDFMake** | 0.2.x | Client-side PDF generation |
| **ExcelJS** | 4.x | Client-side Excel export |
| **Keycloak JS** | 25.x | Authentication client |
| **Auth0 SPA JS** | 2.x | Alternative authentication client |
| **sql-formatter** | 11.x | SQL formatting for display |
| **Lodash** | 4.x | Utility functions |
| **DOMPurify** | 3.x | HTML sanitization |

### 3.3 Testing Stack (Frontend)

| Technology | Purpose |
|-----------|---------|
| **Vitest** | Test runner (newer modules) |
| **Jest** | Test runner (older modules like textile-shell) |
| **Testing Library (React)** | Component testing utilities |
| **Testing Library (User Event)** | Simulating user interactions |
| **MSW (Mock Service Worker)** | API mocking for integration tests |
| **@mswjs/data** | Mock database for MSW handlers |
| **@faker-js/faker** | Test data generation |
| **jest-mock-extended** | Advanced mocking |
| **Happy DOM / JSDOM** | Virtual browser environments |

### 3.4 Code Quality (Frontend)

| Tool | Purpose |
|------|---------|
| **ESLint** | Linting (TypeScript + React rules) |
| **Prettier** | Code formatting |
| **Husky** | Git hooks (pre-commit linting/formatting) |
| **pretty-quick** | Run Prettier on staged files only |

### 3.5 Design System: Textile

The UI is built on a custom design system called **Textile**, composed of multiple layers:

1. **@textile/mui** — Base Material UI theme customizations and styled components
2. **@textile/common** — Shared utilities, types, and configurations
3. **@textile/runtime** — Runtime services (auth, config, module registration)
4. **@textile/ag-grid** — Themed AG Grid wrapper with standard behaviors
5. **@textile/biz-components** (`textile-biz-components` repo) — Higher-level business components (charts, PDF export, data tables, forms)
6. **@solvasam/bizcomponents** (`solvas-am-biz-components` repo) — AM-specific business components (compliance grids, snapshot viewers, etc.)

### 3.6 Documentation: Storybook

`textile-biz-components` includes **Storybook 8.x** for component documentation and visual development. This provides:
- Interactive component playground
- Design integration (`@storybook/addon-designs` for Figma links)
- Testing support (`@storybook/test`)

---

## 4. Backend Architecture (Detailed)

### 4.1 Service Architecture Pattern

Every backend service follows the same layered architecture:

```
┌─────────────────────────────┐
│     GrpcApi (Proto files)   │  ← Contract/interface definitions
├─────────────────────────────┤
│     Grpc (Host/Startup)     │  ← ASP.NET Core host, gRPC server setup
├─────────────────────────────┤
│     Core (Business Logic)   │  ← Domain services, handlers, validation
├─────────────────────────────┤
│  Infrastructure.EF (Data)   │  ← Entity Framework DbContext, migrations
├─────────────────────────────┤
│     Shared (DTOs/Models)    │  ← Shared types between layers
└─────────────────────────────┘
```

**Project breakdown per service:**

| Project Suffix | Purpose |
|---------------|---------|
| `.GrpcApi` | Protobuf `.proto` files defining the service contract. Generates C# server/client code. |
| `.Grpc` | The runnable ASP.NET Core application — configures DI, middleware, gRPC server hosting. |
| `.Core` | Business logic — service classes, domain models, validation, CQRS handlers. |
| `.Infrastructure.EntityFramework` | Database access — DbContext, entity configurations, EF Core migrations. |
| `.Shared` | Shared DTOs, constants, and models used across layers. |
| `.DataProtection.Cli` | CLI tool for managing data protection keys (encryption at rest). |
| `.DataSeeding.Cli` | CLI tool for seeding reference/initial data into the database. |
| `.DdxExpressions` | (Modeling only) Dynamic Data Expression engine. |

### 4.2 Core Backend Tech Stack

| Technology | Purpose |
|-----------|---------|
| **.NET 8** (via `fabric-dotnet/sdk:1.0.0` base image) | Runtime and SDK |
| **ASP.NET Core** | Web framework / gRPC hosting |
| **gRPC / Protocol Buffers** | Inter-service communication protocol |
| **Entity Framework Core** | ORM for database access (new domain services) |
| **SQL Server** | Database engine |
| **Dapper** (via Common.DbShim) | Lightweight DB access for stored procedure calls |
| **FluentValidation** | Request validation |
| **MediatR** (likely via Core projects) | CQRS/Mediator pattern |
| **AutoMapper** | Object-to-object mapping |
| **Serilog** | Structured logging |
| **Data Protection API** | Encryption at rest for sensitive config |

### 4.3 Shared Library: `solvas-am-common`

This NuGet package is consumed by ALL backend services. It provides:

| Package | Purpose |
|---------|---------|
| `SolvasAm.Common` | Base classes, interfaces, common utilities |
| `SolvasAm.Common.Analyzers` | Custom Roslyn analyzers enforcing coding standards |
| `SolvasAm.Common.Audit` | Audit trail infrastructure (who changed what, when) |
| `SolvasAm.Common.Caching` | Distributed caching abstractions |
| `SolvasAm.Common.Database` | Database connection management, multi-tenancy support |
| `SolvasAm.Common.DbShim` | Stored procedure execution layer (bridge to Classic DB) |
| `SolvasAm.Common.DdxExpressions` | Dynamic Data Expression evaluation engine |
| `SolvasAm.Common.Extensions` | Extension methods for common .NET types |
| `SolvasAm.Common.GrpcApi` | Shared protobuf definitions for cross-service communication |
| `SolvasAm.Common.JobQueue` | Background job queue abstraction |
| `SolvasAm.Common.JobQueue.SqlServer` | SQL Server-backed job queue implementation |
| `SolvasAm.Common.Models` | Shared domain models |
| `SolvasAm.Common.Queue` | Message queue abstractions |
| `SolvasAm.Common.SpecUtilities` | Test utilities and spec helpers |

### 4.4 BFF (Backend-for-Frontend) Pattern

The BFF services act as API gateways between the frontend and backend microservices:

**Structure per BFF:**
- `*.Bff.GrpcApi` — Defines the BFF's own gRPC contract (frontend-facing)
- `*.Bff.Grpc` — Hosts the BFF, routes requests to downstream services
- `*.Bff.Core` — Orchestration logic, aggregation, transformation
- `*.Bff.DataProtection.Cli` — Key management

**What BFFs do:**
- Aggregate data from multiple backend services into a single response
- Transform internal domain models into frontend-friendly shapes
- Handle authentication/authorization at the API boundary
- Provide a stable API contract even when backend services evolve
- Shield the frontend from knowing about internal service topology

### 4.5 Service Communication

```
Frontend (React)
    │
    │ HTTP/REST (via Axios)
    ▼
BFF Layer (solvas-am-bff, compliance-bff, portfolio-bff)
    │
    │ gRPC (Protocol Buffers)
    ▼
Domain Services (snapshots, modeling, domaindata, organizations)
    │
    │ Entity Framework Core / DbShim (Stored Procs)
    ▼
SQL Server Database (shared with Classic)
```

---

## 5. Infrastructure & DevOps

### 5.1 Containerization

| Component | Base Image | Registry |
|-----------|-----------|----------|
| Backend Services | `solvassharedservicesacr.azurecr.io/fabric-dotnet/sdk:1.0.0` (build) / `fabric-dotnet/aspnet:1.0.0` (runtime) | Azure Container Registry |
| Frontend Modules | `solvassharedservicesacr.azurecr.io/fabric/nginx-k8s:1.3.2` (static file serving) | Azure Container Registry |
| Frontend Build | Node.js-based Dockerfile-build (multi-stage) | Azure Container Registry |

**Build pattern (Backend):**
- Multi-stage Docker build
- Stage 1 (SDK): Restore packages, build, run tests, pack NuGet
- Stage 2 (Runtime): Copy built artifacts, configure entrypoint via Fabric init system
- BuildKit caching for NuGet packages

**Build pattern (Frontend):**
- Multi-stage Docker build
- Stage 1: `Dockerfile-build` — installs Node.js dependencies, runs webpack build
- Stage 2: `Dockerfile` — copies dist into nginx container for static serving

### 5.2 Orchestration: Kubernetes + Helm

Every service has a `helm/` directory containing Helm chart definitions for deployment:

- **Helm charts** define Kubernetes resources (Deployments, Services, Ingress, ConfigMaps, Secrets)
- **Stack configs** (`stack-config/`) provide local development overrides
- **Dev scripts** (`dev-up.sh`, `dev-down.sh`) manage local Kubernetes development stacks
- **Kube-test scripts** (`kube-test.sh`) run integration tests in Kubernetes

### 5.3 CI/CD Pipeline

| Aspect | Technology |
|--------|-----------|
| **CI Definition** | `codebase-ci.yml` (Azure DevOps Pipelines) |
| **Legacy CI** | `.gitlab-ci.yml` (GitLab CI — some repos still have this) |
| **Container Registry** | Azure Container Registry (`solvassharedservicesacr.azurecr.io`) |
| **Package Registry** | Private NuGet feed (for .NET packages), npm (for frontend packages) |
| **Helm Repository** | Helm chart repository for deployment artifacts |
| **Version Strategy** | Semantic versioning, with branch-based pre-release tags |

### 5.4 Development Workflow

Each repo follows the same local development pattern:

1. `dev-up.sh` — Stands up the full local development stack (Kubernetes pods for dependencies)
2. `stack-helper.sh` — Manages the local Kubernetes stack lifecycle
3. `update-helm-dependencies.sh` — Pulls latest dependency chart versions
4. Service runs locally (dotnet run / webpack serve) while dependencies run in K8s
5. `dev-down.sh` — Tears down the local stack

### 5.5 Secrets Management

- `.passwords` file (gitignored) stores local development secrets
- `passwords.template` provides the structure for new developers
- `DataProtection.Cli` tool manages encryption keys for each service
- Production secrets managed via Kubernetes Secrets + Azure Key Vault integration
- Connection strings encrypted with AES-256 (via `renew-service-certificate.sh`)

---

## 6. Database Strategy

### 6.1 Dual-Database Access Pattern

The modern services interact with SQL Server in two ways:

1. **Entity Framework Core** (for new domain data)
   - Services like `compliance-snapshots`, `compliance-modeling`, `domaindata`, `portfolio-organizations` have their own EF Core `Infrastructure.EntityFramework` project
   - They own their schema via Code-First migrations (`add-migration.sh`, `remove-migration.sh`)
   - Clean domain models mapped to tables via Fluent API configurations

2. **DbShim / Stored Procedures** (for legacy Classic data)
   - `SolvasAm.Common.DbShim` provides a thin layer over ADO.NET / Dapper
   - BFF services call Classic stored procedures directly to read/write data that lives in the legacy schema
   - This is the bridge pattern — new UI talks to BFF, BFF calls old stored procs

### 6.2 Migration Strategy

- New services: EF Core Code-First migrations
- Legacy data: Stays in Classic's SQL project (`App.Db`), migrated via SQL Migration Studio (`.sql-migration-studio.json` in Classic repo)
- Gradual extraction: Over time, data ownership moves from Classic stored procs to new services with proper domain models

### 6.3 Data Seeding

Services with `DataSeeding.Cli` projects can seed reference/initial data:
- `solvas-am-domaindata` — Seeds lookup tables, reference codes
- `solvas-am-portfolio-organizations` — Seeds organization structures
- `solvas-am-compliance-modeling` — Seeds calculation templates

---

## 7. Authentication & Authorization

### 7.1 Identity Providers

| Provider | Usage |
|---------|-------|
| **Keycloak** | Primary identity provider (open-source, self-hosted) |
| **Auth0** | Alternative/cloud-based identity provider |

The shell supports both via separate entry points:
- `textile-shell.tsx` — Keycloak authentication flow
- `textile-shell-auth0.tsx` / `root-auth0.component.tsx` — Auth0 authentication flow

### 7.2 Authentication Flow

1. User accesses the textile-shell URL
2. Shell checks for valid auth token via Keycloak JS / Auth0 SPA JS
3. If unauthenticated → redirected to Keycloak/Auth0 login page
4. After login → token returned to shell
5. Shell stores token and attaches it to all API requests (Axios interceptors)
6. BFF services validate tokens and extract user claims
7. Backend services receive user context via gRPC metadata

### 7.3 Authorization

- Role-based access control configured in Keycloak
- BFF layer enforces authorization policies
- Entity-level access restrictions from Classic DB (`cdosa_Entity_Access`) are respected

---

## 8. Detailed Repository Profiles

### 8.1 `textile-shell`

**Role:** The micro-frontend host/shell application. This is what the user actually loads in their browser.

**What it does:**
- Boots the Single-SPA framework
- Renders the top navigation bar (app bar) and side navigation (navbar)
- Registers and mounts child micro-frontends based on routes
- Manages global authentication state (Keycloak/Auth0)
- Provides the Textile runtime context to all child modules
- Handles top-level error boundaries and session management

**Key technologies:**
- React 18, TypeScript, Single-SPA 6
- Keycloak JS / Auth0 SPA JS for authentication
- Webpack with `webpack-config-single-spa-react-ts`
- Deployed as static files on nginx in Kubernetes

**Entry points:**
- `textile-shell.tsx` — Keycloak-based app bootstrap
- `textile-shell-auth0.tsx` — Auth0-based app bootstrap
- `textile-shell-shared-lib.ts` — Exports shared utilities to child modules

---

### 8.2 `textile-biz-components`

**Role:** Platform-level reusable business component library.

**What it provides:**
- Data visualization components (Chart.js wrappers)
- PDF export framework (PDFMake integration)
- Number formatting utilities (Numeral.js)
- Date handling utilities (date-fns)
- Advanced form components (Formik + validation)
- Flat/nested object utilities
- Platform-wide UX patterns

**Key technologies:**
- React 18, TypeScript
- Chart.js + react-chartjs-2 for data visualization
- PDFMake for client-side PDF generation
- Formik for form management
- Storybook 8 for component documentation
- Jest for testing
- Published as `@textile/biz-components` npm package

---

### 8.3 `solvas-am-biz-components`

**Role:** Solvas AM domain-specific shared business components.

**What it provides:**
- Monaco Editor integration (for SQL/expression editing in compliance configuration)
- Excel export (ExcelJS integration)
- Domain-specific grid configurations
- SQL formatting utilities
- AM-specific form patterns and validation
- Shared domain widgets used across snapshots-ui, modeling-ui, domaindata-ui

**Key technologies:**
- React 18, TypeScript
- Monaco Editor (`@monaco-editor/react`) for code editing
- ExcelJS for Excel generation
- TanStack React Query for server state
- Zustand for client state
- Yup for validation
- Vitest for testing
- Published as `@solvasam/bizcomponents` npm package

---

### 8.4 `solvas-am-compliance-snapshots-ui`

**Role:** The Portfolio Snapshots micro-frontend module.

**What it shows users:**
- Snapshot list with filtering and sorting
- Snapshot detail view (asset grid with all portfolio data)
- Snapshot comparison views
- Import/export snapshot data
- Snapshot generation controls

**Key technologies:**
- React 18, TypeScript, Vitest
- AG Grid (via `@textile/ag-grid`) for main data grid
- Zustand for state management
- Yup for validation
- React Number Format for numeric inputs
- Registers as Single-SPA module mounted by textile-shell
- Calls `solvas-am-compliance-bff` for data

---

### 8.5 `solvas-am-compliance-modeling-ui`

**Role:** The Compliance Modeling micro-frontend module.

**What it shows users:**
- Model management (create, copy, compare, lock)
- Calculation sequence configuration UI
- Compliance test configuration
- Test result viewing
- Waterfall/Priority of Payments configuration
- Dynamic data rule editing (with Monaco Editor for expressions)
- Rating derivation rule management

**Key technologies:**
- React 18, TypeScript, Vitest
- Monaco Editor for expression editing
- AG Grid for data displays
- Zustand for state
- Registers as Single-SPA module
- Calls `solvas-am-compliance-bff` for data

---

### 8.6 `solvas-am-domaindata-ui`

**Role:** The Domain Data management micro-frontend module.

**What it shows users:**
- Reference data management (lookup codes, industries, countries)
- Rating tables configuration
- Index/rate management
- Holiday calendar management
- System-wide configuration editing

**Key technologies:**
- React 18, TypeScript, Vitest
- AG Grid for tabular data editing
- Registers as Single-SPA module
- Calls `solvas-am-bff` or dedicated domain data endpoints

---

### 8.7 `solvas-am-bff` (Main BFF)

**Role:** Primary Backend-for-Frontend gateway for general AM operations.

**What it does:**
- Proxies and aggregates calls to multiple backend services
- Provides the main REST/gRPC interface for textile-shell and modules
- Handles general portfolio data operations
- Routes to domain services and Classic stored procedures

**Internal projects:**
- `SolvasAm.Bff.Core` — Business orchestration logic
- `SolvasAm.Bff.Grpc` — gRPC host application
- `SolvasAm.Bff.GrpcApi` — Proto definitions
- `SolvasAm.Bff.DataProtection.Cli` — Key management

**Tech:** .NET 8, ASP.NET Core, gRPC, Entity Framework Core, Dapper (DbShim)

---

### 8.8 `solvas-am-compliance-bff`

**Role:** Compliance-specific Backend-for-Frontend.

**What it does:**
- Serves compliance-related API calls for snapshots-ui and modeling-ui
- Aggregates compliance data from snapshots service and modeling service
- Orchestrates compliance operations (generate snapshot, run tests, etc.)
- Translates between frontend models and internal service contracts

**Internal projects:** Same pattern as main BFF (Core, Grpc, GrpcApi, DataProtection.Cli)

**Tech:** .NET 8, ASP.NET Core, gRPC

---

### 8.9 `solvas-am-portfolio-bff`

**Role:** Portfolio-specific Backend-for-Frontend.

**What it does:**
- Serves portfolio operations (asset management, transactions, trading)
- Routes to portfolio-organizations service and Classic DB
- Aggregates entity/position data

**Internal projects:** Same BFF pattern

**Tech:** .NET 8, ASP.NET Core, gRPC

---

### 8.10 `solvas-am-compliance-snapshots`

**Role:** Domain microservice owning all Portfolio Snapshot data.

**What it owns:**
- Snapshot storage and retrieval
- Snapshot generation logic
- Snapshot item (asset line) management
- Snapshot comparison engine
- Import/export of snapshot data

**Internal projects:**
- `*.Core` — Domain logic for snapshot operations
- `*.Infrastructure.EntityFramework` — EF Core data access + migrations
- `*.Grpc` — Service host
- `*.GrpcApi` — Proto contract definitions
- `*.Shared` — Shared DTOs
- `*.DataProtection.Cli` — Key management

**Tech:** .NET 8, Entity Framework Core, SQL Server, gRPC

---

### 8.11 `solvas-am-compliance-modeling`

**Role:** Domain microservice owning compliance modeling/calculation data.

**What it owns:**
- Compliance Models (state, versions, locking)
- Calculation Sequences and Blocks
- Compliance Test definitions and parameters
- Priority of Payments (waterfall) definitions
- Rating Derivation rules
- Par Build rules
- Dynamic Data Expressions (DDX)
- Test result storage

**Internal projects:**
- `*.Core` — Domain services for modeling operations
- `*.Infrastructure.EntityFramework` — EF Core data access
- `*.DdxExpressions` — Dynamic Data Expression evaluation engine
- `*.Grpc` — Service host
- `*.GrpcApi` — Proto definitions
- `*.Shared` — Shared types
- `*.DataSeeding.Cli` — Seed calculation templates

**Tech:** .NET 8, Entity Framework Core, SQL Server, gRPC, custom expression engine

---

### 8.12 `solvas-am-domaindata`

**Role:** Domain microservice owning reference/configuration data.

**What it owns:**
- Lookup types and codes
- Industry classifications
- Country data
- Rating agency tables
- Index types and tenors
- Calendar/holiday schedules
- System configuration values

**Internal projects:**
- `*.Core` — Domain logic
- `*.Infrastructure.EntityFramework` — EF Core with migrations
- `*.Ddx` — Domain data expression utilities
- `*.Grpc` — Service host
- `*.GrpcApi` — Proto contract
- `*.Shared` — Shared models
- `*.DataSeeding.Cli` — Seed reference data

**Tech:** .NET 8, Entity Framework Core, SQL Server, gRPC

**Extra:** Has a `dataInit/` folder with initial data sets for bootstrapping new environments.

---

### 8.13 `solvas-am-portfolio-organizations`

**Role:** Domain microservice owning deal/entity/organization structure.

**What it owns:**
- Deal definitions and configurations
- Entity (investor/tranche) management
- Organization hierarchies
- Account structures

**Internal projects:** Same domain service pattern (Core, EF, Grpc, GrpcApi, Shared, DataSeeding)

**Tech:** .NET 8, Entity Framework Core, SQL Server, gRPC

---

### 8.14 `solvas-am-common`

**Role:** Shared .NET library providing infrastructure and cross-cutting concerns.

**What it provides:**
- Database connection management and multi-tenancy
- DbShim for calling Classic stored procedures from modern services
- Audit trail infrastructure
- Caching abstractions
- Job queue framework (background processing)
- Custom Roslyn analyzers (enforce coding standards at compile time)
- Shared gRPC API definitions
- Domain models shared across services
- Expression evaluation engine (DdxExpressions)
- Test/spec utilities

**Published as:** Private NuGet packages consumed by all services

**Tech:** .NET 8, Dapper, ADO.NET, Roslyn (for analyzers)

---

## 9. Inter-Service Dependency Map

```
┌──────────────────────────────────────────────────────────────────┐
│                        FRONTEND LAYER                            │
│                                                                  │
│  textile-shell ─── loads ──┬── compliance-snapshots-ui           │
│       │                    ├── compliance-modeling-ui             │
│       │                    └── domaindata-ui                     │
│       │                                                          │
│  Shared libs: textile-biz-components, solvas-am-biz-components   │
└──────────────────────────────────────────────────────────────────┘
                              │ HTTP/gRPC
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                        BFF LAYER                                 │
│                                                                  │
│  solvas-am-bff ──────────── solvas-am-compliance-bff             │
│       │                              │                           │
│  solvas-am-portfolio-bff             │                           │
└──────────────────────────────────────────────────────────────────┘
                              │ gRPC
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                    DOMAIN SERVICE LAYER                           │
│                                                                  │
│  solvas-am-domaindata                                            │
│  solvas-am-compliance-snapshots                                  │
│  solvas-am-compliance-modeling                                   │
│  solvas-am-portfolio-organizations                               │
└──────────────────────────────────────────────────────────────────┘
                              │ EF Core / DbShim
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                      DATABASE LAYER                               │
│                                                                  │
│  SQL Server (shared with solvas-assetmanagement-classic)         │
│  ┌─────────┐  ┌──────────────┐  ┌─────────────┐                │
│  │  Core   │  │  Portfolio   │  │ Compliance  │                 │
│  │ Schema  │  │   Schema     │  │   Schema    │                 │
│  └─────────┘  └──────────────┘  └─────────────┘                │
└──────────────────────────────────────────────────────────────────┘
```

**Shared library dependency:**
- ALL backend services depend on → `solvas-am-common` (via NuGet)
- ALL frontend modules depend on → `@textile/common`, `@textile/mui`, `@textile/runtime` (via npm)
- AM frontend modules depend on → `@solvasam/bizcomponents` and `@textile/biz-components`

---

## 10. Testing Strategy

### 10.1 Backend Testing

Each backend repo has a `spec/` directory containing:
- **Unit tests** — Isolated business logic testing
- **Integration tests** — Database-backed tests (using test containers or local K8s SQL Server)
- **gRPC endpoint tests** — Testing the full request/response cycle
- Test utilities provided by `SolvasAm.Common.SpecUtilities`
- Framework: **xUnit** (standard .NET test framework)

### 10.2 Frontend Testing

- **Unit tests** — Component rendering and behavior tests (Vitest/Jest + Testing Library)
- **Integration tests** — Full module tests with mocked APIs (MSW)
- **Visual tests** — Storybook-based component verification
- Coverage reporting via `@vitest/coverage-v8`

### 10.3 HTTP Testing

Several repos include `http-testing/` directories with:
- `.http` files (REST Client format) for manual API testing
- Pre-configured requests for common operations
- Environment-specific variable files

---

## 11. Build & Package Versioning

### 11.1 Backend (.NET)

- Version set by `build/set-package-versions.sh` based on branch and build ID
- NuGet packages published to private feed
- Docker images tagged with build ID and semantic version
- `SharedAssemblyInfo.cs` provides shared assembly metadata

### 11.2 Frontend (Node.js)

- `package.json` version typically `0.0.0` (actual version set at build time)
- Library modules (`build-lib` script) produce publishable npm packages
- Application modules (`build-prod` script) produce webpack bundles deployed as Docker images
- `CHANGELOG.md` tracks version history

---

## 12. Local Development Setup

### Prerequisites
- Docker Desktop with Kubernetes enabled
- .NET 8 SDK
- Node.js 20+
- Helm 3
- kubectl configured for local cluster

### Backend Service
```bash
./dev-up.sh          # Start dependencies in K8s
dotnet run           # Run the service locally
./dev-down.sh        # Tear down dependencies
```

### Frontend Module
```bash
npm install
npm run watch        # Webpack dev build with watch
npm run start        # Serve built files locally
npm run test         # Run tests
npm run storybook    # (textile-biz-components only)
```

---

## 13. Summary Table

| Aspect | Technology |
|--------|-----------|
| Frontend Framework | React 18 + TypeScript 5 |
| Micro-Frontend | Single-SPA 6 |
| State Management | Zustand 5 + TanStack React Query 5 |
| UI Library | Material UI 6 (custom Textile theme) |
| Data Grid | AG Grid (enterprise) |
| Code Editor | Monaco Editor |
| Charts | Chart.js |
| Backend Runtime | .NET 8 / ASP.NET Core |
| API Protocol | gRPC (Protocol Buffers) |
| ORM | Entity Framework Core |
| Legacy DB Access | Dapper via DbShim |
| Database | SQL Server |
| Auth | Keycloak / Auth0 |
| Container Runtime | Docker |
| Orchestration | Kubernetes + Helm |
| CI/CD | Azure DevOps Pipelines |
| Container Registry | Azure Container Registry |
| Frontend Bundler | Webpack 5 |
| Frontend Testing | Vitest / Jest + Testing Library + MSW |
| Backend Testing | xUnit |
| Linting | ESLint (frontend) / Roslyn Analyzers (backend) |
| Formatting | Prettier (frontend) |
| Component Docs | Storybook 8 |
| Version Control | Git (Azure DevOps) |
