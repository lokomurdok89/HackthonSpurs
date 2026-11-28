# Run at the Git root with PowerShell 7.6 (Roslyn is bundled with pwsh).
$ErrorActionPreference = 'Stop'
python -B tools/generate_backend_resources.py --check
if ($LASTEXITCODE) { throw 'Regenerate the backend resource projections.' }
$catalog = Get-Content -Raw backend/HackthonSpurs.Resources/backend-resources.json | ConvertFrom-Json -AsHashtable
$codes = @($catalog.Errors.Values | ForEach-Object { $_.code })
$files = @(git ls-files -co --exclude-standard -- 'backend/*.cs' | Sort-Object -Unique)
if ($LASTEXITCODE -or !$files) { throw 'Run this check from the Git root.' }
$failed = [Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    if ($file -match '/(Migrations|HackthonSpurs.Resources)/' -or !(Test-Path -LiteralPath $file)) { continue }
    $path = (Resolve-Path -LiteralPath $file).Path
    $root = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($path)).GetRoot()
    foreach ($node in $root.DescendantNodes()) {
        if ($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.InterpolatedStringTextSyntax]) {
            # Interpolated messages are resources too; only URI/framing punctuation is structural.
            if ($node.TextToken.ValueText -cin @('', '/', 'data:', ';base64,')) { continue }
        } elseif ($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax]) {
            if ($node.Kind().ToString() -eq 'NumericLiteralExpression') {
                # Catch operational timeouts and typed configuration defaults, not array offsets or schema ordinals.
                if ($node.Parent.Parent -is [Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentListSyntax] -and
                    $node.Parent.Parent.Parent -is [Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax] -and
                    $node.Parent.Parent.Parent.Expression.ToString() -match '(TimeSpan\.From\w+|\.GetValue<.+>)$') {
                    $failed.Add("${file}:$($node.GetLocation().GetLineSpan().StartLinePosition.Line + 1) - inline operational default")
                }
                continue
            }
            if ($node.Kind().ToString() -notin @('StringLiteralExpression', 'Utf8StringLiteralExpression')) { continue }
            $v = $node.Token.ValueText
            if ($v -cin @('', ' ', '/', '[', ']', 'D', 'N', 'R', '%PDF-', "PK`u{3}`u{4}")) { continue }
            # SQL, constraints and database identifiers are schema, not application messages or settings.
            if ($v -match '^\s*(WITH|UPDATE|DELETE|INSERT|SELECT)\s') {
                if (!($codes | Where-Object { $v.Contains("'$_'") })) { continue }
            } elseif ($file -match '/Persistence/(DocumentIndexJob|DocumentsDb)\.cs$' -and
                ($node.Ancestors() | Where-Object {
                    $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax] -and
                    $_.Expression.ToString() -match '\.(HasCheckConstraint|HasColumnType|HasDatabaseName|HasPostgresExtension|ToTable)$'
                })) { continue }
        } else { continue }
        $line = $node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
        $failed.Add("${file}:$line - move application literals to backend-resources.json")
    }
}
if ($failed.Count) { $failed | Write-Output; throw 'Found inline backend resources.' }
Write-Output "PASS: $($codes.Count) unique error definitions; production C# literal policy and projections checked."
