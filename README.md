# ServiceDesk

## Local Development / Manual Testing

ServiceDesk uses SQL Server LocalDB for the local manual-testing workflow. The helper scripts use a development-only database named `ServiceDeskLocal`; they do not delete or recreate it.

Prerequisites:

- .NET 10 SDK
- EF Core CLI tooling (`dotnet ef`)
- SQL Server Express LocalDB

From the repository root, run the one-time bootstrap helper first:

```powershell
.\scripts\bootstrap-local.ps1
```

It prompts for the first Administrator's name, email, and password without echoing the password. It applies pending migrations to `ServiceDeskLocal` and runs the application's existing bootstrap command. Bootstrap succeeds only while the installation contains no Users.

Then start the API:

```powershell
.\scripts\run-local.ps1
```

The script applies pending migrations to the same `ServiceDeskLocal` database and starts the existing `http` launch profile at `http://localhost:5011`. It uses `Jwt__SigningKey` from the current environment when available. Otherwise, it creates a cryptographically random, temporary key only for that run; tokens issued with it become invalid after a restart that generates a different key. No signing key is written to source-controlled configuration.

Current HTTP operations:

- `POST http://localhost:5011/api/users` creates a User.
- `POST http://localhost:5011/api/auth/login` authenticates a User with a credential and returns an access token.

There is no Swagger UI yet. A Customer created through `POST /api/users` cannot authenticate yet because normal credential provisioning is not implemented.
