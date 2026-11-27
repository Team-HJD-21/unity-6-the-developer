$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '../AssetLocks.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw $parseErrors[0] }
$function = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Submit-Assets'
}, $true)
Invoke-Expression $function.Extent.Text

$path = 'Assets/Scenes/Gameplay/Stage_1.unity'
$record = [pscustomobject]@{ path = $path; base = 'base'; submitted = 'submitted'; id = 'test' }
$InputPath = Join-Path $env:TEMP ('unity-lock-submit-' + [guid]::NewGuid() + '.txt')
function Get-Records { $record }
function Get-Blob($commit, $assetPath) { $script:versions[$commit] }
function Get-LockKey { 'test' }
function Get-Locks { @{ ours = @() } }
function Invoke-Git {
    switch ($args[0]) {
        'merge-base' { $script:usedMergeBase = $true; return 'base-commit' }
        'rev-parse' { return 'previous-commit' }
        'status' { return $script:dirty }
        'ls-remote' { return @() }
        default { throw "Unexpected git operation $args" }
    }
}
function Check($name, $remote, $assetVersion, $headVersion, $dirtyState, $expectedError) {
    $script:versions = @{ 'base-commit' = 'base'; 'previous-commit' = 'base'; ('a' * 40) = $assetVersion; HEAD = $headVersion }
    $script:dirty = $dirtyState
    $script:usedMergeBase = $false
    [IO.File]::WriteAllText($InputPath, "refs/heads/test $('a' * 40) refs/heads/test $remote")
    $message = ''
    try { Submit-Assets } catch { $message = $_.Exception.Message }
    if ($expectedError) {
        if ($message -notlike $expectedError) { throw "$name failed: $message" }
    }
    elseif ($message) { throw "$name failed: $message" }
    if ($usedMergeBase -ne ($remote -eq ('0' * 40))) { throw "$name used the wrong comparison base" }
    Write-Output "PASS $name"
}
try {
    Check 'new Docs branch ignores an already-submitted unchanged scene' ('0' * 40) 'base' 'local-scene' ' M scene' ''
    Check 'new branch still blocks an uncommitted scene' ('0' * 40) 'changed' 'changed' ' M scene' 'Save and commit*'
    Check 'new branch still blocks a scene different from HEAD' ('0' * 40) 'changed' 'other' '' 'Save and commit*'
    Check 'new branch still checks scene ownership' ('0' * 40) 'changed' 'changed' '' '*no longer owns its lock*'
    Check 'existing Docs branch ignores an unchanged scene' ('b' * 40) 'base' 'local-scene' ' M scene' ''
    Check 'existing branch still checks changed scene ownership' ('b' * 40) 'changed' 'changed' '' '*no longer owns its lock*'
}
finally {
    Remove-Item -LiteralPath $InputPath -ErrorAction SilentlyContinue
}