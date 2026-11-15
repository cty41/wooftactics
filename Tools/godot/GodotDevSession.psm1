Set-StrictMode -Version Latest

function Test-TacticsGodotToolsWindowsPlatform {
    if ($null -ne $IsWindows) { return [bool]$IsWindows }
    return [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [Runtime.InteropServices.OSPlatform]::Windows)
}

function Test-TacticsGodotToolsLinuxPlatform {
    if ($null -ne $IsLinux) { return [bool]$IsLinux }
    return [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [Runtime.InteropServices.OSPlatform]::Linux)
}

function Test-TacticsGodotToolsMacOsPlatform {
    if ($null -ne $IsMacOS) { return [bool]$IsMacOS }
    return [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [Runtime.InteropServices.OSPlatform]::OSX)
}

function Get-TacticsProcessCommandLineFromProc {
    param([Parameter(Mandatory = $true)][int]$ProcessId)
    $cmdlinePath = "/proc/$ProcessId/cmdline"
    if (-not (Test-Path -LiteralPath $cmdlinePath)) { return $null }
    try {
        $raw = [IO.File]::ReadAllBytes($cmdlinePath)
    }
    catch { return $null }
    if ($raw.Length -eq 0) { return $null }
    $builder = [Text.StringBuilder]::new()
    foreach ($byte in $raw) {
        if ($byte -eq 0) {
            [void]$builder.Append(' ')
        }
        else {
            [void]$builder.Append([char]$byte)
        }
    }
    $commandLine = $builder.ToString().Trim()
    if ([string]::IsNullOrWhiteSpace($commandLine)) { return $null }
    return $commandLine
}

function Get-TacticsProcessRecords {
    if (Test-TacticsGodotToolsWindowsPlatform) {
        return @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)
    }

    $records = [System.Collections.Generic.List[object]]::new()
    if (Test-TacticsGodotToolsLinuxPlatform) {
        foreach ($procDir in @(Get-ChildItem -Path '/proc' -Directory -ErrorAction SilentlyContinue)) {
            if ($procDir.Name -notmatch '^\d+$') { continue }
            $processId = [int]$procDir.Name
            $commandLine = Get-TacticsProcessCommandLineFromProc -ProcessId $processId
            if ($null -eq $commandLine) { continue }
            $executableToken = ($commandLine -split '\s+', 2)[0]
            $name = [IO.Path]::GetFileName($executableToken)
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $records.Add([pscustomobject]@{
                    ProcessId   = $processId
                    Name        = $name
                    CommandLine = $commandLine
                })
        }
        return @($records)
    }

    if (Test-TacticsGodotToolsMacOsPlatform) {
        foreach ($process in @(Get-Process -ErrorAction SilentlyContinue)) {
            $commandLine = (& ps -p $process.Id -o args= 2>$null | Out-String).Trim()
            if ([string]::IsNullOrWhiteSpace($commandLine)) { continue }
            $executableToken = ($commandLine -split '\s+', 2)[0]
            $name = [IO.Path]::GetFileName($executableToken)
            if ([string]::IsNullOrWhiteSpace($name)) {
                $name = $process.ProcessName
            }
            $records.Add([pscustomobject]@{
                    ProcessId   = $process.Id
                    Name        = $name
                    CommandLine = $commandLine
                })
        }
        return @($records)
    }

    throw 'Unsupported platform for Tactics Godot process discovery.'
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
    @(Get-TacticsProcessRecords | Where-Object {
            if ($_.Name -notlike 'Godot*' -or -not $_.CommandLine) { return $false }
            $normalizedCommandLine = $_.CommandLine.Replace('\', '/')
            $normalizedCommandLine.IndexOf($resolved, [StringComparison]::OrdinalIgnoreCase) -ge 0
        })
}

Export-ModuleMember -Function Get-TacticsGodotWorktreeIdentity, Enter-TacticsGodotOperationLock, Exit-TacticsGodotOperationLock, Get-TacticsGodotEditorProcess
