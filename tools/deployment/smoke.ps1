param(
    [string]$Image = 'constant-learning:t45-smoke',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$network = "constant-learning-smoke-$suffix"
$database = "constant-learning-db-$suffix"
$application = "constant-learning-app-$suffix"
$password = [Guid]::NewGuid().ToString('N')

function Invoke-Docker {
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw "Команда Docker завершилася з кодом $LASTEXITCODE." }
}

try {
    if (!$SkipBuild) {
        Invoke-Docker build --target container-smoke -f (Join-Path $taskRoot 'ConstantLearning/Dockerfile') -t $Image $taskRoot
    }
    Invoke-Docker network create --internal $network
    Invoke-Docker run --detach --name $database --network $network --network-alias database --env POSTGRES_DB=container_smoke --env POSTGRES_USER=smoke --env "POSTGRES_PASSWORD=$password" postgres:16-alpine
    $databaseReady = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & docker exec $database pg_isready -U smoke -d container_smoke *> $null
        if ($LASTEXITCODE -eq 0) { $databaseReady = $true; break }
        Start-Sleep -Seconds 1
    }
    if (!$databaseReady) { throw 'Тестовий PostgreSQL не запустився.' }
    Invoke-Docker run --detach --name $application --network $network --env "ConnectionStrings__DefaultConnection=Host=database;Database=container_smoke;Username=smoke;Password=$password" $Image
    $applicationReady = $false
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        $logs = & docker logs $application 2>&1
        if ($LASTEXITCODE -ne 0) { throw 'Не вдалося прочитати журнал контейнера.' }
        if (($logs -join "`n") -match 'CONTAINER_SMOKE_READY=8888') { $applicationReady = $true; break }
        $state = & docker inspect --format '{{.State.Running}}' $application
        if ($state -ne 'true') { throw "Контейнер зупинився:`n$($logs -join "`n")" }
        Start-Sleep -Seconds 1
    }
    if (!$applicationReady) { throw "Host не запустився:`n$($logs -join "`n")" }
    $logs | Select-String 'TIMEZONE_OK|ICU_OK|CONTAINER_SMOKE_READY'
    Invoke-Docker run --rm --network $network $Image --probe "http://${application}:8888/api/telegram/health"
}
finally {
    # Видаляємо лише ресурси з випадковими іменами, створеними цим запуском, разом із томом БД.
    & docker rm --force --volumes $application $database 2>$null | Out-Null
    & docker network rm $network 2>$null | Out-Null
}
