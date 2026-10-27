# beforeMCPExecution guard for Cursor Agent Chat.
# Reads alwaysForbiddenTools from the godot-tooling manifest (same source as Codex).
# Fail-closed: unreadable input, missing manifest, or empty deny-list -> deny.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-GuardDecision {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('allow', 'deny')]
        [string]$Permission,

        [string]$UserMessage = '',
        [string]$AgentMessage = ''
    )

    $payload = [ordered]@{ permission = $Permission }
    if (-not [string]::IsNullOrWhiteSpace($UserMessage)) {
        $payload.user_message = $UserMessage
    }
    if (-not [string]::IsNullOrWhiteSpace($AgentMessage)) {
        $payload.agent_message = $AgentMessage
    }
    # Cursor expects a single JSON object on stdout.
    [Console]::Out.WriteLine(($payload | ConvertTo-Json -Compress))
}

function Deny-AndExit {
    param(
        [Parameter(Mandatory = $true)][string]$UserMessage,
        [Parameter(Mandatory = $true)][string]$AgentMessage
    )

    Write-GuardDecision -Permission deny -UserMessage $UserMessage -AgentMessage $AgentMessage
    exit 2
}

function Read-HookStdin {
    # Cursor pipes JSON into the powershell process. Prefer Console.In so
    # `powershell -File ...` works both under Cursor and in subprocess tests.
    # Fall back to $input for hosts that only feed the pipeline enumerator.
    if ([Console]::IsInputRedirected) {
        $text = [Console]::In.ReadToEnd()
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            return [string]$text
        }
    }
    $pipeline = @($input)
    if ($pipeline.Count -gt 0) {
        return [string]($pipeline -join "`n")
    }
    return ''
}

try {
    $raw = Read-HookStdin
    if ([string]::IsNullOrWhiteSpace($raw)) {
        Deny-AndExit `
            -UserMessage 'Cursor MCP guard rejected an empty hook payload.' `
            -AgentMessage 'beforeMCPExecution received empty stdin; refusing the MCP call (fail-closed).'
    }

    try {
        $payload = $raw | ConvertFrom-Json
    }
    catch {
        Deny-AndExit `
            -UserMessage 'Cursor MCP guard rejected an invalid hook payload.' `
            -AgentMessage 'beforeMCPExecution stdin was not valid JSON; refusing the MCP call (fail-closed).'
    }

    $toolName = ''
    if ($null -ne $payload.PSObject.Properties['tool_name'] -and $null -ne $payload.tool_name) {
        $toolName = [string]$payload.tool_name
    }
    if ([string]::IsNullOrWhiteSpace($toolName)) {
        Deny-AndExit `
            -UserMessage 'Cursor MCP guard rejected a call without a tool name.' `
            -AgentMessage 'beforeMCPExecution payload is missing tool_name; refusing the MCP call (fail-closed).'
    }

    $manifestPath = $env:TACTICS_GODOT_AI_MANIFEST
    if ([string]::IsNullOrWhiteSpace($manifestPath)) {
        $manifestPath = Join-Path $PSScriptRoot '..\migration\manifest\godot-tooling.json'
    }
    $manifestPath = [System.IO.Path]::GetFullPath($manifestPath)
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Deny-AndExit `
            -UserMessage 'Cursor MCP guard could not find the godot-tooling manifest.' `
            -AgentMessage "Manifest missing at $manifestPath; refusing the MCP call (fail-closed)."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $forbidden = @()
    if (
        $null -ne $manifest.godotAi -and
        $null -ne $manifest.godotAi.codexMcp -and
        $null -ne $manifest.godotAi.codexMcp.alwaysForbiddenTools
    ) {
        $forbidden = @(
            $manifest.godotAi.codexMcp.alwaysForbiddenTools |
                ForEach-Object { [string]$_ } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )
    }
    if ($forbidden.Count -eq 0) {
        Deny-AndExit `
            -UserMessage 'Cursor MCP guard found an empty alwaysForbiddenTools list.' `
            -AgentMessage 'godotAi.codexMcp.alwaysForbiddenTools is empty or missing; refusing the MCP call (fail-closed).'
    }

    if ($forbidden -contains $toolName) {
        Deny-AndExit `
            -UserMessage "Blocked permanently-forbidden Godot AI tool: $toolName" `
            -AgentMessage ("Tool '$toolName' is listed in alwaysForbiddenTools. " +
                'Cursor has no Codex-style enabled_tools allowlist; this project hook hard-blocks the permanent deny list only. ' +
                'Use apply_patch, Build, ResourceSaver, and project sync scripts instead.')
    }

    Write-GuardDecision -Permission allow
    exit 0
}
catch {
    Write-GuardDecision `
        -Permission deny `
        -UserMessage 'Cursor MCP guard failed closed.' `
        -AgentMessage ("Guard script error: $($_.Exception.Message). Refusing the MCP call (fail-closed).")
    exit 2
}
