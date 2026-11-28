# PostgreSQL + pgvector for local development

The `postgres` service builds [the extension image](../backend/Infrastructure/PostgresVector/Dockerfile) from the same PostgreSQL 18.4 / Alpine 3.23 digest used by the existing demo. pgvector 0.8.2 is compiled against that image's PostgreSQL headers from a checksummed upstream source archive; only the compiled library, SQL files and license enter the final image. Keeping the same PG build avoids switching the existing cluster between Alpine and Debian. No database port is published.

From the repository root, with the existing private Compose environment:

```sh
docker compose up --build -d --wait postgres
pwsh -NoProfile -File tests/quality/Test-Pgvector.ps1
```

Use the same project name, Compose files (including any subnet override) and database password as the running demo. A new volume enables `vector` in the configured database at initialization. PostgreSQL does not rerun init scripts on existing volumes.

**For an existing cluster:** verify there are no in-flight jobs, record its actual database and originals volume names, make a private `pg_dump -Fc` and restore it into an isolated PG18 container to check it. Do not publish the dump. Leave the originals volume in place: a database dump contains metadata, not the uploaded files. Recreate **only** the database service on the same data volume and, if necessary, run:

```sh
docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -U documents -d documents -c "CREATE EXTENSION IF NOT EXISTS vector"
pwsh -NoProfile -File tests/quality/Test-Pgvector.ps1
```

Enable the extension before deploying application migrations that use its types. Verify the database identity and document records before and after replacing the container. The smoke test uses a temporary table and two invented vectors; it never queries document content.

**Recovery:** leave the original and backup volumes intact. If application rollout fails, first revert the application while keeping the pgvector-capable DB runtime. To recover the pre-upgrade state, restore the verified dump into a **new** volume and validate it before selecting it. Do not point an image missing `vector.so` at a live cluster that already uses vector columns. Do not run `down -v`, drop the extension with cascade, or reset shared migrations.
