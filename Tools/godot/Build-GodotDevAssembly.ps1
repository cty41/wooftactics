[CmdletBinding()]
param(
    [string]$Token,
    [string]$ResultPath,
    [string]$LogPath,
    [object]$HeldOperationLock
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $repoRoot 'godot')).Path
$adapterProject = (Resolve-Path -LiteralPath (Join-Path $projectRoot 'Tactics.Godot.Adapter.csproj')).Path
$assemblyPath = Join-Path $projectRoot '.godot\mono\temp\bin\Debug\Tactics.Godot.Adapter.dll'
$manifestPath = Join-Path $repoRoot 'Tools\migration\manifest\godot-tooling.json'
$sessionModule = Join-Path $PSScriptRoot 'GodotDevSession.psm1'
Import-Module $sessionModule -Force

if ([string]::IsNullOrWhiteSpace($Token)) { $Token = [guid]::NewGuid().ToString('N') }
$operationLock = $null
$assemblyIdentity = $null
$firstError = $null
$exitCode = 1
$status = 'failed'

function Write-BuildResult {
    param([Parameter(Mandatory = $true)][int]$ResultExitCode)
    if ([string]::IsNullOrWhiteSpace($ResultPath)) { return }

    $resultDirectory = Split-Path -Parent $ResultPath
    if ([string]::IsNullOrWhiteSpace($resultDirectory)) { $resultDirectory = (Get-Location).Path }
    New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
    $temporaryResultPath = Join-Path $resultDirectory ('.' + [IO.Path]::GetFileName($ResultPath) + '.' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [ordered]@{
            schemaVersion = 1
            token = $Token
            status = $status
            exitCode = $ResultExitCode
            assemblyPath = $assemblyPath
            assemblyIdentity = $assemblyIdentity
            firstError = $firstError
            logPath = if ([string]::IsNullOrWhiteSpace($LogPath)) { $null } else { $LogPath }
            completedAt = [DateTimeOffset]::Now.ToString('o')
        } | ConvertTo-Json | Set-Content -LiteralPath $temporaryResultPath -Encoding UTF8
        Move-Item -LiteralPath $temporaryResultPath -Destination $ResultPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryResultPath) { Remove-Item -LiteralPath $temporaryResultPath -Force }
    }
}

try {
    foreach ($required in @($adapterProject, $manifestPath)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required Godot build file is missing: $required" }
    }

    $worktreeIdentity = Get-TacticsGodotWorktreeIdentity -RepoRoot $repoRoot
    if ($null -ne $HeldOperationLock) {
        if ($null -eq $HeldOperationLock.Identity -or
            $HeldOperationLock.Identity.RepoRoot -ne $repoRoot -or
            $HeldOperationLock.Identity.Key -ne $worktreeIdentity.Key -or
            $HeldOperationLock.Identity.MutexName -ne $worktreeIdentity.MutexName -or
            $HeldOperationLock.Mutex -isnot [Threading.Mutex] -or
            $HeldOperationLock.Mutex.SafeWaitHandle.IsClosed -or
            $HeldOperationLock.Mutex.SafeWaitHandle.IsInvalid) {
            throw 'HeldOperationLock must be the live operation lock for this repository.'
        }
    }
    else {
        $operationLock = Enter-TacticsGodotOperationLock -RepoRoot $repoRoot
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $expectedSdk = [Version][string]$manifest.dotnetSdk
    Push-Location $repoRoot
    try { $actualSdkText = (dotnet --version).Trim() }
    finally { Pop-Location }
    $actualSdk = [Version]$actualSdkText
    $expectedFeatureBand = [Math]::Floor($expectedSdk.Build / 100)
    $actualFeatureBand = [Math]::Floor($actualSdk.Build / 100)
    if ($actualSdk.Major -ne $expectedSdk.Major -or $actualSdk.Minor -ne $expectedSdk.Minor -or $actualFeatureBand -ne $expectedFeatureBand) {
        throw "Expected .NET SDK feature band $($expectedSdk.Major).$($expectedSdk.Minor).$($expectedFeatureBand)xx, found $actualSdkText."
    }

    Push-Location $repoRoot
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try { $buildOutput = @(& dotnet build $adapterProject -c Debug -m:1 2>&1) }
        finally { $ErrorActionPreference = $previousErrorActionPreference }
        $exitCode = $LASTEXITCODE
    }
    finally { Pop-Location }

    $buildOutput | ForEach-Object { Write-Output $_ }
    if (-not [string]::IsNullOrWhiteSpace($LogPath)) {
        $logDirectory = Split-Path -Parent $LogPath
        if (-not [string]::IsNullOrWhiteSpace($logDirectory)) { New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null }
        $buildOutput | Set-Content -LiteralPath $LogPath -Encoding UTF8
    }
    $firstBuildError = $buildOutput | ForEach-Object { [string]$_ } | Where-Object { $_ -match ': error ' } | Select-Object -First 1
    if ($exitCode -ne 0) {
        if ($null -ne $firstBuildError) { $firstError = $firstBuildError.Trim() }
        else { $firstError = "Godot Adapter Debug build failed with exit code $exitCode." }
        throw $firstError
    }

    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) { throw "Production Adapter assembly is missing: $assemblyPath" }
    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
    $assemblyIdentity = $assemblyName.FullName
    if ($assemblyName.Name -ne 'Tactics.Godot.Adapter') { throw "Unexpected Adapter assembly identity: $assemblyIdentity" }

    $status = 'succeeded'
    $exitCode = 0
}
catch {
    if ([string]::IsNullOrWhiteSpace($firstError)) { $firstError = $_.Exception.Message }
    if ($exitCode -eq 0) { $exitCode = 1 }
    if (-not [string]::IsNullOrWhiteSpace($LogPath)) {
        try {
            $logDirectory = Split-Path -Parent $LogPath
            if (-not [string]::IsNullOrWhiteSpace($logDirectory)) { New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null }
            "BUILD_HELPER_FAILURE: $firstError" | Add-Content -LiteralPath $LogPath -Encoding UTF8
        }
        catch {
            [Console]::Error.WriteLine("Could not append failure to build log '$LogPath': $($_.Exception.Message)")
        }
    }
    [Console]::Error.WriteLine($firstError)
}
finally {
    try { Write-BuildResult -ResultExitCode $exitCode }
    catch { [Console]::Error.WriteLine("Could not write build result '$ResultPath': $($_.Exception.Message)") }
    Exit-TacticsGodotOperationLock -Lock $operationLock
}

exit $exitCode
