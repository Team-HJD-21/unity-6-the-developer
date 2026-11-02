# 열린 Editor/Scene을 조작하지 않고 Unity가 만든 참조 목록으로 C# 컴파일만 확인합니다.
# 출력은 Docs/Local에 격리하며 Unity Runtime·PlayMode·테스트 통과를 뜻하지 않습니다.
[CmdletBinding()]
param([string]$EditorData = 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnet = Join-Path $EditorData 'NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $EditorData 'DotNetSdkRoslyn/csc.dll'
foreach ($required in @($dotnet, $compiler)) {
    if (!(Test-Path -LiteralPath $required)) { throw "Unity compiler dependency missing: $required" }
}
$response = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Library/Bee/artifacts') -Recurse -File -Filter 'TeamHJD.Game.Presentation.rsp' |
    Where-Object { $_.Directory.Name.EndsWith('E.dag') } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($null -eq $response) { throw 'An existing Editor compilation response file is required. This tool does not launch Unity to generate one.' }
$assemblies = @('TeamHJD.Game.Presentation', 'TeamHJD.Game.Bootstrap', 'Assembly-CSharp-Editor')
$sourceResponses = @{}
foreach ($assembly in $assemblies) {
    $path = Join-Path $response.Directory.FullName ($assembly + '.rsp')
    $sourceResponses[$assembly] = [IO.File]::ReadAllLines($path)
    if (!@($sourceResponses[$assembly] | Where-Object { $_ -eq '-define:UNITY_EDITOR' }).Count) {
        throw "Not an Editor reference set: $assembly"
    }
    if (@($sourceResponses[$assembly] | Where-Object { $_ -match '^[-/](pdb|doc|errorlog|generatedfilesout):' }).Count) {
        throw "Unexpected additional compiler output destination: $assembly. Refusing to write outside the isolated output."
    }
}
$run = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$output = Join-Path $projectRoot ('Docs/Local/Validation/CoreSprint20261015/ManagedCompile/' + $run)
New-Item -ItemType Directory -Path $output | Out-Null
$replacementReferences = @{}
$results = @()
$trackedSources = @(
    'Assets/Scripts/Core/Presentation/Scene/PlayerSceneAdapter.cs',
    'Assets/Scripts/Core/Presentation/Scene/MatchPlayerActorScope.cs',
    'Assets/Scripts/Core/Bootstrap/SceneComposition/RegionSceneBinding.cs',
    'Assets/Scripts/Core/Bootstrap/SceneComposition/InitialStageCompositionRoot.cs',
    'Assets/Scripts/Features/Player/Tests/Editor/PlayerBindingCleanupTests.cs')
$sourceHashes = @{}
foreach ($source in $trackedSources) { $sourceHashes[$source] = (Get-FileHash -LiteralPath (Join-Path $projectRoot $source) -Algorithm SHA256).Hash }
Push-Location $projectRoot
try {
    foreach ($assembly in $assemblies) {
        $dll = (Join-Path $output ($assembly + '.dll')).Replace('\', '/')
        $reference = (Join-Path $output ($assembly + '.ref.dll')).Replace('\', '/')
        $lines = [Collections.Generic.List[string]]::new()
        foreach ($line in $sourceResponses[$assembly]) {
            if ($line -match '^[-/]out:') { $lines.Add('-out:"' + $dll + '"'); continue }
            if ($line -match '^[-/]refout:') { $lines.Add('-refout:"' + $reference + '"'); continue }
            if ($line -match '^[-/]r:"[^\"]+/([^/\"]+)\.ref\.dll"$' -and $replacementReferences.ContainsKey($Matches[1])) {
                $lines.Add('-r:"' + $replacementReferences[$Matches[1]] + '"'); continue
            }
            $lines.Add($line)
        }
        $testSource = 'Assets/Scripts/Features/Player/Tests/Editor/PlayerBindingCleanupTests.cs'
        if ($assembly -eq 'Assembly-CSharp-Editor' -and !@($lines | Where-Object { $_.Trim('"').Replace('\', '/') -eq $testSource }).Count) {
            $lines.Add('"' + $testSource + '"')
        }
        $rsp = Join-Path $output ($assembly + '.rsp')
        [IO.File]::WriteAllLines($rsp, $lines, [Text.UTF8Encoding]::new($false))
        $diagnostics = @(& $dotnet $compiler ('@' + $rsp) 2>&1)
        $exitCode = $LASTEXITCODE
        [IO.File]::WriteAllLines((Join-Path $output ($assembly + '.log')), [string[]]$diagnostics)
        $results += [ordered]@{ assembly = $assembly; exitCode = $exitCode; output = $dll }
        if ($exitCode -ne 0) { $diagnostics | Select-Object -Last 20; throw "Managed compilation failed: $assembly; evidence=$output" }
        $replacementReferences[$assembly] = $reference
        Write-Output "COMPILED $assembly"
    }
    foreach ($source in $trackedSources) {
        if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $source) -Algorithm SHA256).Hash -ne $sourceHashes[$source]) {
            throw "A source changed during compilation: $source"
        }
    }
    [ordered]@{ success = $true; run = $run; scope = 'Managed compile only; no Unity lifecycle/test execution';
        referenceSet = $response.Directory.FullName; results = $results; sourceHashes = $sourceHashes } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'compiled.json') -Encoding utf8
    Write-Output "MANAGED_COMPILE_VERIFIED $output"
}
finally { Pop-Location }
