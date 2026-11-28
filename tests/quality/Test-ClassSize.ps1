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
    $nodes = @($tree.GetRoot().DescendantNodes())
    if ($file.StartsWith('backend/')) {
        $types = @($nodes | Where-Object {
            $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax] -or
            $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax]
        })
        $ports = @($types | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax] })
        if ($ports.Count) {
            if ($ports.Count -ne 1 -or $types.Count -ne 1) { throw "Expected exactly one interface in ${file}." }
            $name = $ports[0].Identifier.ValueText
            if ($name -cnotmatch '^I[A-Z]' -or !$file.EndsWith("/Contracts/$name.cs", [StringComparison]::Ordinal)) {
                throw "Expected an I-prefixed type in its own feature/Contracts/$name.cs: ${file}."
            }
        }
        if ($file.StartsWith('backend/HackthonSpurs.Application/')) {
            $top = @($types | Where-Object { $_.Parent -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax] })
            if (!$types.Count -and $file.Split('/')[-1] -cin @('GlobalUsings.cs', 'AssemblyInfo.cs')) { continue }
            if ($top.Count -ne 1 -or $file.Split('/')[-1] -cne ($top[0].Identifier.ValueText + '.cs')) {
                throw "Expected one application type per file named after its declaration: ${file}."
            }
            $area = $file.Split('/')[2]
            if ($ports.Count -and $file -cne "backend/HackthonSpurs.Application/$area/Contracts/$name.cs") {
                throw "Keep application interfaces directly in their owning feature's Contracts folder: $file."
            }
            if ($top[0].Parent.Name.ToString() -cne "HackthonSpurs.Application.$area") {
                throw "Keep the application type in its owning feature namespace: ${file}."
            }
            $isModel = $top[0] -is [Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax] -or
                       $top[0] -is [Microsoft.CodeAnalysis.CSharp.Syntax.EnumDeclarationSyntax]
            if ($isModel -ne ($file -cmatch '/Models/[^/]+$')) { throw "Place only application records/enums in feature/Models/: ${file}." }
        }
    }
    foreach ($type in $nodes) {
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
Write-Output "PASS: $($sizes.Count) C# types and Python modules checked; backend interface and application type layout verified. Run the web lint for TypeScript."
