# Run from the repository root with PowerShell 7.6 (Roslyn ships with pwsh).
$ErrorActionPreference = 'Stop'
$limit = 200
$files = @(git ls-files -co --exclude-standard -- '*.cs' '*.py' | Sort-Object -Unique)
if ($LASTEXITCODE -ne 0 -or !$files) { throw 'Expected source files at the Git root.' }
$sizes = @{}
foreach ($file in $files) {
    if (!(Test-Path -LiteralPath $file)) { continue } # Deleted in the working tree
    $path = (Resolve-Path -LiteralPath $file).Path
    if ($file.EndsWith('.py')) {
        # Conservative bound for Python test modules (including their classes).
        $sizes[$file] = [IO.File]::ReadAllLines($path).Length
        continue
    }
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($path))
    foreach ($type in $tree.GetRoot().DescendantNodes()) {
        if ($type -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax]) { continue }
        $scope = $type.Identifier.ValueText + '`' + $type.Arity
        for ($parent = $type.Parent; $null -ne $parent; $parent = $parent.Parent) {
            if ($parent -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax]) {
                $scope = $parent.Name.ToString() + '.' + $scope
            } elseif ($parent -is [Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax]) {
                $scope = $parent.Identifier.ValueText + '`' + $parent.Arity + '+' + $scope
            }
        }
        # Each .NET project lives under backend/ or tests/; sum all partial declarations.
        $key = (($file -split '/')[0..1] -join '/') + ':' + $scope
        $span = $type.GetLocation().GetLineSpan()
        $sizes[$key] += $span.EndLinePosition.Line - $span.StartLinePosition.Line + 1
    }
}
$over = @($sizes.GetEnumerator() | Where-Object Value -gt $limit)
if ($over) { $over | Format-Table -AutoSize | Out-Host; throw "Exceeded $limit physical lines." }
$sizes.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 3 | Format-Table -AutoSize
Write-Output "PASS: $($sizes.Count) C# types and Python modules checked. Run the web lint for TypeScript."
