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

项目复制到新电脑或 SQL Server 实例变化后，直接双击项目根目录的：

`SQL配置工具.exe`

工具会自动定位当前项目，无需修改任何绝对路径。配置保存到原来的
`src/ProjectManager.Web/appsettings.json`，并自动生成 `appsettings.json.bak` 备份。
你也可以继续直接打开 `appsettings.json`，按原来的方式手动修改连接字符串。

如需使用脚本备用方式，可运行：

`pwsh -ExecutionPolicy Bypass -File ".\SQL配置工具.ps1"`

## Default Admin

Username: `admin`

Password: `ChangeMe123!`

Change the password after first login.
