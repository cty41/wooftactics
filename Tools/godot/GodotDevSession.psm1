Set-StrictMode -Version Latest

$script:TacticsGodotLaunchArgument = '--tactics-open-godot-dev=v1'

function Get-TacticsGodotLaunchArgument {
    $script:TacticsGodotLaunchArgument
}

function Get-TacticsGodotWorktreeIdentity {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)
    $resolved = (Resolve-Path -LiteralPath $RepoRoot).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    $normalized = $resolved.ToLowerInvariant()
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($normalized))
    }
    finally { $sha.Dispose() }
    $key = ([BitConverter]::ToString($digest)).Replace('-', '').ToLowerInvariant().Substring(0, 24)
    [pscustomobject]@{
        RepoRoot = $resolved
        Key = $key
        MutexName = "Local\TacticsGodotDev-$key"
        UserDirectoryName = "TacticsGodotDev/$key"
    }
}

function Enter-TacticsGodotOperationLock {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [int]$TimeoutMilliseconds = 0
    )
    $identity = Get-TacticsGodotWorktreeIdentity -RepoRoot $RepoRoot
    $mutex = [Threading.Mutex]::new($false, $identity.MutexName)
    try {
        $acquired = $false
        try { $acquired = $mutex.WaitOne($TimeoutMilliseconds) }
        catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) {
            throw "Another build, verification, or Editor launch owns this worktree: $($identity.RepoRoot)"
        }
    }
    catch {
        $mutex.Dispose()
        throw
    }
    [pscustomobject]@{ Mutex = $mutex; Identity = $identity }
}

function Exit-TacticsGodotOperationLock {
    param($Lock)
    if ($null -eq $Lock) { return }
    try { $Lock.Mutex.ReleaseMutex() }
    finally { $Lock.Mutex.Dispose() }
}

function Get-TacticsGodotEditorProcess {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    $resolved = (Resolve-Path -LiteralPath $ProjectRoot).Path.Replace('\', '/').TrimEnd('/')
    @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
        if ($_.Name -notlike 'Godot*' -or -not $_.CommandLine) { return $false }
        $normalizedCommandLine = $_.CommandLine.Replace('\', '/')
        $normalizedCommandLine.IndexOf($resolved, [StringComparison]::OrdinalIgnoreCase) -ge 0
    })
}

Export-ModuleMember -Function Get-TacticsGodotLaunchArgument, Get-TacticsGodotWorktreeIdentity, Enter-TacticsGodotOperationLock, Exit-TacticsGodotOperationLock, Get-TacticsGodotEditorProcess
