# University Admission System

[![CI](https://github.com/3ab3al11/University-Admission-System/actions/workflows/ci.yml/badge.svg)](https://github.com/3ab3al11/University-Admission-System/actions/workflows/ci.yml)
[![CodeQL](https://github.com/3ab3al11/University-Admission-System/actions/workflows/codeql.yml/badge.svg)](https://github.com/3ab3al11/University-Admission-System/actions/workflows/codeql.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

A secure, role-based university admission platform built with **ASP.NET Core MVC**, **Entity Framework Core**, and **SQL Server**.

The system supports the complete admission workflow: importing official student results, verifying applicant identity, collecting ranked college preferences, and automatically allocating students according to score, capacity, and eligibility rules.

## Core Features

- Official results import from Excel using `SqlBulkCopy`
- Student verification by national ID and seat number
- Ranked college-preference submission
- Automated seat-allocation workflow
- Capacity, minimum-score, section, and eligibility validation
- Separate Admin and Student roles
- ASP.NET Core Identity authentication and authorization
- Account lockout, password policy, global anti-forgery protection, and server-side validation
- Per-request CSP nonces, blocked inline event handlers, security headers, and no-store caching
- Server-generated request IDs plus localized 404 and safe production error pages
- Minimal liveness and SQL Server readiness health endpoints
- Admission-period controls and workflow guards
- Arabic and English localization with RTL/LTR support
- Admin dashboards, reports, audit logs, and nomination results

## Technology Stack

- C# and .NET 8
- ASP.NET Core MVC
- Entity Framework Core
- Microsoft SQL Server
- ASP.NET Core Identity
- ExcelDataReader and `SqlBulkCopy`
- Razor Views
- Bootstrap

## Documentation

- [Architecture and system boundaries](docs/architecture.md)
- [Contribution guide](CONTRIBUTING.md)
- [Security reporting policy](SECURITY.md)

## Testing

Core business rules are isolated from MVC so they can be tested quickly and
deterministically. Unit and in-memory integration tests cover:

- Ranked college preferences
- College capacity and fallback preferences
- Minimum-score requirements
- Inactive and section-ineligible colleges
- Students without preferences
- Score ties and deterministic tie-breaking
- Final college cutoff calculation
- Official-result eligibility status parsing
- Supported official-score formats and invalid values
- Student-section validation and exact seat-number matching
- Admission schedules with optional start/end dates and boundary handling
- Authentication rate-limit partitioning and policy settings
- Trusted password-reset URL validation and query encoding
- Environment-isolated development email logging
- Transactional registration and database-enforced student-phone uniqueness
- Deterministic EF Core seed metadata without timestamp-only migrations
- Serialized, transactional official-record import, reset, and deletion
- SQL application-lock and range-lock rules for official-record maintenance
- XLS/XLSX signatures, workbook structure, size, and archive-safety validation
- Anonymous-user redirects for protected Admin and Student dashboards
- Cross-role access denial between Admin and Student areas
- Successful dashboard access for authenticated users in the correct role
- Anonymous access to public pages
- Security headers on successful and redirect responses
- Unique CSP nonces rendered into approved scripts and styles
- Localized 404 responses and safe error pages with traceable request IDs
- No-store browser-cache protection for authenticated dashboards
- Global anti-forgery rejection for unsafe requests without a valid token
- SQL Server enforcement of the filtered unique student-phone index
- Real `sp_getapplock`, range-lock, transaction, and audit-log behavior
- Healthy and unavailable database-readiness behavior without detail leakage

Run the test suite locally:

```bash
dotnet run --project tests/ANU_Admissions.UnitTests/ANU_Admissions.UnitTests.csproj
```

Generate the same Cobertura coverage report used by CI:

```bash
dotnet tool restore
dotnet build tests/ANU_Admissions.UnitTests/ANU_Admissions.UnitTests.csproj --configuration Release
dotnet coverage collect "dotnet run --project tests/ANU_Admissions.UnitTests/ANU_Admissions.UnitTests.csproj --configuration Release --no-build" --settings coverage.settings.xml --output-format cobertura --output artifacts/coverage/coverage.cobertura.xml
```

GitHub Actions restores, verifies `dotnet format`, builds with the .NET 8
recommended analyzers and warnings treated as errors, and runs all tests on
every push and pull request to `main`. CI uploads a Cobertura report, enforces
30% line and 22% branch coverage baselines over hand-written application code,
and verifies that the EF Core model matches the committed migrations. NuGet
restore audits direct and transitive dependencies and fails when a known
vulnerable package is found. A separate CI job migrates a real SQL Server 2022
database and runs provider-specific integration tests.

CodeQL scans C# changes on pushes, pull requests, and a weekly schedule.
Dependabot checks both NuGet packages and GitHub Actions every week. Committed
NuGet lock files make CI and Docker restores repeatable, while Dependency Review
rejects pull requests that introduce moderate-or-higher known vulnerabilities.

## Project Structure

```text
Controllers/     MVC controllers and application workflows
Data/            EF Core DbContext and database initialization
docs/            Architecture notes and repository screenshots
Helpers/         Shared display helpers
Migrations/      EF Core database migrations
Models/          Domain and Identity entities
Resources/       Arabic and English localization resources
Services/        Admission gates, identity provider, and email abstractions
tests/           Unit, in-memory web, and real SQL Server integration tests
ViewModels/      Request and presentation models
Views/           Razor views for Admin, Student, Account, and shared UI
wwwroot/         Static CSS, JavaScript, images, and client libraries
```

## Main Workflow

1. An administrator imports official student results from an Excel file.
2. A student creates an account and verifies identity using national ID and seat number.
3. The system links the student to the matching official result.
4. The student completes the application and submits ranked college preferences.
5. The administrator runs the allocation process.
6. The system assigns each eligible student to the highest-ranked available college that satisfies the admission rules.
7. The student views the result and prints the nomination card.

## Security Highlights

- Role-based access control for Admin and Student operations
- Server-side validation for scores, eligibility, and admission rules
- Account lockout after repeated failed login attempts
- IP-based rate limiting for login and password-recovery operations
- Password-reset links built from a validated trusted origin, not request headers
- Reset links and tokens can appear in logs only in Development
- Serializable registration with a filtered unique student-phone index
- Database range locks prevent deleting official records linked to students
- Strong password requirements
- Anti-forgery validation on state-changing requests
- Hardened authentication cookies
- Security response headers
- Non-root application container with locally bound web/database ports
- Locked NuGet dependency graphs and pull-request dependency review
- Sensitive admin credentials loaded through .NET User Secrets or environment variables
- Uploaded/generated documents excluded from source control
## Screenshots

<table>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/01-home.webp" alt="Arabic admission portal home page">
      <p align="center"><b>Admission Portal Home</b></p>
    </td>
    <td width="50%">
      <img src="docs/screenshots/02-login.webp" alt="User login page">
      <p align="center"><b>Secure Login</b></p>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/03-admin-dashboard.webp" alt="Administrator dashboard">
      <p align="center"><b>Admin Dashboard</b></p>
    </td>
    <td width="50%">
      <img src="docs/screenshots/04-excel-import.webp" alt="Excel student records import">
      <p align="center"><b>Official Records Import</b></p>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/05-college-management.webp" alt="College management page">
      <p align="center"><b>College Management</b></p>
    </td>
    <td width="50%">
      <img src="docs/screenshots/06-student-preferences.webp" alt="Eligible college preferences">
      <p align="center"><b>Eligibility-Based Preferences</b></p>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/07-allocation-engine.webp" alt="Automated allocation engine">
      <p align="center"><b>Automated Allocation Engine</b></p>
    </td>
    <td width="50%">
      <img src="docs/screenshots/08-allocation-success.webp" alt="Allocation execution summary">
      <p align="center"><b>Allocation Summary</b></p>
    </td>
  </tr>
</table>


## Getting Started

### Requirements

- .NET 8 SDK
- SQL Server LocalDB, SQL Server Express, or SQL Server
- Visual Studio 2022 or VS Code
- Or Docker Desktop with Docker Compose

### 1. Clone the repository

```bash
git clone https://github.com/3ab3al11/University-Admission-System.git
cd University-Admission-System
```

### 2. Configure the database

The default configuration uses SQL Server LocalDB:

```text
Server=(localdb)\MSSQLLocalDB;Database=ANU_Admissions;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

To use another SQL Server instance, update `ConnectionStrings:DefaultConnection` in `appsettings.Development.json`.

### 3. Configure the administrator account

The project does not store an administrator password in source control. Configure it with .NET User Secrets:

```bash
dotnet user-secrets set "AdminSeed:Email" "admin@anu.local"
dotnet user-secrets set "AdminSeed:Password" "ChangeThis123!"
dotnet user-secrets set "AdminSeed:FullName" "System Administrator"
```

Use a different strong password for your local environment.

### 4. Configure the public URL for deployment

Local development is preconfigured for `http://localhost:5023`. For any hosted
environment, provide the real HTTPS origin and its allowed host through secure
environment configuration:

```powershell
$env:ApplicationUrls__PublicBaseUrl="https://admissions.example.edu"
$env:AllowedHosts="admissions.example.edu"
```

The application validates the public URL at startup and requires HTTPS outside
the Development environment. This trusted origin is used for password-reset
links instead of the incoming request Host header.

### 5. Email behavior

In Development, reset emails are written to the local console so the workflow
can be tested without an external provider. That sender both registers and
operates only in the Development environment.

Production and Staging use `DisabledEmailSender`, which discards the message
without logging its recipient, body, reset link, or token. Replace that
registration with a real SMTP/API provider before deploying password recovery.

### 6. Restore and run

```bash
dotnet restore
dotnet run
```

Database migrations are applied automatically when the application starts.

### Docker quick start

Docker runs both the application and SQL Server without storing a password in
the repository. Copy the example environment file, then set a strong local SQL
Server password before starting the containers:

```bash
cp .env.example .env
docker compose up --build
```

Open `http://localhost:8080`. Both published ports bind to `127.0.0.1` by
default, the web process runs as the non-root .NET `app` user, and database and
Data Protection keys are stored in named volumes.

Stop the containers without deleting their data:

```bash
docker compose down
```

Use `docker compose down --volumes` only when you intentionally want to delete
the local database and persisted login-protection keys.

### Operational health endpoints

| Endpoint | Purpose | Result |
|---|---|---|
| `/health/live` | Confirms the web process can serve requests | `200 Healthy` |
| `/health/ready` | Confirms the application can connect to SQL Server | `200 Healthy` or `503 Unhealthy` |

Health responses contain only a status value, are never cached, and do not
expose connection strings, exception messages, or database details.

Every response also includes a server-generated `X-Request-ID`. Production
error pages show this value without exposing exception details, and unknown
routes return a localized page while preserving HTTP 404.

## Excel Import Format

The first worksheet must contain a header row followed by student records in this column order:

| Column | Value |
|---|---|
| 1 | Seat number |
| 2 | Full name |
| 3 | Total score |
| 4 | Student case code |
| 5 | Student case description |
| 6 | Additional flag |

The import accepts validated `.xlsx` and `.xls` files up to 100 MB. The server
checks the real file signature; XLSX uploads must contain the required workbook
structure and stay within safe archive expansion limits. The administrator also
enters the maximum possible score so the system can calculate percentages.

## Privacy Note

The repository contains anonymized mock identity records for development purposes. Real student documents, generated files, local databases, passwords, and user uploads are intentionally excluded from source control.

## Author

**Ahmed Mohamed Abdel Aal**  
Junior Backend .NET Developer

- [LinkedIn](https://www.linkedin.com/in/ahmed-mohamed-web-dev/)
- [GitHub](https://github.com/3ab3al11)
