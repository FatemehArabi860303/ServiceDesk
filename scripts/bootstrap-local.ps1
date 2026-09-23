Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=ServiceDeskLocal;Trusted_Connection=True;MultipleActiveResultSets=True"
$originalConnectionString = [Environment]::GetEnvironmentVariable("ConnectionStrings__ServiceDesk", "Process")
$bootstrapEnvironmentNames = @(
    "BootstrapAdministrator__FirstName",
    "BootstrapAdministrator__LastName",
    "BootstrapAdministrator__Email",
    "BootstrapAdministrator__Password"
)
$originalBootstrapEnvironment = @{}
$passwordBstr = [IntPtr]::Zero

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

    $firstName = Read-Host "Administrator first name"
    $lastName = Read-Host "Administrator last name"
    $email = Read-Host "Administrator email"
    $securePassword = Read-Host "Administrator password" -AsSecureString
    $passwordBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordBstr)

    if ([string]::IsNullOrWhiteSpace($password)) {
        throw "Administrator password is required."
    }

    $env:ConnectionStrings__ServiceDesk = $connectionString
    foreach ($name in $bootstrapEnvironmentNames) {
        $originalBootstrapEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
    }

    $env:BootstrapAdministrator__FirstName = $firstName
    $env:BootstrapAdministrator__LastName = $lastName
    $env:BootstrapAdministrator__Email = $email
    $env:BootstrapAdministrator__Password = $password

    Push-Location $repositoryRoot
    try {
        & dotnet ef database update --project ".\src\ServiceDesk.Repository" --startup-project ".\src\ServiceDesk.Repository" --connection $connectionString
        if ($LASTEXITCODE -ne 0) {
            throw "Applying EF Core migrations to ServiceDeskLocal failed."
        }

        & dotnet run --project ".\src\ServiceDesk.WebApi" -- --bootstrap-administrator
        if ($LASTEXITCODE -ne 0) {
            throw "First Administrator bootstrap failed. Check that ServiceDeskLocal is empty and that the entered data meets the application rules."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($passwordBstr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordBstr)
    }

    foreach ($name in $bootstrapEnvironmentNames) {
        if ($null -eq $originalBootstrapEnvironment[$name]) {
            Remove-Item "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            Set-Item "Env:$name" $originalBootstrapEnvironment[$name]
        }
    }

    if ($null -eq $originalConnectionString) {
        Remove-Item Env:ConnectionStrings__ServiceDesk -ErrorAction SilentlyContinue
    }
    else {
        Set-Item Env:ConnectionStrings__ServiceDesk $originalConnectionString
    }
}
