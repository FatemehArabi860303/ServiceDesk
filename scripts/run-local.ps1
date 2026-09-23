Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=ServiceDeskLocal;Trusted_Connection=True;MultipleActiveResultSets=True"
$originalConnectionString = [Environment]::GetEnvironmentVariable("ConnectionStrings__ServiceDesk", "Process")
$generatedSigningKey = $false

function Assert-LocalPrerequisites {
    if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "The .NET SDK is required. Install the .NET 10 SDK and try again."
    }

    if ($null -eq (Get-Command sqllocaldb.exe -ErrorAction SilentlyContinue)) {
        throw "SQL Server LocalDB is required. Install SQL Server Express LocalDB and try again."
    }

    & dotnet ef --version
    if ($LASTEXITCODE -ne 0) {
        throw "The EF Core CLI tooling is required. Install or restore dotnet-ef and try again."
    }
}

try {
    Assert-LocalPrerequisites

    $env:ConnectionStrings__ServiceDesk = $connectionString

    if ([string]::IsNullOrWhiteSpace($env:Jwt__SigningKey)) {
        $keyBytes = New-Object byte[] 32
        $randomNumberGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $randomNumberGenerator.GetBytes($keyBytes)
        }
        finally {
            $randomNumberGenerator.Dispose()
        }

        $env:Jwt__SigningKey = [Convert]::ToBase64String($keyBytes)
        $generatedSigningKey = $true
        Write-Host "A temporary local JWT signing key was generated for this run. Restarting with a new generated key invalidates previously issued tokens."
    }

    Push-Location $repositoryRoot
    try {
        & dotnet ef database update --project ".\src\ServiceDesk.Repository" --startup-project ".\src\ServiceDesk.Repository" --connection $connectionString
        if ($LASTEXITCODE -ne 0) {
            throw "Applying EF Core migrations to ServiceDeskLocal failed."
        }

        & dotnet run --project ".\src\ServiceDesk.WebApi" --launch-profile http
        if ($LASTEXITCODE -ne 0) {
            throw "ServiceDesk.WebApi stopped with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($generatedSigningKey) {
        Remove-Item Env:Jwt__SigningKey -ErrorAction SilentlyContinue
    }

    if ($null -eq $originalConnectionString) {
        Remove-Item Env:ConnectionStrings__ServiceDesk -ErrorAction SilentlyContinue
    }
    else {
        Set-Item Env:ConnectionStrings__ServiceDesk $originalConnectionString
    }
}
