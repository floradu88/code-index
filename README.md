# CodeIndex — Codebase → ChatGPT Thread Indexer

A .NET 8 API that recursively loads your codebase, chunks & streams files into an OpenAI **ChatGPT Thread**, and lets you **ask questions** later. Uses **Hangfire** for background indexing and **EF Core** (SQLite by default) for persistence.

## Quickstart
1. Set env var `OpenAI__ApiKey`.
2. Run migrations and API:
   ```powershell
   ./scripts/ef-migrate.ps1
   ./scripts/run.ps1
   ```
3. Open Swagger at `http://localhost:5000/swagger` (port depends on your run).

### Start an index
```bash
curl -X POST http://localhost:5253/api/index/start  -H "Content-Type: application/json"  -d '{ "name":"MyRepo", "basePath":"C:/Dev/MyRepo" }'
```

### Ask a question
```bash
curl -X POST http://localhost:5253/api/chat/ask  -H "Content-Type: application/json"  -d '{ "projectId":"<GUID>", "question":"Where is JWT validated?" }'
```

## Config
- SQLite default: `ConnectionStrings__Default="Data Source=codeindex.db"`
- PostgreSQL: set `FeatureFlags__UsePostgres=true` and `ConnectionStrings__Default="Host=...;Database=...;Username=...;Password=..."`

## Hangfire
Dashboard at `/jobs`. In-memory by default; toggle Postgres via `FeatureFlags__HangfirePostgres` (add Hangfire.PostgreSql package when enabling).

## Notes
- Skips binary files; change includes/excludes in `appsettings.json`.
- Add secret scanning to exclude `.env`, `secrets.*` etc.
- For production cost control, prefer RAG/embeddings (future toggle `LazyRagOnAsk`).

## License
MIT


-- Log in as superuser (postgres) then:
CREATE ROLE admin WITH LOGIN PASSWORD 'Adminp@ss2025!';
ALTER ROLE admin CREATEDB;
CREATE DATABASE "CodeIndexDb" OWNER admin;
GRANT ALL PRIVILEGES ON DATABASE "CodeIndexDb" TO admin;


curl https://api.openai.com/v1/assistants \
  -H "Authorization: Bearer $OPENAI_API_KEY" \
  -H "Content-Type: application/json" \
  -H "OpenAI-Beta: assistants=v2" \
  -d '{
    "model": "gpt-4.1",
    "name": "CodeIndex Assistant",
    "instructions": "You help answer questions about a codebase whose files are posted as messages with metadata file & chunkIndex. Prefer precise, scoped answers and ask follow-ups only when needed."
  }'

