param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("capabilities", "describe", "list-tables", "schema", "validate")]
    [string]$Mode,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AdditionalArguments
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..\..\..")).Path
$agentPath = Join-Path $repositoryRoot "Luban.Agent\Luban.Agent.exe"
$configPath = Join-Path $repositoryRoot "LubanData\luban.conf"

if (-not (Test-Path -LiteralPath $agentPath -PathType Leaf)) {
    Write-Error "Luban Agent executable was not found: $agentPath"
    exit 1
}

if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    Write-Error "Luban configuration was not found: $configPath"
    exit 1
}

$agentArguments = @($Mode, "--conf", $configPath, "-t", "client")
if ($AdditionalArguments) {
    $agentArguments += $AdditionalArguments
}

Set-Location -LiteralPath $repositoryRoot
& $agentPath @agentArguments
exit $LASTEXITCODE

