param(
    [string]$SkillsRoot = ".agents/skills"
)

$ErrorActionPreference = "Stop"

$allowedFrontmatter = @("name", "description")
$requiredSections = @("Quick Reference", "When to use", "Workflow")
$recommendedSections = @("Anti-patterns", "Checklist")
$namePattern = '^[a-z0-9]+(-[a-z0-9]+)*$'
$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

if (-not (Test-Path -LiteralPath $SkillsRoot)) {
    throw "Skills root not found: $SkillsRoot"
}

$files = Get-ChildItem -Path $SkillsRoot -Recurse -Filter SKILL.md | Sort-Object FullName

foreach ($file in $files) {
    $prefixBytes = @()
    $bomStream = [System.IO.File]::OpenRead($file.FullName)
    try {
        $bomBuffer = New-Object byte[] 3
        $bomRead = $bomStream.Read($bomBuffer, 0, 3)
        if ($bomRead -gt 0) {
            $prefixBytes = @($bomBuffer[0..($bomRead - 1)])
        }
    } finally {
        $bomStream.Dispose()
    }
    $hasUtf8Bom = $prefixBytes.Count -ge 3 -and $prefixBytes[0] -eq 0xEF -and $prefixBytes[1] -eq 0xBB -and $prefixBytes[2] -eq 0xBF
    if ($hasUtf8Bom) {
        $rel = Resolve-Path -Relative $file.FullName
        $errors.Add("${rel}: file starts with UTF-8 BOM; frontmatter must begin with --- at byte 0")
    }

    $text = Get-Content -Raw -LiteralPath $file.FullName
    $rel = Resolve-Path -Relative $file.FullName
    $dirName = Split-Path -Leaf $file.DirectoryName

    if ($text -notmatch '(?s)^---\s*\r?\n(.*?)\r?\n---') {
        $errors.Add("${rel}: missing YAML frontmatter")
        continue
    }

    $frontmatter = $matches[1]
    $fields = New-Object System.Collections.Generic.List[string]
    foreach ($line in ($frontmatter -split "\r?\n")) {
        if ($line -match '^\s*([A-Za-z0-9_-]+)\s*:') {
            $fields.Add($matches[1])
        }
    }

    foreach ($field in $fields) {
        if ($allowedFrontmatter -notcontains $field) {
            $errors.Add("${rel}: unsupported frontmatter field '$field' for Codex/OpenCode compatibility")
        }
    }

    if ($frontmatter -notmatch '(?m)^name:\s*(.+?)\s*$') {
        $errors.Add("${rel}: missing name")
    } else {
        $name = $matches[1].Trim().Trim('"').Trim("'")
        if ($name -ne $dirName) {
            $errors.Add("${rel}: name '$name' does not match directory '$dirName'")
        }
        if ($name -notmatch $namePattern) {
            $errors.Add("${rel}: name '$name' does not match $namePattern")
        }
    }

    if ($frontmatter -notmatch '(?m)^description:\s*(.+?)\s*$') {
        $errors.Add("${rel}: missing description")
    } else {
        $description = $matches[1].Trim().Trim('"').Trim("'")
        if ($description.Length -lt 1 -or $description.Length -gt 1024) {
            $errors.Add("${rel}: description length must be 1-1024 characters")
        }
        if ($description -notmatch '(?i)\buse when\b|使用|用于|需要') {
            $warnings.Add("${rel}: description may not clearly state trigger conditions")
        }
    }

    foreach ($section in $requiredSections) {
        if ($text -notmatch "(?m)^##\s+$([regex]::Escape($section))\s*$") {
            $errors.Add("${rel}: missing required section '$section'")
        }
    }

    foreach ($section in $recommendedSections) {
        if ($text -notmatch "(?m)^##\s+$([regex]::Escape($section))\s*$") {
            $warnings.Add("${rel}: missing recommended section '$section'")
        }
    }
}

foreach ($warning in $warnings) {
    Write-Output "WARNING: $warning"
}

if ($errors.Count -gt 0) {
    foreach ($err in $errors) {
        Write-Output "ERROR: $err"
    }
    exit 1
}

Write-Output "Skill validation passed: $($files.Count) skill(s) checked."
if ($warnings.Count -gt 0) {
    Write-Output "Warnings: $($warnings.Count)"
}
