# Contributing

Thanks for improving the University Admission System. Keep changes focused,
testable, and safe for existing student and administrator workflows.

## Development setup

Use the .NET 8 SDK selected by `global.json`, then restore the local tools and
packages:

```bash
dotnet tool restore
dotnet restore tests/ANU_Admissions.UnitTests/ANU_Admissions.UnitTests.csproj
```

Configure SQL Server and administrator credentials as described in the main
README. Never commit `.env`, User Secrets, connection credentials, real student
records, or generated uploads.

### Updating dependencies

Package versions and their full transitive graphs are committed in
`packages.lock.json` files. After intentionally editing a `PackageReference`,
regenerate the affected lock file and commit both changes:

```bash
dotnet restore path/to/project.csproj --force-evaluate
```

Use `--locked-mode` when verifying a clean checkout. Do not edit lock files by
hand or add another package source without documenting why it is trusted.

## Before opening a pull request

Run the same fast checks as CI:

```bash
dotnet build tests/ANU_Admissions.UnitTests/ANU_Admissions.UnitTests.csproj --configuration Release --no-restore --warnaserror
dotnet run --project tests/ANU_Admissions.UnitTests/ANU_Admissions.UnitTests.csproj --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --no-build --configuration Release
```

SQL Server-specific changes should also run the provider tests. Set
`ANU_TEST_SQLSERVER` to a trusted development instance; each test creates and
deletes its own uniquely named database:

```powershell
$env:ANU_TEST_SQLSERVER="Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project tests/ANU_Admissions.SqlServerTests/ANU_Admissions.SqlServerTests.csproj --configuration Release
```

## Change guidelines

- Put deterministic business rules in services or rule classes, not Razor
  views.
- Keep controller actions thin and preserve role authorization and anti-forgery
  protection.
- Add database constraints for invariants that must survive concurrency.
- Add or update tests for every behavior change and bug fix.
- Use EF migrations for schema changes and verify there is no unintended model
  drift.
- Update Arabic and English resources together when adding user-facing text.
- Avoid logging passwords, reset tokens, national IDs, imported student data,
  or full connection strings.
- Keep health endpoints minimal; never return exception or infrastructure
  details from liveness or readiness responses.
- Keep generated `bin`, `obj`, `artifacts`, uploads, and local databases out of
  commits.

## Pull requests

Explain the problem, the chosen behavior, database or security impact, and the
checks you ran. Keep unrelated formatting or refactoring out of the same pull
request so the review remains clear.
