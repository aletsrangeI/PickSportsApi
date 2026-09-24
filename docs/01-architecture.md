# Arquitectura de Backend - PickSportsApi

## 1. Clean Architecture (Onion Pattern)

```
        ┌──────────────────────────────────────────────┐
        │                 WebApi Layer                 │
        │      (Controllers, Middlewares, Workers)     │
        └──────────────────────┬───────────────────────┘
                               ▼
        ┌──────────────────────────────────────────────┐
        │                UseCases Layer                │
        │    (ScoringEngine, EspnSync, PickService)    │
        └───────┬───────────────────────────────┬──────┘
                ▼                               ▼
  ┌───────────────────────────┐   ┌───────────────────────────┐
  │      Interface Layer      │   │         DTO Layer         │
  │ (IRepositories, IUseCases)│   │(Request / Response models)│
  └─────────────┬─────────────┘   └─────────────┬─────────────┘
                ▼                               ▼
  ┌───────────────────────────┐   ┌───────────────────────────┐
  │     Persistence Layer     │   │      Validator Layer      │
  │  (EF Core DbContext, Repos│   │    (FluentValidation)     │
  └─────────────┬─────────────┘   └───────────────────────────┘
                ▼
        ┌──────────────────────────────────────────────┐
        │                 Domain Layer                 │
        │          (Entities, Enums, Rules)            │
        └──────────────────────────────────────────────┘
```

## 2. Inyección de Dependencias
- Toda dependencia externa se expone como una interfaz en `Interface/`.
- La persistencia se agrupa en `IUnitOfWork`.
- Los servicios de aplicación se registran en `UseCases/ConfigureServices.cs`.
- Los repositorios y DbContext se registran en `Persistence/ConfigureServices.cs`.
- `WebApi/Program.cs` orquesta los módulos mediante extensiones.
