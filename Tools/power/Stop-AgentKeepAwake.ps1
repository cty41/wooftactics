[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$stateDir = Join-Path $PSScriptRoot '.state'
$stateFile = Join-Path $stateDir 'keepawake.json'
$holderScript = Join-Path $stateDir 'holder.ps1'

if (-not (Test-Path $stateFile)) {
    Write-Host 'KeepAwake 未在运行（无状态文件），无需停止。'
    exit 0
}

$state = Get-Content -Path $stateFile -Raw | ConvertFrom-Json
$existing = Get-Process -Id $state.Pid -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Process -Id $state.Pid -Force
    Write-Host "KeepAwake 已停止：PID $($state.Pid)。请求随进程退出自动释放。"
} else {
    Write-Host "PID $($state.Pid) 已不存在，仅清理残留状态。"
}

Remove-Item -Path $stateFile -Force
if (Test-Path $holderScript) {
    Remove-Item -Path $holderScript -Force
}
Remove-Item -Path (Join-Path $stateDir 'holder.err.log'), (Join-Path $stateDir 'holder.out.log') -Force -ErrorAction SilentlyContinue

Write-Host '--- powercfg /requests（应不再出现该 powershell 进程；需管理员权限，失败不影响功能）---'
try {
    powercfg /requests 2>&1 | Out-String | Write-Host
} catch {
    Write-Host "（无管理员权限，跳过 /requests 核验；以心跳连续性为权威证据）"
}
