# Exercise pgvector on the configured local database. No documents or embeddings are read.
param([string] $Container = 'hackthonspurs-postgres-1')
$ErrorActionPreference = 'Stop'
$sql = @'
BEGIN;
CREATE TEMP TABLE vector_smoke (sample_id int PRIMARY KEY, embedding vector(2));
INSERT INTO vector_smoke VALUES (1, '[1,0]'), (2, '[0,1]');
DO $$
BEGIN
    IF (SELECT sample_id FROM vector_smoke ORDER BY embedding <=> '[1,0]'::vector LIMIT 1) <> 1
       OR abs(('[1,0]'::vector <=> '[0,1]'::vector) - 1.0) > 0.000001 THEN
        RAISE EXCEPTION 'Unexpected cosine search result';
    END IF;
END $$;
SELECT current_setting('server_version') AS postgres_version, extversion AS vector_version
FROM pg_extension WHERE extname = 'vector';
ROLLBACK;
'@
$sql | docker exec -i $Container psql -U documents -d documents -X -v ON_ERROR_STOP=1 -q
if ($LASTEXITCODE) { throw 'pgvector SQL smoke failed.' }
Write-Output 'PASS: pgvector type, cosine distance and nearest-neighbor ordering; synthetic transaction rolled back.'
