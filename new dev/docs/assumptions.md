# Assumptions

- The available SDK is .NET 8.0.100, so the first implementation targets `net8.0` and uses the same ASP.NET Core architecture. The requested .NET 10 SDK should replace this target before production deployment.
- The legacy screenshots are used only as read-only behavioral and field references; no legacy customer data is copied into the repository.
- Phase 1 uses a service-backed development store while the EF Core PostgreSQL model is established. Persistence wiring and migrations are the next backend hardening step within Phase 1.
- Admin login requires `ADMIN_PASSWORD_HASH`; plaintext passwords are never stored or committed.
