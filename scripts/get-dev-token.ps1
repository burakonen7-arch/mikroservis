param([ValidateSet('admin','reader','inventory','catalog')][string]$Role = 'admin')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$values = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $projectRoot '.env')) {
    $parts = $line -split '=', 2
    if ($parts.Count -eq 2 -and -not $line.TrimStart().StartsWith('#')) { $values[$parts[0].Trim()] = $parts[1].Trim() }
}
$password = $values['AUTH_' + $Role.ToUpperInvariant() + '_PASSWORD']
if (-not $password) { throw 'Run scripts/prepare-debug.ps1 to generate development credentials.' }
$body = @{ username=$Role; password=$password } | ConvertTo-Json -Compress
$response = Invoke-RestMethod -Uri 'http://localhost:5000/auth/token' -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 10 -Proxy $null
$response.accessToken
