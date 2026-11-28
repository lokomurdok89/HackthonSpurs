# Run from the Git root after a Release build, with EF 10 available.
param([string] $EfTool = "dotnet-ef")
$ErrorActionPreference = 'Stop'
$efArgs = @('--project', 'backend/HackthonSpurs.Documents', '--startup-project', 'backend/HackthonSpurs.Api',
            '--configuration', 'Release', '--no-build')
$baseline = '20261128002954_WorkerLeases'
$upgrade = '20261128042412_WorkspaceAccess'
# Generate SQL separately from the database. Do not connect to developer services.
$before = & $EfTool migrations script 0 $baseline @efArgs
if ($LASTEXITCODE) { throw 'Could not generate the baseline SQL.' }
$after = & $EfTool migrations script $baseline $upgrade @efArgs
if ($LASTEXITCODE) { throw 'Could not generate the upgrade SQL.' }
# Compose builds this image locally. The test container has no ports, network or persistent data.
$name = 'spurs-upgrade-' + [guid]::NewGuid().ToString('N')
$image = 'hackthonspurs/postgres-vector:18.4-alpine3.23-v0.8.2'
docker run --rm --pull never --network none -d --name $name -e POSTGRES_DB=documents -e POSTGRES_USER=documents `
    -e POSTGRES_HOST_AUTH_METHOD=trust --tmpfs /var/lib/postgresql $image | Out-Null
if ($LASTEXITCODE) { throw 'Build the local pgvector image before running this check.' }
try {
    for ($i = 0; $i -lt 45; $i++) {
        docker exec $name pg_isready -h 127.0.0.1 -U documents -d documents *> $null
        if (!$LASTEXITCODE) { break }
        Start-Sleep -Seconds 1
    }
    $psql = @('exec', '-i', $name, 'psql', '-XAtq', '-v', 'ON_ERROR_STOP=1', '-U', 'documents', '-d', 'documents')
    $before | docker @psql
    if ($LASTEXITCODE) { throw 'Baseline migration failed.' }
    @"
INSERT INTO document_jobs ("Id","OperationKey","Sha256","MediaType","ByteCount","CreatedAt","State","Attempt")
VALUES ('00000000-0000-4000-8000-000000000001',gen_random_uuid(),repeat('a',64),'application/pdf',15,now(),'Queued',0);
INSERT INTO document_chunks ("Id","DocumentId","ChunkIndex","SourceName","Locator","Text","Model","Embedding","CreatedAt")
VALUES (gen_random_uuid(),'00000000-0000-4000-8000-000000000001',0,'fixture','page 1','synthetic legacy text','prior','[1,0]',now());
"@ | docker @psql
    if ($LASTEXITCODE) { throw 'Could not insert the synthetic legacy receipt and vector.' }
    $after | docker @psql
    if ($LASTEXITCODE) { throw 'Upgrade migration failed.' }
    $ok = @"
SELECT count(*)=1 AND NOT EXISTS (SELECT FROM document_index_jobs) AND NOT EXISTS (SELECT FROM workspace_users)
FROM document_chunks c JOIN document_jobs j ON j."Id"=c."DocumentId"
WHERE j."State"='Queued' AND NOT j."SendToOcr" AND c."Page" IS NULL
AND c."Model"='prior' AND c."Text"='synthetic legacy text' AND c."Embedding"::text='[1,0]';
"@ | docker @psql
    if ($LASTEXITCODE -or $ok -ne 't') { throw 'Legacy data or routing changed unexpectedly.' }
    & "$PSScriptRoot/Test-Pgvector.ps1" -Container $name
    Write-Output 'PASS: preserved an existing receipt and vector without scheduling OCR or indexing.'
} finally {
    docker stop $name | Out-Null
}
