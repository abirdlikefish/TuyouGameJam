$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$serverPath = Join-Path $repositoryRoot "Luban.Mcp\Luban.Mcp.exe"

if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
    Write-Error "Luban MCP executable was not found: $serverPath"
    exit 1
}

Set-Location -LiteralPath $repositoryRoot
& $serverPath
exit $LASTEXITCODE

