param(
    [switch]$Headless,
    [string]$Server,
    [string]$Database,
    [ValidateSet('Windows', 'Sql')]
    [string]$Authentication = 'Windows',
    [string]$UserName,
    [string]$Password,
    [switch]$TestConnection,
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
        throw "找不到项目配置文件：$defaultConfig。请把本工具放在项目根目录后再运行。"
    }

    [pscustomobject]@{
        ProjectRoot       = $projectRoot
        WebRoot           = $webRoot
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
        # 配置损坏时仍允许用户通过界面重新填写。
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
        throw 'SQL Server 地址不能为空。'
    }
    if ([string]::IsNullOrWhiteSpace($InitialCatalog)) {
        throw '数据库名称不能为空。'
    }
    if ($AuthMode -eq 'Sql' -and [string]::IsNullOrWhiteSpace($SqlUserName)) {
        throw '使用 SQL Server 认证时，登录账号不能为空。'
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
        return "连接成功，当前数据库：$databaseName"
    }
    finally {
        $connection.Dispose()
    }
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
    $form.ClientSize = [System.Drawing.Size]::new(690, 570)
    $form.MinimumSize = [System.Drawing.Size]::new(706, 609)
    $form.Font = [System.Drawing.Font]::new('Microsoft YaHei UI', 9)
    $form.MaximizeBox = $false

    $title = [System.Windows.Forms.Label]::new()
    $title.Text = 'SQL 数据库配置'
    $title.Font = [System.Drawing.Font]::new('Microsoft YaHei UI', 18, [System.Drawing.FontStyle]::Bold)
    $title.AutoSize = $true
    $title.Location = [System.Drawing.Point]::new(28, 22)
    $form.Controls.Add($title)

    $subtitle = [System.Windows.Forms.Label]::new()
    $subtitle.Text = '配置会保存到原 appsettings.json，项目换目录后工具仍会自动找到正确位置。'
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

    Add-FieldLabel -Text 'SQL Server 地址' -Y 24
    $serverBox = [System.Windows.Forms.ComboBox]::new()
    $serverBox.Location = [System.Drawing.Point]::new(158, 20)
    $serverBox.Size = [System.Drawing.Size]::new(444, 28)
    $serverBox.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDown
    foreach ($item in Get-SqlServerSuggestions) {
        [void]$serverBox.Items.Add($item)
    }
    $serverBox.Text = $InitialSettings.Server
    $panel.Controls.Add($serverBox)

    Add-FieldLabel -Text '数据库名称' -Y 70
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
    [void]$authBox.Items.Add('SQL Server 账号密码')
    $authBox.SelectedIndex = if ($InitialSettings.Authentication -eq 'Sql') { 1 } else { 0 }
    $panel.Controls.Add($authBox)

    Add-FieldLabel -Text 'SQL 登录账号' -Y 162
    $userBox = [System.Windows.Forms.TextBox]::new()
    $userBox.Location = [System.Drawing.Point]::new(158, 158)
    $userBox.Size = [System.Drawing.Size]::new(444, 27)
    $userBox.Text = $InitialSettings.UserName
    $panel.Controls.Add($userBox)

    Add-FieldLabel -Text 'SQL 登录密码' -Y 208
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
    $encryptBox.Text = '加密连接'
    $encryptBox.AutoSize = $true
    $encryptBox.Checked = [bool]$InitialSettings.Encrypt
    $encryptBox.Location = [System.Drawing.Point]::new(158, 250)
    $panel.Controls.Add($encryptBox)

    $trustBox = [System.Windows.Forms.CheckBox]::new()
    $trustBox.Text = '信任服务器证书（本机开发常用）'
    $trustBox.AutoSize = $true
    $trustBox.Checked = [bool]$InitialSettings.TrustServerCertificate
    $trustBox.Location = [System.Drawing.Point]::new(280, 250)
    $panel.Controls.Add($trustBox)

    $pathLabel = [System.Windows.Forms.Label]::new()
    $pathLabel.Text = "保存位置：$($Paths.DevelopmentConfig)"
    $pathLabel.ForeColor = [System.Drawing.Color]::DimGray
    $pathLabel.AutoEllipsis = $true
    $pathLabel.Location = [System.Drawing.Point]::new(24, 289)
    $pathLabel.Size = [System.Drawing.Size]::new(578, 22)
    $panel.Controls.Add($pathLabel)

    $statusLabel = [System.Windows.Forms.Label]::new()
    $statusLabel.Text = '请填写配置后先测试连接。'
    $statusLabel.AutoEllipsis = $true
    $statusLabel.Location = [System.Drawing.Point]::new(31, 440)
    $statusLabel.Size = [System.Drawing.Size]::new(630, 42)
    $form.Controls.Add($statusLabel)

    $testButton = [System.Windows.Forms.Button]::new()
    $testButton.Text = '测试连接'
    $testButton.Location = [System.Drawing.Point]::new(292, 500)
    $testButton.Size = [System.Drawing.Size]::new(112, 38)
    $form.Controls.Add($testButton)

    $saveButton = [System.Windows.Forms.Button]::new()
    $saveButton.Text = '保存配置'
    $saveButton.Location = [System.Drawing.Point]::new(416, 500)
    $saveButton.Size = [System.Drawing.Size]::new(112, 38)
    $saveButton.BackColor = [System.Drawing.Color]::FromArgb(31, 111, 235)
    $saveButton.ForeColor = [System.Drawing.Color]::White
    $saveButton.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $form.Controls.Add($saveButton)

    $cancelButton = [System.Windows.Forms.Button]::new()
    $cancelButton.Text = '关闭'
    $cancelButton.Location = [System.Drawing.Point]::new(540, 500)
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
            $statusLabel.Text = '正在连接 SQL Server，请稍候……'
            [System.Windows.Forms.Application]::DoEvents()
            $connectionString = Get-FormConnectionString
            $message = Test-SqlConnectionString -ConnectionString $connectionString
            $statusLabel.ForeColor = [System.Drawing.Color]::ForestGreen
            $statusLabel.Text = $message
            [System.Windows.Forms.MessageBox]::Show($message, '连接成功', 'OK', 'Information') | Out-Null
        }
        catch {
            $statusLabel.ForeColor = [System.Drawing.Color]::Firebrick
            $statusLabel.Text = "连接失败：$($_.Exception.Message)"
            [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '连接失败', 'OK', 'Error') | Out-Null
        }
        finally {
            $testButton.Enabled = $true
            $form.UseWaitCursor = $false
        }
    })

    $saveButton.Add_Click({
        try {
            $connectionString = Get-FormConnectionString
            Save-DevelopmentConfig -Path $Paths.DevelopmentConfig -ConnectionString $connectionString
            $statusLabel.ForeColor = [System.Drawing.Color]::ForestGreen
            $statusLabel.Text = '配置已保存。下次项目移动后重新运行本工具即可。'
            [System.Windows.Forms.MessageBox]::Show(
                "配置已保存到：`n$($Paths.DevelopmentConfig)`n`n连接字符串：`n$(Get-MaskedConnectionString -ConnectionString $connectionString)",
                '保存成功',
                'OK',
                'Information'
            ) | Out-Null
        }
        catch {
            $statusLabel.ForeColor = [System.Drawing.Color]::Firebrick
            $statusLabel.Text = "保存失败：$($_.Exception.Message)"
            [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '保存失败', 'OK', 'Error') | Out-Null
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
        $connectionString = New-SqlConnectionString `
            -DataSource $Server `
            -InitialCatalog $Database `
            -AuthMode $Authentication `
            -SqlUserName $UserName `
            -SqlPassword $Password

        if ($TestConnection) {
            Write-Output (Test-SqlConnectionString -ConnectionString $connectionString)
        }

        Save-DevelopmentConfig -Path $paths.DevelopmentConfig -ConnectionString $connectionString
        Write-Output "配置已保存：$($paths.DevelopmentConfig)"
        Write-Output (Get-MaskedConnectionString -ConnectionString $connectionString)
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
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'SQL 配置工具错误', 'OK', 'Error') | Out-Null
    }
    catch {
        Write-Error $_
    }
}
