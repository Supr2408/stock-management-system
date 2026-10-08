# BoxTrack Architecture

BoxTrack is a modular monolith. Domain contains entities and business rules with no framework dependency. Application contains use-case services and contracts. Infrastructure owns EF Core and PostgreSQL. Api owns HTTP, authentication, middleware, and dependency wiring. The React/Vite frontend consumes REST endpoints and keeps navigation lightweight for older Windows workstations.

Master data is soft-deleted through `is_active`. Changes must be recorded in `audit_log`. PostgreSQL schema changes are owned by EF Core migrations in Infrastructure. Controllers are transport adapters; business decisions live in Application services.
