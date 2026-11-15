param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('Sync', 'Acquire', 'AcquireFolder', 'Submit')]
    [string]$Command,
    [string]$AssetPath,
    [string]$InputPath
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'
$env:GIT_LITERAL_PATHSPECS = '1'
$utf8 = [Text.UTF8Encoding]::new($false)

function Invoke-Git {
    $result = & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args[0]) failed ($LASTEXITCODE)." }
    return $result
}

function Get-Blob($revision, $path) {
    $oid = & git rev-parse --verify --quiet "${revision}:$path"
    if ($LASTEXITCODE -eq 0) { return $oid }
    return 'deleted'
}

function Test-AssetPath($path) {
    return $path -cmatch '^Assets/[^\\\r\n]+$' -and ($path -split '/') -cnotcontains '..' -and
        ($path -split '/') -cnotcontains '.'
}

function Test-ManagedPath($path) {
    if ($path -cin @('ProjectSettings/Physics2DSettings.asset', 'ProjectSettings/TagManager.asset')) { return $true }
    return (Test-AssetPath $path) -and $path -cmatch '(?:\.unity|\.prefab|\.png\.meta)$'
}

function Set-ReadOnly($path, [bool]$readOnly) {
    $file = [IO.FileInfo]::new((Join-Path $root $path))
    if ($file.Exists -and $file.IsReadOnly -ne $readOnly) { $file.IsReadOnly = $readOnly }
}

function Get-Locks {
    return Invoke-Git lfs locks --verify --json --remote=origin | ConvertFrom-Json
}

function Get-LockKey([string]$id) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($utf8.GetBytes($id))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Get-RecordPath($id) {
    return Join-Path $state "$(Get-LockKey $id).json"
}

function Get-Records {
    foreach ($file in Get-ChildItem -LiteralPath $state -Filter '*.json' -File) {
        $record = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        if (!$record.id -or $file.BaseName -cne (Get-LockKey $record.id) -or !(Test-ManagedPath $record.path)) {
            throw "Invalid Unity asset lock record: $($file.Name)"
        }
        $record
    }
}

function Save-Record($record) {
    $file = Get-RecordPath $record.id
    $temp = "$file.tmp"
    [IO.File]::WriteAllText($temp, (ConvertTo-Json -InputObject $record -Compress), $utf8)
    if ([IO.File]::Exists($file)) {
        $backup = "$file.previous"
        [IO.File]::Replace($temp, $file, $backup)
        [IO.File]::Delete($backup)
    }
    else { [IO.File]::Move($temp, $file) }
}

function Get-EditableAsset($path, $id = '') {
    $record = if ($id) { Get-RecordPath $id } else { '' }
    return @{ path = $path; record = $record }
}

function Install-Hook {
    $hooks = Invoke-Git rev-parse --path-format=absolute --git-path hooks
    $common = Invoke-Git rev-parse --path-format=absolute --git-common-dir
    $standardHooks = [IO.Path]::GetFullPath((Join-Path $common 'hooks'))
    if ([IO.Path]::GetFullPath($hooks) -ine $standardHooks) {
        throw 'Unity asset locks need the repository pre-push hook. The current core.hooksPath points elsewhere.'
    }
    [IO.Directory]::CreateDirectory($hooks) | Out-Null
    $hook = Join-Path $hooks 'pre-push'
    $previous = Join-Path $hooks 'pre-push.before-unity-asset-locks'
    $template = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'pre-push.sh')).Replace("`r`n", "`n")
    $current = if ([IO.File]::Exists($hook)) { [IO.File]::ReadAllText($hook) } else { '' }
    if ($current -ceq $template) { return }
    if ($current -and !$current.Contains('# Unity asset locks (managed by ')) {
        if ([IO.File]::Exists($previous)) { throw 'An existing pre-push hook was already backed up. Check both hooks before replacing either.' }
        [IO.File]::Copy($hook, $previous)
    }
    [IO.File]::WriteAllText($hook, $template, $utf8)
    if ($env:OS -ne 'Windows_NT') { & chmod +x $hook; if ($LASTEXITCODE) { throw 'Cannot make pre-push executable.' } }
}

function Initialize-Repository {
    $endpointLine = Invoke-Git lfs env | Where-Object { $_ -match '^Endpoint(?: \(origin\))?=https://' } | Select-Object -First 1
    if ($endpointLine -notmatch '^Endpoint(?: \(origin\))?=(https://[^ ]+)') {
        throw 'Git LFS did not report an HTTPS locking endpoint for origin.'
    }
    $key = "lfs.$($Matches[1]).locksverify"
    $current = & git config --local --get $key
    if ($current -ne 'true') { $null = Invoke-Git config --local $key true }
    Install-Hook
}

function Sync-Assets {
    Initialize-Repository
    $records = @(Get-Records)
    $owned = @()
    if ($records.Count) {
        try { $owned = @((Get-Locks).ours | ForEach-Object { [string]$_.id }) }
        catch {
            # Preserve the local records when GitHub is unreachable.
            $owned = $null
        }
    }
    $editable = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($record in $records) {
        if ($null -ne $owned -and $owned -cnotcontains $record.id) {
            Set-ReadOnly $record.path $true
            Remove-Item -LiteralPath (Get-RecordPath $record.id)
        }
        elseif ($null -ne $owned -and !$record.submitted) { $editable.Add($record.path, $record.id) }
    }
    $paths = (Invoke-Git ls-files -z) -split "`0" | Where-Object { Test-ManagedPath $_ }
    foreach ($path in $paths) { Set-ReadOnly $path (!$editable.ContainsKey($path)) }
    $openAssets = @($editable.GetEnumerator() | ForEach-Object { Get-EditableAsset $_.Key $_.Value })
    ConvertTo-Json -Compress -Depth 4 -InputObject @{ tracked = @($paths); editable = $openAssets }
}

function Open-Asset($path) {
    if (!(Test-ManagedPath $path)) { throw "Unsupported Unity asset path: $path" }
    if ((Get-Blob HEAD $path) -eq 'deleted') {
        Set-ReadOnly $path $false
        return Get-EditableAsset $path
    }
    Open-TrackedAssets @($path)
}

function Open-AssetFolder($path) {
    if (!(Test-AssetPath $path)) { throw "Unsupported Unity asset folder: $path" }
    $paths = @((Invoke-Git ls-tree -r --name-only -z HEAD -- "$path/") -split "`0" |
        Where-Object { Test-ManagedPath $_ })
    $editable = @(Open-TrackedAssets $paths)
    ConvertTo-Json -Compress -Depth 4 -InputObject @{ editable = $editable }
}

function Open-TrackedAssets([string[]]$paths) {
    if (!$paths.Count) { return }
    Initialize-Repository

    $locks = Get-Locks
    $serverByPath = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($lock in (@($locks.ours) + @($locks.theirs))) {
        if ($null -ne $lock) { $serverByPath[$lock.path] = $lock }
    }
    $ownedIds = @($locks.ours | ForEach-Object { [string]$_.id })
    $localByPath = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($record in Get-Records) { $localByPath[$record.path] = $record }
    $editable = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)

    foreach ($path in $paths) {
        if (!$serverByPath.ContainsKey($path)) { continue }
        $lock = $serverByPath[$path]
        $id = [string]$lock.id
        if ($ownedIds -cnotcontains $id -or !$localByPath.ContainsKey($path) -or $localByPath[$path].id -cne $id) {
            Set-ReadOnly $path $true
            $owner = $lock.owner.name
            if ($owner) { throw "$path is locked by $owner in another account or checkout." }
            throw "$path is already locked in another checkout."
        }
        if ($localByPath[$path].submitted) {
            throw "$path has been submitted. It will unlock when that version reaches main."
        }
        $editable[$path] = $id
    }

    $newLocks = [Collections.Generic.List[object]]::new()
    try {
        foreach ($path in $paths) {
            if ($editable.ContainsKey($path)) { continue }
            if ($localByPath.ContainsKey($path)) {
                Set-ReadOnly $path $true
                Remove-Item -LiteralPath (Get-RecordPath $localByPath[$path].id)
            }
            try {
                $lock = @(Invoke-Git lfs lock --json --remote=origin -- $path | ConvertFrom-Json) | Select-Object -First 1
            }
            catch {
                Set-ReadOnly $path $true
                $failure = $_
                $reported = & git lfs locks --json --remote=origin "--path=$path"
                if ($LASTEXITCODE -eq 0) {
                    $owner = $reported | ConvertFrom-Json | ForEach-Object { $_.owner.name } | Select-Object -First 1
                    if ($owner) { throw "$path is locked by $owner." }
                }
                throw $failure
            }
            $id = [string]$lock.id
            if (!$id) { throw "No LFS lock ID returned for $path." }
            $newLocks.Add([pscustomobject]@{ id = $id; path = $path })
            $editable[$path] = $id
        }

        if ($newLocks.Count) {
            $null = Invoke-Git fetch --no-tags origin '+refs/heads/main:refs/remotes/origin/main'
            $main = Invoke-Git rev-parse refs/remotes/origin/main
            $head = Invoke-Git rev-parse HEAD
            $mergeBase = $null
            foreach ($lock in $newLocks) {
                $published = Get-Blob $main $lock.path
                $base = Get-Blob $head $lock.path
                if ($published -cne $base) {
                    if (!$mergeBase) { $mergeBase = Invoke-Git merge-base $head $main }
                    if ((Get-Blob $mergeBase $lock.path) -cne $published) {
                        throw "$($lock.path) changed on main since this branch diverged. Update your branch before editing it."
                    }
                }
                Save-Record ([pscustomobject]@{ id = $lock.id; path = $lock.path; main = $main; base = $published; submitted = '' })
            }
        }
        foreach ($path in $paths) {
            Set-ReadOnly $path $false
            Get-EditableAsset $path $editable[$path]
        }
    }
    catch {
        $failure = $_
        foreach ($lock in $newLocks) {
            try { Set-ReadOnly $lock.path $true }
            catch { [Console]::Error.WriteLine($_.Exception.Message) }
            try {
                $null = Invoke-Git lfs unlock "--id=$($lock.id)" --remote=origin
                $recordPath = Get-RecordPath $lock.id
                if (Test-Path -LiteralPath $recordPath) { Remove-Item -LiteralPath $recordPath }
            }
            catch { [Console]::Error.WriteLine("Could not roll back lock $($lock.id) on $($lock.path): $($_.Exception.Message)") }
        }
        throw $failure
    }
}

function Submit-Assets {
    $records = @(Get-Records)
    if (!$records.Count) { return }
    $updates = @(Get-Content -LiteralPath $InputPath | ForEach-Object {
        $parts = $_ -split ' '
        if ($parts.Count -eq 4 -and $parts[2].StartsWith('refs/heads/') -and $parts[1] -match '^[0-9a-f]{40}$' -and
            $parts[1] -ne ('0' * 40)) {
            $previous = & git rev-parse --verify --quiet "$($parts[3])^{commit}"
            [pscustomobject]@{ commit = $parts[1]; previous = $previous }
        }
    })
    if (!$updates.Count) { return }
    $owned = @((Get-Locks).ours | ForEach-Object { [string]$_.id })
    $submissions = @()
    foreach ($record in $records) {
        $versions = @(foreach ($update in $updates) {
            $version = Get-Blob $update.commit $record.path
            if ($version -cne $record.base -and (!$update.previous -or
                $version -cne (Get-Blob $update.previous $record.path))) { $version }
        })
        $versions = @($versions | Sort-Object -Unique)
        if (!$versions.Count) { continue }
        $version = $versions[0]
        $dirty = Invoke-Git status --porcelain --untracked-files=all -- $record.path
        if ($versions.Count -gt 1 -or $dirty -or (Get-Blob HEAD $record.path) -cne $version) {
            throw "Save and commit the complete change before pushing $($record.path)."
        }
        if ($owned -cnotcontains $record.id -or ($record.submitted -and $record.submitted -cne $version)) {
            throw "The edit session for $($record.path) no longer owns this version."
        }
        $submissions += [pscustomobject]@{ record = $record; version = $version }
    }
    $refspecs = @(foreach ($submission in $submissions) {
        $record = $submission.record
        $ref = "refs/tags/unity-asset-locks/$(Get-LockKey $record.id)/$($submission.version)"
        $record.submitted = $submission.version
        Save-Record $record
        Set-ReadOnly $record.path $true
        # Point to a commit already on GitHub. The tag name carries only the lock ID and version.
        "$($record.main):$ref"
    })
    if ($refspecs.Count) {
        $null = Invoke-Git -c push.followTags=false -c remote.origin.mirror=false push --no-verify --no-follow-tags origin @refspecs
    }
}

try {
    $root = Invoke-Git rev-parse --show-toplevel
    Set-Location -LiteralPath $root
    $state = Join-Path (Invoke-Git rev-parse --absolute-git-dir) 'unity-asset-locks'
    [IO.Directory]::CreateDirectory($state) | Out-Null
    $gate = $null
    for ($attempt = 0; $attempt -lt 40 -and $null -eq $gate; $attempt++) {
        try { $gate = [IO.File]::Open((Join-Path $state 'mutex'), 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] { Start-Sleep -Milliseconds 250 }
    }
    if ($null -eq $gate) { throw 'Another Unity asset lock operation is still running.' }
    try {
        switch ($Command) {
            'Sync' { Sync-Assets }
            'Acquire' { ConvertTo-Json -Compress -InputObject (Open-Asset $AssetPath) }
            'AcquireFolder' { Open-AssetFolder $AssetPath }
            'Submit' { Submit-Assets }
        }
    }
    finally { $gate.Dispose() }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
