# BoxTrack

Phase 0 and the first Phase 1 slice of the Nagreeka Indcon stock system.

## Run locally

1. Copy `.env.example` to `.env` and provide an ASP.NET Identity password hash and JWT secret.
2. Start PostgreSQL with `docker compose -f database/docker-compose.yml up -d`.
3. Run the API with `dotnet run --project backend/src/BoxTrack.Api`.
4. Run the frontend with `npm install; npm run dev` from `frontend/`.

The API health endpoint is `/health`; OpenAPI is available at `/swagger` in Development. Build checks are `dotnet build backend/BoxTrack.sln`, `dotnet test backend/BoxTrack.sln`, and `npm run build` from `frontend/`.
