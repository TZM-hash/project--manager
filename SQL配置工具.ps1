param(
    [switch]$Headless,
    [string]$Server,
    [string]$Database,
    [ValidateSet('Windows', 'Sql')]
    [string]$Authentication = 'Windows',
    [string]$UserName,
    [string]$Password,
    [switch]$TestConnection,
    [switch]$ApplyMigrations,
    [switch]$HealthCheck,
    [switch]$Launch,
    [string]$BaseUrl = 'http://127.0.0.1:62383',
    [string]$ConfigPath
)

$ErrorActionPreference = 'Stop'

function Get-ProjectPaths {
    $projectRoot = $PSScriptRoot
    $webRoot = Join-Path $projectRoot 'src\ProjectManager.Web'
    $defaultConfig = Join-Path $webRoot 'appsettings.json'
    $developmentConfig = if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
        $defaultConfig
    }
    else {
        [System.IO.Path]::GetFullPath($ConfigPath)
    }

    if (-not (Test-Path -LiteralPath $defaultConfig -PathType Leaf)) {
        throw "找不到專案設定檔：$defaultConfig。請把本工具放在專案根目錄後再執行。"
    }

    [pscustomobject]@{
        ProjectRoot       = $projectRoot
        WebRoot           = $webRoot
        WebProject        = Join-Path $webRoot 'ProjectManager.Web.csproj'
        DefaultConfig     = $defaultConfig
        DevelopmentConfig = $developmentConfig
    }
}

function Get-JsonConfig {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $null
    }

    $content | ConvertFrom-Json
}

function Get-ConnectionStringFromConfig {
    param([Parameter(Mandatory)]$Paths)

    foreach ($path in @($Paths.DevelopmentConfig, $Paths.DefaultConfig)) {
        $config = Get-JsonConfig -Path $path
        if ($null -eq $config) {
            continue
        }

        $connectionStrings = $config.PSObject.Properties['ConnectionStrings']
        if ($null -eq $connectionStrings) {
            continue
        }

        $defaultConnection = $connectionStrings.Value.PSObject.Properties['DefaultConnection']
        if ($null -ne $defaultConnection -and -not [string]::IsNullOrWhiteSpace([string]$defaultConnection.Value)) {
            return [string]$defaultConnection.Value
        }
    }

    return ''
}

function ConvertFrom-ConnectionString {
    param([string]$ConnectionString)

    $settings = [ordered]@{
        Server                 = '.\SQLEXPRESS'
        Database               = 'ProjectManager'
        Authentication         = 'Windows'
        UserName               = ''
        Password               = ''
        Encrypt                = $true
        TrustServerCertificate = $true
    }

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return [pscustomobject]$settings
    }

    try {
        $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
        if (-not [string]::IsNullOrWhiteSpace([string]$builder['Data Source'])) {
            $settings.Server = [string]$builder['Data Source']
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$builder['Initial Catalog'])) {
            $settings.Database = [string]$builder['Initial Catalog']
        }
        $settings.Authentication = if ([bool]$builder['Integrated Security']) { 'Windows' } else { 'Sql' }
        $settings.UserName = [string]$builder['User ID']
        $settings.Password = [string]$builder['Password']
        $settings.Encrypt = [bool]$builder['Encrypt']
        $settings.TrustServerCertificate = [bool]$builder['TrustServerCertificate']
    }
    catch {
        # 舊設定損壞時仍允許使用者透過介面重新填寫。
    }

    [pscustomobject]$settings
}

function New-SqlConnectionString {
    param(
        [Parameter(Mandatory)][string]$DataSource,
        [Parameter(Mandatory)][string]$InitialCatalog,
        [Parameter(Mandatory)][ValidateSet('Windows', 'Sql')][string]$AuthMode,
        [string]$SqlUserName,
        [string]$SqlPassword,
        [bool]$Encrypt = $true,
        [bool]$TrustServerCertificate = $true
    )

    if ([string]::IsNullOrWhiteSpace($DataSource)) {
        throw 'SQL Server 位址不可空白。'
    }
    if ([string]::IsNullOrWhiteSpace($InitialCatalog)) {
        throw '資料庫名稱不可空白。'
    }
    if ($AuthMode -eq 'Sql' -and [string]::IsNullOrWhiteSpace($SqlUserName)) {
        throw '使用 SQL Server 驗證時，登入帳號不可空白。'
    }

    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = $DataSource.Trim()
    $builder['Initial Catalog'] = $InitialCatalog.Trim()
    $builder['Integrated Security'] = $AuthMode -eq 'Windows'
    $builder['Encrypt'] = $Encrypt
    $builder['TrustServerCertificate'] = $TrustServerCertificate
    $builder['MultipleActiveResultSets'] = $true
    $builder['Connect Timeout'] = 5

    if ($AuthMode -eq 'Sql') {
        $builder['User ID'] = $SqlUserName.Trim()
        $builder['Password'] = $SqlPassword
        $builder['Persist Security Info'] = $false
    }

    $builder.ConnectionString
}

function Get-MaskedConnectionString {
    param([Parameter(Mandatory)][string]$ConnectionString)

    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
    if (-not [bool]$builder['Integrated Security'] -and -not [string]::IsNullOrEmpty([string]$builder['Password'])) {
        $builder['Password'] = '********'
    }
    $builder.ConnectionString
}

function Save-DevelopmentConfig {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ConnectionString
    )

    $config = Get-JsonConfig -Path $Path
    if ($null -eq $config) {
        $config = [pscustomobject]@{}
    }

    if ($null -eq $config.PSObject.Properties['ConnectionStrings']) {
        $config | Add-Member -NotePropertyName 'ConnectionStrings' -NotePropertyValue ([pscustomobject]@{})
    }

    $connectionStrings = $config.PSObject.Properties['ConnectionStrings'].Value
    if ($null -eq $connectionStrings.PSObject.Properties['DefaultConnection']) {
        $connectionStrings | Add-Member -NotePropertyName 'DefaultConnection' -NotePropertyValue $ConnectionString
    }
    else {
        $connectionStrings.DefaultConnection = $ConnectionString
    }

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Copy-Item -LiteralPath $Path -Destination "${Path}.bak" -Force
    }

    $json = $config | ConvertTo-Json -Depth 20
    Set-Content -LiteralPath $Path -Value $json -Encoding UTF8
}

function Test-SqlConnectionString {
    param([Parameter(Mandatory)][string]$ConnectionString)

    $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT DB_NAME()'
        $databaseName = [string]$command.ExecuteScalar()
        return "連線成功，目前資料庫：$databaseName"
    }
    finally {
        $connection.Dispose()
    }
}

function Invoke-ProjectDotNet {
    param(
        [Parameter(Mandatory)]$Paths,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    Push-Location -LiteralPath $Paths.ProjectRoot
    try {
        $output = & dotnet @Arguments 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) {
            $message = ($output -split [Environment]::NewLine |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                Select-Object -Last 1)
            throw "dotnet 執行失敗：$message"
        }

        $output.Trim()
    }
    finally {
        Pop-Location
    }
}

function Invoke-DatabaseMigration {
    param([Parameter(Mandatory)]$Paths)

    $arguments = @(
        'ef', 'database', 'update',
        '--project', $Paths.WebProject,
        '--startup-project', $Paths.WebProject,
        '--configuration', 'Release',
        '--no-build'
    )
    $output = Invoke-ProjectDotNet -Paths $Paths -Arguments $arguments
    if ([string]::IsNullOrWhiteSpace($output)) {
        return '資料庫更新完成。'
    }

    $lastLine = $output -split [Environment]::NewLine |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Last 1
    "資料庫更新完成：$lastLine"
}

function Test-ProjectHealth {
    param(
        [Parameter(Mandatory)]$Paths,
        [Parameter(Mandatory)][string]$Url
    )

    $rootUrl = $Url.TrimEnd('/')
    $live = Invoke-WebRequest -Uri "$rootUrl/health/live" -UseBasicParsing -TimeoutSec 5
    $ready = Invoke-WebRequest -Uri "$rootUrl/health/ready" -UseBasicParsing -TimeoutSec 5

    $appData = Join-Path $Paths.WebRoot 'App_Data'
    if (-not (Test-Path -LiteralPath $appData -PathType Container)) {
        New-Item -ItemType Directory -Path $appData -Force | Out-Null
    }
    $probe = Join-Path $appData ('.sql-tool-write-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        Set-Content -LiteralPath $probe -Value 'ok' -Encoding UTF8
    }
    finally {
        if (Test-Path -LiteralPath $probe -PathType Leaf) {
            Remove-Item -LiteralPath $probe -Force
        }
    }

    "系統健康：Live $($live.Content.Trim())、Ready $($ready.Content.Trim())；App_Data 可寫入。"
}

function Start-ProjectRelease {
    param(
        [Parameter(Mandatory)]$Paths,
        [Parameter(Mandatory)][string]$Url
    )

    try {
        Test-ProjectHealth -Paths $Paths -Url $Url | Out-Null
        return "系統已在 $($Url.TrimEnd('/')) 執行。"
    }
    catch {
        # 尚未啟動時建立背景程序。
    }

    $argumentList = "run --project `"$($Paths.WebProject)`" -c Release --no-build --launch-profile http"
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $argumentList -WorkingDirectory $Paths.ProjectRoot -WindowStyle Hidden -PassThru

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($process.HasExited) {
            throw "系統啟動失敗，dotnet 已結束（代碼 $($process.ExitCode)）。"
        }

        try {
            $message = Test-ProjectHealth -Paths $Paths -Url $Url
            return "系統已啟動：$($Url.TrimEnd('/'))。$message"
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "系統已送出啟動指令，但 30 秒內無法連線到 $($Url.TrimEnd('/'))/health/live。"
}

function Get-SqlServerSuggestions {
    $suggestions = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @('.\SQLEXPRESS', 'localhost\SQLEXPRESS', '(localdb)\MSSQLLocalDB', 'localhost')) {
        if (-not $suggestions.Contains($value)) {
            $suggestions.Add($value)
        }
    }

    try {
        Get-Service -Name 'MSSQL*' -ErrorAction SilentlyContinue | ForEach-Object {
            $serverName = if ($_.Name -eq 'MSSQLSERVER') {
                'localhost'
            }
            elseif ($_.Name -like 'MSSQL$*') {
                '.\' + $_.Name.Substring(6)
            }
            else {
                $null
            }

            if (-not [string]::IsNullOrWhiteSpace($serverName) -and -not $suggestions.Contains($serverName)) {
                $suggestions.Add($serverName)
            }
        }
    }
    catch {
        # 无权读取服务列表时保留常用选项即可。
    }

    $suggestions
}

function Show-SqlConfigWindow {
    param(
        [Parameter(Mandatory)]$Paths,
        [Parameter(Mandatory)]$InitialSettings
    )

    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [System.Windows.Forms.Application]::EnableVisualStyles()

    $form = [System.Windows.Forms.Form]::new()
    $form.Text = 'Project Manager - SQL 配置工具'
    $form.StartPosition = 'CenterScreen'
    $form.ClientSize = [System.Drawing.Size]::new(690, 650)
    $form.MinimumSize = [System.Drawing.Size]::new(706, 689)
    $form.Font = [System.Drawing.Font]::new('Microsoft JhengHei UI', 9)
    $form.MaximizeBox = $false

    $title = [System.Windows.Forms.Label]::new()
    $title.Text = 'SQL 資料庫與系統啟動設定'
    $title.Font = [System.Drawing.Font]::new('Microsoft JhengHei UI', 18, [System.Drawing.FontStyle]::Bold)
    $title.AutoSize = $true
    $title.Location = [System.Drawing.Point]::new(28, 22)
    $form.Controls.Add($title)

    $subtitle = [System.Windows.Forms.Label]::new()
    $subtitle.Text = '可儲存連線、更新資料庫、檢查健康狀態並以 Release 模式啟動系統。'
    $subtitle.ForeColor = [System.Drawing.Color]::DimGray
    $subtitle.AutoSize = $true
    $subtitle.Location = [System.Drawing.Point]::new(31, 65)
    $form.Controls.Add($subtitle)

    $panel = [System.Windows.Forms.Panel]::new()
    $panel.Location = [System.Drawing.Point]::new(28, 96)
    $panel.Size = [System.Drawing.Size]::new(634, 326)
    $panel.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $form.Controls.Add($panel)

    function Add-FieldLabel {
        param([string]$Text, [int]$Y)
        $label = [System.Windows.Forms.Label]::new()
        $label.Text = $Text
        $label.AutoSize = $true
        $label.Location = [System.Drawing.Point]::new(24, $Y + 5)
        $panel.Controls.Add($label)
    }

    Add-FieldLabel -Text 'SQL Server 位址' -Y 24
    $serverBox = [System.Windows.Forms.ComboBox]::new()
    $serverBox.Location = [System.Drawing.Point]::new(158, 20)
    $serverBox.Size = [System.Drawing.Size]::new(444, 28)
    $serverBox.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDown
    foreach ($item in Get-SqlServerSuggestions) {
        [void]$serverBox.Items.Add($item)
    }
    $serverBox.Text = $InitialSettings.Server
    $panel.Controls.Add($serverBox)

    Add-FieldLabel -Text '資料庫名稱' -Y 70
    $databaseBox = [System.Windows.Forms.TextBox]::new()
    $databaseBox.Location = [System.Drawing.Point]::new(158, 66)
    $databaseBox.Size = [System.Drawing.Size]::new(444, 27)
    $databaseBox.Text = $InitialSettings.Database
    $panel.Controls.Add($databaseBox)

    Add-FieldLabel -Text '登录方式' -Y 116
    $authBox = [System.Windows.Forms.ComboBox]::new()
    $authBox.Location = [System.Drawing.Point]::new(158, 112)
    $authBox.Size = [System.Drawing.Size]::new(444, 28)
    $authBox.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
    [void]$authBox.Items.Add('Windows 身份验证（推荐）')
    [void]$authBox.Items.Add('SQL Server 帳號密碼')
    $authBox.SelectedIndex = if ($InitialSettings.Authentication -eq 'Sql') { 1 } else { 0 }
    $panel.Controls.Add($authBox)

    Add-FieldLabel -Text 'SQL 登入帳號' -Y 162
    $userBox = [System.Windows.Forms.TextBox]::new()
    $userBox.Location = [System.Drawing.Point]::new(158, 158)
    $userBox.Size = [System.Drawing.Size]::new(444, 27)
    $userBox.Text = $InitialSettings.UserName
    $panel.Controls.Add($userBox)

    Add-FieldLabel -Text 'SQL 登入密碼' -Y 208
    $passwordBox = [System.Windows.Forms.TextBox]::new()
    $passwordBox.Location = [System.Drawing.Point]::new(158, 204)
    $passwordBox.Size = [System.Drawing.Size]::new(416, 27)
    $passwordBox.Text = $InitialSettings.Password
    $passwordBox.UseSystemPasswordChar = $true
    $panel.Controls.Add($passwordBox)

    $showPasswordBox = [System.Windows.Forms.CheckBox]::new()
    $showPasswordBox.Text = '显示'
    $showPasswordBox.AutoSize = $true
    $showPasswordBox.Location = [System.Drawing.Point]::new(580, 207)
    $panel.Controls.Add($showPasswordBox)

    $encryptBox = [System.Windows.Forms.CheckBox]::new()
    $encryptBox.Text = '加密連線'
    $encryptBox.AutoSize = $true
    $encryptBox.Checked = [bool]$InitialSettings.Encrypt
    $encryptBox.Location = [System.Drawing.Point]::new(158, 250)
    $panel.Controls.Add($encryptBox)

    $trustBox = [System.Windows.Forms.CheckBox]::new()
    $trustBox.Text = '信任伺服器憑證（內網／本機常用）'
    $trustBox.AutoSize = $true
    $trustBox.Checked = [bool]$InitialSettings.TrustServerCertificate
    $trustBox.Location = [System.Drawing.Point]::new(280, 250)
    $panel.Controls.Add($trustBox)

    $pathLabel = [System.Windows.Forms.Label]::new()
    $pathLabel.Text = "儲存位置：$($Paths.DevelopmentConfig)"
    $pathLabel.ForeColor = [System.Drawing.Color]::DimGray
    $pathLabel.AutoEllipsis = $true
    $pathLabel.Location = [System.Drawing.Point]::new(24, 289)
    $pathLabel.Size = [System.Drawing.Size]::new(578, 22)
    $panel.Controls.Add($pathLabel)

    $statusLabel = [System.Windows.Forms.Label]::new()
    $statusLabel.Text = '建議先測試連線，再儲存設定或執行資料庫更新。'
    $statusLabel.AutoEllipsis = $true
    $statusLabel.Location = [System.Drawing.Point]::new(31, 440)
    $statusLabel.Size = [System.Drawing.Size]::new(630, 42)
    $form.Controls.Add($statusLabel)

    $testButton = [System.Windows.Forms.Button]::new()
    $testButton.Text = '測試連線'
    $testButton.Location = [System.Drawing.Point]::new(31, 500)
    $testButton.Size = [System.Drawing.Size]::new(145, 38)
    $form.Controls.Add($testButton)

    $migrationButton = [System.Windows.Forms.Button]::new()
    $migrationButton.Text = '套用資料庫更新'
    $migrationButton.Location = [System.Drawing.Point]::new(186, 500)
    $migrationButton.Size = [System.Drawing.Size]::new(145, 38)
    $form.Controls.Add($migrationButton)

    $healthButton = [System.Windows.Forms.Button]::new()
    $healthButton.Text = '檢查系統健康'
    $healthButton.Location = [System.Drawing.Point]::new(341, 500)
    $healthButton.Size = [System.Drawing.Size]::new(145, 38)
    $form.Controls.Add($healthButton)

    $launchButton = [System.Windows.Forms.Button]::new()
    $launchButton.Text = '啟動系統'
    $launchButton.Location = [System.Drawing.Point]::new(496, 500)
    $launchButton.Size = [System.Drawing.Size]::new(145, 38)
    $form.Controls.Add($launchButton)

    $saveButton = [System.Windows.Forms.Button]::new()
    $saveButton.Text = '儲存設定'
    $saveButton.Location = [System.Drawing.Point]::new(416, 570)
    $saveButton.Size = [System.Drawing.Size]::new(112, 38)
    $saveButton.BackColor = [System.Drawing.Color]::FromArgb(31, 111, 235)
    $saveButton.ForeColor = [System.Drawing.Color]::White
    $saveButton.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $form.Controls.Add($saveButton)

    $cancelButton = [System.Windows.Forms.Button]::new()
    $cancelButton.Text = '關閉'
    $cancelButton.Location = [System.Drawing.Point]::new(540, 570)
    $cancelButton.Size = [System.Drawing.Size]::new(112, 38)
    $form.Controls.Add($cancelButton)

    function Get-FormConnectionString {
        $authMode = if ($authBox.SelectedIndex -eq 1) { 'Sql' } else { 'Windows' }
        New-SqlConnectionString `
            -DataSource $serverBox.Text `
            -InitialCatalog $databaseBox.Text `
            -AuthMode $authMode `
            -SqlUserName $userBox.Text `
            -SqlPassword $passwordBox.Text `
            -Encrypt $encryptBox.Checked `
            -TrustServerCertificate $trustBox.Checked
    }

    function Update-AuthenticationControls {
        $sqlAuthEnabled = $authBox.SelectedIndex -eq 1
        $userBox.Enabled = $sqlAuthEnabled
        $passwordBox.Enabled = $sqlAuthEnabled
        $showPasswordBox.Enabled = $sqlAuthEnabled
    }

    $authBox.Add_SelectedIndexChanged({ Update-AuthenticationControls })
    $showPasswordBox.Add_CheckedChanged({
        $passwordBox.UseSystemPasswordChar = -not $showPasswordBox.Checked
    })

    $testButton.Add_Click({
        try {
            $form.UseWaitCursor = $true
            $testButton.Enabled = $false
            $statusLabel.ForeColor = [System.Drawing.Color]::DimGray
            $statusLabel.Text = '正在連線 SQL Server，請稍候……'
            [System.Windows.Forms.Application]::DoEvents()
            $connectionString = Get-FormConnectionString
            $message = Test-SqlConnectionString -ConnectionString $connectionString
            $statusLabel.ForeColor = [System.Drawing.Color]::ForestGreen
            $statusLabel.Text = $message
            [System.Windows.Forms.MessageBox]::Show($message, '連線成功', 'OK', 'Information') | Out-Null
        }
        catch {
            $statusLabel.ForeColor = [System.Drawing.Color]::Firebrick
            $statusLabel.Text = "連線失敗：$($_.Exception.Message)"
            [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '連線失敗', 'OK', 'Error') | Out-Null
        }
        finally {
            $testButton.Enabled = $true
            $form.UseWaitCursor = $false
        }
    })

    $migrationButton.Add_Click({
        try {
            $form.UseWaitCursor = $true
            $statusLabel.ForeColor = [System.Drawing.Color]::DimGray
            $statusLabel.Text = '正在套用 EF Migration，請稍候……'
            [System.Windows.Forms.Application]::DoEvents()
            $connectionString = Get-FormConnectionString
            Save-DevelopmentConfig -Path $Paths.DevelopmentConfig -ConnectionString $connectionString
            $message = Invoke-DatabaseMigration -Paths $Paths
            $statusLabel.ForeColor = [System.Drawing.Color]::ForestGreen
            $statusLabel.Text = $message
            [System.Windows.Forms.MessageBox]::Show($message, '資料庫更新完成', 'OK', 'Information') | Out-Null
        }
        catch {
            $statusLabel.ForeColor = [System.Drawing.Color]::Firebrick
            $statusLabel.Text = "資料庫更新失敗：$($_.Exception.Message)"
            [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '資料庫更新失敗', 'OK', 'Error') | Out-Null
        }
        finally {
            $form.UseWaitCursor = $false
        }
    })

    $healthButton.Add_Click({
        try {
            $message = Test-ProjectHealth -Paths $Paths -Url $BaseUrl
            $statusLabel.ForeColor = [System.Drawing.Color]::ForestGreen
            $statusLabel.Text = $message
            [System.Windows.Forms.MessageBox]::Show($message, '系統健康', 'OK', 'Information') | Out-Null
        }
        catch {
            $statusLabel.ForeColor = [System.Drawing.Color]::Firebrick
            $statusLabel.Text = "健康檢查失敗：$($_.Exception.Message)"
            [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '健康檢查失敗', 'OK', 'Error') | Out-Null
        }
    })

    $launchButton.Add_Click({
        try {
            $form.UseWaitCursor = $true
            $statusLabel.Text = '正在以 Release 模式啟動系統……'
            [System.Windows.Forms.Application]::DoEvents()
            $message = Start-ProjectRelease -Paths $Paths -Url $BaseUrl
            $statusLabel.ForeColor = [System.Drawing.Color]::ForestGreen
            $statusLabel.Text = $message
            [System.Windows.Forms.MessageBox]::Show($message, '啟動完成', 'OK', 'Information') | Out-Null
        }
        catch {
            $statusLabel.ForeColor = [System.Drawing.Color]::Firebrick
            $statusLabel.Text = "啟動失敗：$($_.Exception.Message)"
            [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '啟動失敗', 'OK', 'Error') | Out-Null
        }
        finally {
            $form.UseWaitCursor = $false
        }
    })

    $saveButton.Add_Click({
        try {
            $connectionString = Get-FormConnectionString
            Save-DevelopmentConfig -Path $Paths.DevelopmentConfig -ConnectionString $connectionString
            $statusLabel.ForeColor = [System.Drawing.Color]::ForestGreen
            $statusLabel.Text = '設定已儲存；專案移動後重新執行本工具即可。'
            [System.Windows.Forms.MessageBox]::Show(
                "設定已儲存到：`n$($Paths.DevelopmentConfig)`n`n連線字串：`n$(Get-MaskedConnectionString -ConnectionString $connectionString)",
                '儲存成功',
                'OK',
                'Information'
            ) | Out-Null
        }
        catch {
            $statusLabel.ForeColor = [System.Drawing.Color]::Firebrick
            $statusLabel.Text = "儲存失敗：$($_.Exception.Message)"
            [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '儲存失敗', 'OK', 'Error') | Out-Null
        }
    })

    $cancelButton.Add_Click({ $form.Close() })
    $form.AcceptButton = $saveButton
    $form.CancelButton = $cancelButton
    Update-AuthenticationControls
    [void]$form.ShowDialog()
}

try {
    Add-Type -AssemblyName System.Data
    $paths = Get-ProjectPaths

    if ($Headless) {
        $connectionString = Get-ConnectionStringFromConfig -Paths $paths
        $hasNewConnection = -not [string]::IsNullOrWhiteSpace($Server) -or -not [string]::IsNullOrWhiteSpace($Database)
        if ($hasNewConnection) {
            $connectionString = New-SqlConnectionString `
                -DataSource $Server `
                -InitialCatalog $Database `
                -AuthMode $Authentication `
                -SqlUserName $UserName `
                -SqlPassword $Password
            Save-DevelopmentConfig -Path $paths.DevelopmentConfig -ConnectionString $connectionString
            Write-Output "設定已儲存：$($paths.DevelopmentConfig)"
            Write-Output (Get-MaskedConnectionString -ConnectionString $connectionString)
        }

        if ($TestConnection) {
            if ([string]::IsNullOrWhiteSpace($connectionString)) {
                throw '找不到可供測試的 DefaultConnection。'
            }
            Write-Output (Test-SqlConnectionString -ConnectionString $connectionString)
        }
        if ($ApplyMigrations) {
            Write-Output (Invoke-DatabaseMigration -Paths $paths)
        }
        if ($Launch) {
            Write-Output (Start-ProjectRelease -Paths $paths -Url $BaseUrl)
        }
        elseif ($HealthCheck) {
            Write-Output (Test-ProjectHealth -Paths $paths -Url $BaseUrl)
        }
        if (-not $hasNewConnection -and -not $TestConnection -and -not $ApplyMigrations -and -not $HealthCheck -and -not $Launch) {
            Write-Output '未指定操作；可使用 -TestConnection、-ApplyMigrations、-HealthCheck 或 -Launch。'
        }
        exit 0
    }

    $currentConnectionString = Get-ConnectionStringFromConfig -Paths $paths
    $initialSettings = ConvertFrom-ConnectionString -ConnectionString $currentConnectionString
    Show-SqlConfigWindow -Paths $paths -InitialSettings $initialSettings
}
catch {
    if ($Headless) {
        Write-Error $_
        exit 1
    }

    try {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'SQL 配置工具錯誤', 'OK', 'Error') | Out-Null
    }
    catch {
        Write-Error $_
    }
}
