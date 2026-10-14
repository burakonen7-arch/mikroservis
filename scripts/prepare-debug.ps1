param([switch]$SkipInfrastructure)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $envPath)) { throw 'Once .env gelistirme parolalarini olusturun.' }

$dockerExecutable = $null
if (-not $SkipInfrastructure) {
    $dockerCommand = Get-Command docker.exe -CommandType Application -ErrorAction SilentlyContinue
    if ($dockerCommand) {
        $dockerExecutable = $dockerCommand.Source
    } else {
        $dockerCandidates = @(
            (Join-Path $env:LOCALAPPDATA 'Programs/DockerDesktop/resources/bin/docker.exe'),
            (Join-Path $env:ProgramFiles 'Docker/Docker/resources/bin/docker.exe')
        )
        $dockerExecutable = $dockerCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (-not $dockerExecutable) {
        throw 'Docker CLI bulunamadi. Docker Desktop kurun veya docker.exe yolunu PATH ayarina ekleyin.'
    }
    # Fail before stopping APIs or changing local settings when the engine is unavailable.
    try {
        $null = & $dockerExecutable info --format '{{.ServerVersion}}' 2>&1
        if ($LASTEXITCODE -ne 0) { throw 'Docker engine unavailable.' }
    } catch {
        throw 'Docker motoru hazir degil. Docker Desktop Linux engine baslatildiktan sonra scripti yeniden calistirin.'
    }
}

$values = @{}
foreach ($line in (Get-Content -LiteralPath $envPath)) {
    $parts = $line -split '=', 2
    if ($parts.Count -eq 2 -and -not $line.TrimStart().StartsWith('#')) {
        $values[$parts[0].Trim()] = $parts[1].Trim()
    }
}
foreach ($key in @('RABBITMQ_PASSWORD', 'ORACLE_APP_PASSWORD')) {
    if (-not $values[$key]) { throw ".env dosyasinda $key gerekli." }
}

$secretNames = @('JWT_SIGNING_KEY', 'AUTH_ADMIN_PASSWORD', 'AUTH_READER_PASSWORD', 'AUTH_INVENTORY_PASSWORD', 'AUTH_CATALOG_PASSWORD')
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    foreach ($secretName in $secretNames) {
        if (-not $values[$secretName]) {
            $bytes = New-Object byte[] 32
            $random.GetBytes($bytes)
            $values[$secretName] = [Convert]::ToBase64String($bytes)
            [IO.File]::AppendAllText($envPath, "`n$secretName=" + $values[$secretName] + "`n", [Text.UTF8Encoding]::new($false))
        }
    }
} finally { $random.Dispose() }

$stateDirectory = Join-Path $projectRoot '.local'
$null = New-Item -ItemType Directory -Force -Path $stateDirectory
$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($service in @('Product', 'Stock', 'Gateway')) {
    $port = switch ($service) { 'Product' {5001} 'Stock' {5002} 'Gateway' {5000} }
    $settings = [ordered]@{
        ASPNETCORE_ENVIRONMENT = 'Development'
        ASPNETCORE_URLS = "http://localhost:$port"
        Jwt__Issuer = 'mikroservis'
        Jwt__Audience = 'mikroservis-api'
        Jwt__SigningKey = $values['JWT_SIGNING_KEY']
        OTEL_EXPORTER_OTLP_ENDPOINT = 'http://localhost:4317'
        OTEL_EXPORTER_OTLP_PROTOCOL = 'grpc'
        'Logging__LogLevel__Microsoft.EntityFrameworkCore' = 'Warning'
        'Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command' = 'Information'
        'Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics' = 'Information'
        'Logging__LogLevel__Microsoft.AspNetCore.Routing.EndpointMiddleware' = 'Information'
    }
    if ($service -eq 'Gateway') {
        $settings['Logging__LogLevel__Yarp.ReverseProxy'] = 'Debug'
        foreach ($role in @('admin','reader','inventory','catalog')) {
            $settings['DevelopmentAuth__' + $role + '__Password'] = $values['AUTH_' + $role.ToUpperInvariant() + '_PASSWORD']
        }
    } else {
        $settings['ConnectionStrings__Database'] = if ($service -eq 'Product') {
            'mongodb://localhost:27017/?replicaSet=rs0&directConnection=true'
        } else {
            'User Id=stock;Password=' + $values['ORACLE_APP_PASSWORD'] + ';Data Source=localhost:1521/FREEPDB1'
        }
        $settings['ConnectionStrings__Redis'] = 'localhost:6379,abortConnect=false'
        $settings['RabbitMQ__Uri'] = 'amqp://mikroservis:' + $values['RABBITMQ_PASSWORD'] + '@localhost:5672'
        $settings['Database__Initialize'] = 'true'
    }
    $content = ($settings.GetEnumerator() | ForEach-Object { $_.Key + '=' + $_.Value }) -join "`n"
    [IO.File]::WriteAllText((Join-Path $stateDirectory ($service.ToLowerInvariant() + '-debug.env')), $content + "`n", $utf8)
}

if (-not $SkipInfrastructure) {
foreach ($service in @('Gateway','Product','Stock')) {
    $pidPath = Join-Path $stateDirectory ($service + '.pid')
    if (-not (Test-Path -LiteralPath $pidPath)) { continue }
    $servicePid = 0
    if (-not [int]::TryParse([IO.File]::ReadAllText($pidPath).Trim(), [ref]$servicePid)) { continue }
    $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId=$servicePid" -ErrorAction SilentlyContinue
    $assemblyPath = Join-Path $projectRoot "src/$service/$service.Api/bin/Release/net10.0/$service.Api.dll"
    if ($processInfo.CommandLine -and $processInfo.CommandLine.IndexOf($assemblyPath, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Stop-Process -Id $servicePid
    }
    Remove-Item -LiteralPath $pidPath
}
Push-Location $projectRoot
try {
    & $dockerExecutable compose up -d --wait mongo oracle redis redisinsight rabbitmq aspire-dashboard otel-collector
    if ($LASTEXITCODE -ne 0) { throw 'Docker altyapisi baslatilamadi.' }
    & $dockerExecutable compose run --rm mongo-init
    if ($LASTEXITCODE -ne 0) { throw 'Mongo replica set hazirlanamadi.' }
    & $dockerExecutable compose stop product-api stock-api gateway-api
    if ($LASTEXITCODE -ne 0) { throw 'Docker API servisleri durdurulamadi.' }
} finally {
    Pop-Location
}
}

if ($SkipInfrastructure) { Write-Output 'Yerel ayar dosyalari hazir. -SkipInfrastructure veritabanlarini veya Docker servislerini baslatmaz.' }
else { Write-Output 'Altyapi hazir. Rider: Mikroservis Debug secip Debug baslatin.' }
Write-Output 'Product: http://localhost:5001/swagger/index.html'
Write-Output 'Stock: http://localhost:5002/swagger/index.html'
Write-Output 'Gateway: http://localhost:5000/swagger/index.html'
Write-Output 'Development login: POST http://localhost:5000/auth/token (passwords are in ignored .env).'
Write-Output 'RedisInsight: http://localhost:5540'
Write-Output 'OpenTelemetry (Aspire): http://localhost:18888'
