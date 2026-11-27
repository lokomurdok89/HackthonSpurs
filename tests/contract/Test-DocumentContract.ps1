#requires -Version 7.6
# Offline checks for the UI fixtures; this does not exercise an HTTP server.
$ErrorActionPreference = 'Stop'
$spec = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../docs/api/openapi.json') |
    ConvertFrom-Json -AsHashtable

function Require($condition, [string] $message) {
    if (-not $condition) { throw $message }
}

function Resolve-LocalRef([string] $pointer) {
    Require ($pointer.StartsWith('#/')) "Unexpected external reference: $pointer"
    $node = $spec
    foreach ($part in $pointer.Substring(2).Split('/')) {
        $node = $node[$part]
        Require ($null -ne $node) "Missing reference: $pointer"
    }
    return $node
}

function Matches-Schema($schema, $value) {
    $root = @{
        '$schema' = 'https://json-schema.org/draft/2020-12/schema'
        components = @{ schemas = $spec.components.schemas }
        allOf = @($schema)
    } | ConvertTo-Json -Depth 100 -Compress
    $json = ConvertTo-Json -InputObject $value -Depth 100 -Compress
    return (Test-Json -Json $json -Schema $root -ErrorAction SilentlyContinue)
}

$upload = $spec.paths['/api/documents'].post
$status = $spec.paths['/api/documents/{id}'].get
$result = $spec.paths['/api/documents/{id}/result'].get
Require ($spec.paths.Count -eq 3 -and $upload -and $status -and $result) 'Unexpected public routes.'
Require ($upload.parameters[0].name -ceq 'Idempotency-Key' -and $upload.parameters[0].required) 'Missing upload key.'
Require ($upload.requestBody.content['multipart/form-data'].schema.required -contains 'file') 'Missing file part.'
Require ($upload.responses['202'].headers.Location.required) 'Missing status link.'

$count = 0
foreach ($op in @($upload, $status, $result)) {
    foreach ($entry in $op.responses.GetEnumerator()) {
        $response = $entry.Value
        if ($response.'$ref') { $response = Resolve-LocalRef $response.'$ref' }
        foreach ($media in $response.content.Values) {
            Require ($media.examples.Count -gt 0) "No examples: $($op.operationId) $($entry.Key)"
            foreach ($case in $media.examples.GetEnumerator()) {
                Require (Matches-Schema $media.schema $case.Value.value) "Invalid example: $($op.operationId) $($case.Key)"
                if ([int]$entry.Key -ge 400) {
                    Require ($case.Value.value.status -eq [int]$entry.Key) "Problem status mismatch: $($case.Key)"
                }
                $count++
            }
        }
    }
}

$statusExamples = $status.responses['200'].content['application/json'].examples
$resultExamples = $result.responses['200'].content['application/json'].examples
foreach ($state in 'Queued','Processing','Completed','Failed') {
    Require ($statusExamples.Values.value.state -ccontains $state) "Missing state: $state"
}
foreach ($case in 'materialsRow','unconfirmedLink','originNotDemonstrated','unreadableSection','csvCell') {
    Require ($resultExamples.Contains($case)) "Missing mock case: $case"
}
Require ($resultExamples.unreadableSection.value.warnings[0].locator.page -eq 1) 'Missing original page warning.'
Require ($resultExamples.materialsRow.value.documentId -ne $resultExamples.unreadableSection.value.documentId) 'Two uploads must have distinct IDs.'
Require ($resultExamples.unreadableSection.value.documentId -eq $statusExamples.completed.value.id) 'Mock status/result ID mismatch.'

$badStatus = $statusExamples.failed.value.Clone()
$badStatus.Remove('failureCode')
Require (-not (Matches-Schema $spec.components.schemas.DocumentStatus $badStatus)) 'Failed must include a cause.'
Require (-not (Matches-Schema $spec.components.schemas.Locator @{kind='page';page=0})) 'Page numbers start at one.'
Require (-not (Matches-Schema $spec.components.schemas.Locator @{kind='cells';sheet='Materials'})) 'Missing cell range accepted.'
$empty = $resultExamples.unreadableSection.value.Clone()
$empty.fragments = @()
Require (Matches-Schema $spec.components.schemas.ReadResult $empty) 'A partial reading may contain only a warning.'
$empty.warnings = @()
Require (-not (Matches-Schema $spec.components.schemas.ReadResult $empty)) 'An empty reading must explain why.'
Write-Output "PASS: $count response examples, three public routes, demo fixtures and schema guardrails."
