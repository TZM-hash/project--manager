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

## SQL 配置工具

專案複製到新電腦、SQL Server 執行個體變更或版本升級後，直接雙擊專案根目錄的：

`SQL配置工具.exe`

工具會自動定位目前專案，並提供：

- 測試 SQL 連線。
- 儲存 `ConnectionStrings:DefaultConnection`，同時建立 `appsettings.json.bak`。
- 明確按下後套用最新 EF Migration。
- 檢查 `/health/live`、`/health/ready` 與 `App_Data` 寫入權限。
- 以 Release、隱藏視窗方式啟動網站。

「儲存設定」不會自動變更資料庫；只有「套用資料庫更新」會執行 Migration。

如需使用腳本備用方式，可執行：

`pwsh -ExecutionPolicy Bypass -File ".\SQL配置工具.ps1"`

Headless 範例：

`pwsh -ExecutionPolicy Bypass -File ".\SQL配置工具.ps1" -Headless -TestConnection -ApplyMigrations -HealthCheck`

## Default Admin

Username: `admin`

Password: `ChangeMe123!`

Change the password after first login.
