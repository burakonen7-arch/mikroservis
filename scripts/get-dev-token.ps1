param(
    [ValidateSet('admin','reader','inventory','catalog')][string]$Role = 'admin',
    [ValidateRange(1, 600)][int]$TimeoutSec = 120
)
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
try {
    $response = Invoke-RestMethod -Uri 'http://localhost:5000/auth/token' -Method Post -ContentType 'application/json' -Body $body -TimeoutSec $TimeoutSec -Proxy $null
} catch [System.Net.WebException] {
    if ($_.Exception.Status -eq [System.Net.WebExceptionStatus]::ConnectFailure) {
        throw 'Gateway localhost:5000 adresinde calismiyor. Rider icinde Gateway Debug veya Mikroservis Debug oturumunu baslatin.'
    }
    if ($_.Exception.Status -eq [System.Net.WebExceptionStatus]::Timeout) {
        throw 'Token istegi zaman asimina ugradi. Gateway Debug login breakpointinde duruyorsa Resume yapin; gerekirse -TimeoutSec degerini artirin.'
    }
    throw
}
$response.accessToken
