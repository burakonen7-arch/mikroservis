param(
    [string]$ProductName = ('Demo Klavye ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$values = @{}
foreach ($line in (Get-Content -LiteralPath (Join-Path $projectRoot '.env'))) {
    $parts = $line -split '=', 2
    if ($parts.Count -eq 2 -and -not $line.TrimStart().StartsWith('#')) {
        $values[$parts[0].Trim()] = $parts[1].Trim()
    }
}
if (-not $values['RABBITMQ_PASSWORD']) { throw '.env dosyasinda RABBITMQ_PASSWORD gerekli.' }
$accessToken = & (Join-Path $PSScriptRoot 'get-dev-token.ps1') -Role admin
$apiHeaders = @{ Authorization = 'Bearer ' + $accessToken }
$createHeaders = @{ Authorization = 'Bearer ' + $accessToken; 'Idempotency-Key' = ('demo-' + [Guid]::NewGuid()) }

foreach ($port in @(5001, 5002)) {
    $null = Invoke-RestMethod -Uri "http://localhost:$port/health/ready" -TimeoutSec 10
}

# Her olaydan bir kopya tutar; uygulama kuyruklari normal tuketime devam eder.
# En fazla 100 mesaj, 24 saat: gelistirme demosu icin sinirli saklama.
$basicAuth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(
    'mikroservis:' + $values['RABBITMQ_PASSWORD']))
$headers = @{ Authorization = 'Basic ' + $basicAuth }
$management = 'http://localhost:15672/api'
$queue = 'demo.messages'
$queueBody = @{ durable = $true; auto_delete = $false; arguments = @{
    'x-max-length' = 100; 'x-message-ttl' = 86400000
} } | ConvertTo-Json -Depth 4
$null = Invoke-RestMethod -Method Put -Uri "$management/queues/%2F/$queue" `
    -Headers $headers -ContentType 'application/json' -Body $queueBody -TimeoutSec 10
foreach ($topic in @('product.created', 'stock.changed')) {
    $binding = @{ routing_key = $topic; arguments = @{} } | ConvertTo-Json
    $null = Invoke-RestMethod -Method Post `
        -Uri "$management/bindings/%2F/e/microservices/q/$queue" `
        -Headers $headers -ContentType 'application/json' -Body $binding -TimeoutSec 10
}

$productBody = @{ name = $ProductName; price = 1250.50 } | ConvertTo-Json
$product = Invoke-RestMethod -Method Post -Uri 'http://localhost:5000/products' `
    -Headers $createHeaders -ContentType 'application/json' -Body $productBody -TimeoutSec 10
Write-Output "Urun olusturuldu: $($product.id)"

$stock = $null
for ($attempt = 0; $attempt -lt 40; $attempt++) {
    try {
        $stock = Invoke-RestMethod -Uri "http://localhost:5000/stocks/$($product.id)" -Headers $apiHeaders -TimeoutSec 5
        break
    } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) { throw }
        Start-Sleep -Milliseconds 1000
    }
}
if (-not $stock) { throw 'Stock kaydi olusmadi; stock-api loglarini kontrol edin.' }

$adjustment = @{ delta = 10; expectedVersion = $stock.version } | ConvertTo-Json
$stock = Invoke-RestMethod -Method Post -Uri "http://localhost:5000/stocks/$($product.id)/adjustments" `
    -Headers $apiHeaders -ContentType 'application/json' -Body $adjustment -TimeoutSec 10

$projected = $false
for ($attempt = 0; $attempt -lt 40; $attempt++) {
    $product = Invoke-RestMethod -Uri "http://localhost:5000/products/$($product.id)" -Headers $apiHeaders -TimeoutSec 5
    if ($product.availableQuantity -eq $stock.quantity -and $product.stockVersion -eq $stock.version) {
        $projected = $true
        break
    }
    Start-Sleep -Milliseconds 1000
}
if (-not $projected) { throw 'Mongo stok projeksiyonu guncellenmedi; product-api loglarini kontrol edin.' }

$result = [ordered]@{ product = $product; stock = $stock; rabbitQueue = $queue }
$stateDirectory = Join-Path $projectRoot '.local'
$null = New-Item -ItemType Directory -Force -Path $stateDirectory
$result | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $stateDirectory 'demo-result.json')
$result | ConvertTo-Json -Depth 5
Write-Output 'RabbitMQ: http://localhost:15672/#/queues/%2F/demo.messages'
Write-Output 'Get messages: Ack mode = requeue true, Messages = 10.'
