# Project Manager

Internal ASP.NET Core Razor Pages project management system.

## Run Locally

1. Download and install the project-local SDK:
   `Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile .dotnet-install.ps1`
   `powershell -ExecutionPolicy Bypass -File .\.dotnet-install.ps1 -Channel 10.0 -InstallDir .\.dotnet`
2. Build:
   `.\scripts\dotnet.ps1 build ProjectManager.sln`
3. Update the SQL Server connection string in `src/ProjectManager.Web/appsettings.json` if needed.
   The default local instance is `localhost\SQLEXPRESS`.
4. Apply migrations:
   `.\scripts\dotnet.ps1 ef database update --project src/ProjectManager.Web/ProjectManager.Web.csproj`
5. Run:
   `.\scripts\dotnet.ps1 run --project src/ProjectManager.Web/ProjectManager.Web.csproj`

## Default Admin

Username: `admin`

Password: `ChangeMe123!`

Change the password after first login.
