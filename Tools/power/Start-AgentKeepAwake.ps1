[CmdletBinding()]
param(
    [int]$Minutes = 120
)

$ErrorActionPreference = 'Stop'
$stateDir = Join-Path $PSScriptRoot '.state'
$stateFile = Join-Path $stateDir 'keepawake.json'
$holderScript = Join-Path $stateDir 'holder.ps1'

if (Test-Path $stateFile) {
    $state = Get-Content -Path $stateFile -Raw | ConvertFrom-Json
    $existing = Get-Process -Id $state.Pid -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "KeepAwake 已在运行：PID $($state.Pid)，到期 $($state.Deadline)。无需重复启动。"
        exit 0
    }
    Write-Host "清理过期状态：PID $($state.Pid) 已不存在。"
    Remove-Item -Path $stateFile -Force
}

New-Item -ItemType Directory -Force -Path $stateDir | Out-Null

$deadlineText = ''
if ($Minutes -gt 0) {
    $deadlineText = (Get-Date).AddMinutes($Minutes).ToString('o')
}

$holderBody = @'
param([string]$Deadline)
$ErrorActionPreference = 'Stop'
$sig = '[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)] public static extern uint SetThreadExecutionState(uint esFlags);'
Add-Type -MemberDefinition $sig -Name Power -Namespace Win32 | Out-Null
$flags = [uint32]2147483648 -bor [uint32]1
$deadlineTime = [datetime]::MaxValue
if ($Deadline) {
    $deadlineTime = [datetime]::Parse($Deadline)
}
while ((Get-Date) -lt $deadlineTime) {
    [void][Win32.Power]::SetThreadExecutionState($flags)
    Start-Sleep -Seconds 30
}
'@

Set-Content -Path $holderScript -Value $holderBody -Encoding UTF8

$argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $holderScript)
if ($deadlineText) {
    $argList += @('-Deadline', $deadlineText)
}
$holder = Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -WindowStyle Hidden -PassThru

Start-Sleep -Seconds 2
if (-not (Get-Process -Id $holder.Id -ErrorAction SilentlyContinue)) {
    Remove-Item -Path $holderScript -Force -ErrorAction SilentlyContinue
    throw "KeepAwake 持有进程启动后立即退出，启动失败；请检查 holder 脚本错误输出。"
}

$state = @{
    Pid       = $holder.Id
    Deadline  = $deadlineText
    StartedAt = (Get-Date).ToString('o')
}
$state | ConvertTo-Json | Set-Content -Path $stateFile -Encoding UTF8

Write-Host "KeepAwake 已启动：PID $($holder.Id)。"
if ($deadlineText) {
    Write-Host "到期时间：$deadlineText（到点自动释放）。"
} else {
    Write-Host "未设置到期时间，请务必运行 Stop-AgentKeepAwake.ps1 结束。"
}
Write-Host "注意：本工具不修改电源计划，屏幕仍按原设置关闭；仅阻止空闲休眠（Modern Standby）。"

Write-Host '--- powercfg /requests（SYSTEM 块应可见 powershell 进程；需管理员权限，失败不影响功能）---'
try {
    powercfg /requests 2>&1 | Out-String | Write-Host
} catch {
    Write-Host "（无管理员权限，跳过 /requests 核验；以心跳连续性为权威证据）"
}
