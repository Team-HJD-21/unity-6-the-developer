param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('Sync', 'ReadLocks', 'Acquire', 'AcquireFolder', 'Submit', 'WatchDiscard')]
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

function Get-RemoteLockEntries($locks) {
    foreach ($lock in $locks.ours) {
        if ($lock.path) { @{ path = $lock.path; id = [string]$lock.id; owner = [string]$lock.owner.name; owned = $true } }
    }
    foreach ($lock in $locks.theirs) {
        if ($lock.path) { @{ path = $lock.path; id = [string]$lock.id; owner = [string]$lock.owner.name; owned = $false } }
    }
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
    return @{ path = $path; id = $id; record = $record }
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
    $owned = $null
    $remoteLocks = $null
    try {
        $locks = Get-Locks
        $owned = @($locks.ours | ForEach-Object { [string]$_.id })
        $remoteLocks = @(Get-RemoteLockEntries $locks)
    }
    catch {
        # Preserve the local records and let the editor keep its last lock list.
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
    ConvertTo-Json -Compress -Depth 4 -InputObject @{ tracked = @($paths); editable = $openAssets; remoteLocks = $remoteLocks }
}

function Get-CheckoutRef {
    $reference = & git symbolic-ref --quiet HEAD
    if ($LASTEXITCODE -eq 1) { return 'HEAD' }
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the current checkout.' }
    return $reference
}

function Test-AssetChanges($path) {
    return [bool](Invoke-Git --no-optional-locks status --porcelain --no-renames --untracked-files=all -- $path)
}

function Get-DiscardRecords {
    Get-Records | Where-Object { $_.discardHead -and !$_.submitted }
}

function Test-AssetCommittedSinceLock($record) {
    $head = Invoke-Git rev-parse HEAD
    if ((Get-Blob $head $record.path) -cne $record.base) { return $true }

    # Check the ref log as well as HEAD so a commit followed by a reset still counts.
    & git reflog exists $record.discardRef
    if ($LASTEXITCODE -ne 0) { return $true }
    $tips = @(Invoke-Git reflog show --format=%H "--since=@$($record.discardSince)" $record.discardRef |
        Sort-Object -Unique)
    return [bool](Invoke-Git log -1 --full-history --format=%H $head @tips "^$($record.discardHead)" -- $record.path)
}

function Release-DiscardedAssets {
    $records = @(Get-DiscardRecords)
    if (!$records.Count) { return $false }

    $reference = Get-CheckoutRef
    foreach ($record in $records) {
        if ($record.discardRef -cne $reference) { continue }
        if (Test-AssetChanges $record.path) {
            if (!$record.discardSeenDirty) {
                $record.discardSeenDirty = $true
                Save-Record $record
            }
            continue
        }
        if (!$record.discardSeenDirty) { continue }

        $file = [IO.FileInfo]::new((Join-Path $root $record.path))
        $wasReadOnly = $file.Exists -and $file.IsReadOnly
        Set-ReadOnly $record.path $true
        $released = $false
        try {
            if ((Get-CheckoutRef) -cne $record.discardRef -or (Test-AssetChanges $record.path)) { continue }
            if (Test-AssetCommittedSinceLock $record) {
                $record.discardHead = ''
                Save-Record $record
                continue
            }

            try { $null = Invoke-Git lfs unlock "--id=$($record.id)" --remote=origin }
            catch {
                $failure = $_
                $owned = (Get-Locks).ours
                if ($owned | Where-Object { $_.id -ceq $record.id -and $_.path -ceq $record.path }) {
                    throw $failure
                }
            }
            $released = $true
            Remove-Item -LiteralPath (Get-RecordPath $record.id)
        }
        finally {
            if (!$released) { Set-ReadOnly $record.path $wasReadOnly }
        }
    }

    return $true
}

function Enter-OperationGate {
    $path = Join-Path $state 'mutex'
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try { return [IO.File]::Open($path, 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] { Start-Sleep -Milliseconds 250 }
    }
    throw 'Another Unity asset lock operation is still running.'
}

function Write-DiscardWatcherError([string]$message) {
    $log = Join-Path $state 'discard-watcher.error.log'
    [IO.File]::AppendAllText($log, $message + [Environment]::NewLine, $utf8)
}

function Start-DiscardWatcher {
    if (!(Get-DiscardRecords | Select-Object -First 1)) { return }
    try { $probe = [IO.File]::Open((Join-Path $state 'discard-watcher'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch [IO.IOException] { return }

    try {
        $shell = (Get-Process -Id $PID).Path
        $arguments = @('-NoProfile', '-NonInteractive')
        if ($env:OS -eq 'Windows_NT') { $arguments += @('-ExecutionPolicy', 'RemoteSigned') }
        $arguments += @('-File', 'tools/UnityAssetLocks/AssetLocks.ps1', 'WatchDiscard')
        $options = @{
            FilePath = $shell
            ArgumentList = $arguments
            WorkingDirectory = $root
        }
        if ($env:OS -eq 'Windows_NT') {
            $options.WindowStyle = 'Hidden'
        }
        else {
            $options.FilePath = (Get-Command nohup -CommandType Application -ErrorAction Stop).Source
            $options.ArgumentList = @('"' + $shell + '"') + $arguments
            $options.RedirectStandardOutput = Join-Path $state 'discard-watcher.log'
            $options.RedirectStandardError = Join-Path $state 'discard-watcher-launch.error.log'
        }
        Start-Process @options
    }
    catch { Write-DiscardWatcherError "Could not start the discard watcher: $($_.Exception.Message)" }
    finally { $probe.Dispose() }
}

function Watch-DiscardedAssets {
    # The launching process briefly holds this file while starting the watcher.
    $watch = $null
    for ($attempt = 0; $attempt -lt 10 -and $null -eq $watch; $attempt++) {
        try { $watch = [IO.File]::Open((Join-Path $state 'discard-watcher'), 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] { Start-Sleep -Milliseconds 100 }
    }
    if ($null -eq $watch) { return }

    try {
        while ($true) {
            $delay = 2
            try {
                $gate = Enter-OperationGate
                try { if (!(Release-DiscardedAssets)) { return } }
                finally { $gate.Dispose() }
            }
            catch {
                Write-DiscardWatcherError $_.Exception.Message
                $delay = 15
            }
            Start-Sleep -Seconds $delay
        }
    }
    finally { $watch.Dispose() }
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

    $localByPath = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($record in Get-Records) { $localByPath[$record.path] = $record }
    $editable = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)

    # The server rejects a duplicate lock. Preflight multiple paths and verify any existing local lock.
    if ($paths.Count -gt 1 -or $localByPath.ContainsKey($paths[0])) {
        $locks = Get-Locks
        $serverByPath = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
        foreach ($lock in (@($locks.ours) + @($locks.theirs))) {
            if ($null -ne $lock) { $serverByPath[$lock.path] = $lock }
        }
        $ownedIds = @($locks.ours | ForEach-Object { [string]$_.id })

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
    }

    $checkout = Get-CheckoutRef
    $startHead = Invoke-Git rev-parse HEAD
    $lockStartedAt = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
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
                $discardHead = if ($base -ceq $published -and $head -ceq $startHead -and
                    (Get-CheckoutRef) -ceq $checkout) { $head } else { '' }
                Save-Record ([pscustomobject]@{
                    id = $lock.id; path = $lock.path; main = $main; base = $published; submitted = ''
                    discardHead = $discardHead; discardRef = $checkout; discardSince = $lockStartedAt
                    discardSeenDirty = [bool]$discardHead -and (Test-AssetChanges $lock.path)
                })
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
    if ($Command -eq 'ReadLocks') {
        ConvertTo-Json -Compress -InputObject @{ remoteLocks = @(Get-RemoteLockEntries (Get-Locks)) }
        return
    }

    $state = Join-Path (Invoke-Git rev-parse --absolute-git-dir) 'unity-asset-locks'
    [IO.Directory]::CreateDirectory($state) | Out-Null
    if ($Command -eq 'WatchDiscard') {
        Watch-DiscardedAssets
        return
    }

    $gate = Enter-OperationGate
    try {
        switch ($Command) {
            'Sync' { Sync-Assets }
            'Acquire' { ConvertTo-Json -Compress -InputObject (Open-Asset $AssetPath) }
            'AcquireFolder' { Open-AssetFolder $AssetPath }
            'Submit' { Submit-Assets }
        }
        if ($Command -ne 'Submit') { Start-DiscardWatcher }
    }
    finally { $gate.Dispose() }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
