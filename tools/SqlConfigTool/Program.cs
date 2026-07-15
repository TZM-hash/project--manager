using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;

namespace SqlConfigTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            var projectRoot = FindProjectRoot();
            Application.Run(new SqlConfigForm(projectRoot));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "SQL 配置工具錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string FindProjectRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                var configPath = Path.Combine(directory.FullName, "src", "ProjectManager.Web", "appsettings.json");
                if (File.Exists(configPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException(
            "找不到 Project Manager 專案。請把「SQL配置工具.exe」放在專案根目錄後再執行。");
    }
}

internal sealed class SqlConfigForm : Form
{
    private const string DefaultBaseUrl = "http://127.0.0.1:62383";
    private readonly string _projectRoot;
    private readonly string _webProjectPath;
    private readonly string _defaultConfigPath;
    private readonly ComboBox _serverBox = new();
    private readonly TextBox _databaseBox = new();
    private readonly ComboBox _authenticationBox = new();
    private readonly TextBox _userNameBox = new();
    private readonly TextBox _passwordBox = new();
    private readonly CheckBox _showPasswordBox = new();
    private readonly CheckBox _encryptBox = new();
    private readonly CheckBox _trustCertificateBox = new();
    private readonly Label _statusLabel = new();
    private readonly List<Button> _operationButtons = [];

    public SqlConfigForm(string projectRoot)
    {
        _projectRoot = projectRoot;
        _webProjectPath = Path.Combine(projectRoot, "src", "ProjectManager.Web", "ProjectManager.Web.csproj");
        _defaultConfigPath = Path.Combine(projectRoot, "src", "ProjectManager.Web", "appsettings.json");

        Text = "Project Manager - SQL 配置工具";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(860, 690);
        MinimumSize = new Size(780, 660);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft JhengHei UI", 9F);
        BackColor = Color.FromArgb(247, 249, 252);

        BuildInterface();
        LoadCurrentSettings();
        UpdateAuthenticationControls();
    }

    private void BuildInterface()
    {
        var title = new Label
        {
            Text = "SQL 資料庫與系統啟動設定",
            AutoSize = true,
            Font = new Font("Microsoft JhengHei UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(25, 32, 44),
            Location = new Point(32, 24)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "可儲存連線、測試 SQL、套用資料庫更新、檢查健康狀態並以 Release 模式啟動系統。",
            AutoSize = true,
            ForeColor = Color.FromArgb(90, 99, 115),
            Location = new Point(35, 68)
        };
        Controls.Add(subtitle);

        var card = new Panel
        {
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(32, 102),
            Size = new Size(796, 354),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(card);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 12),
            ColumnCount = 2,
            RowCount = 7
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < 6; row++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        }
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        card.Controls.Add(table);

        _serverBox.DropDownStyle = ComboBoxStyle.DropDown;
        _serverBox.Items.AddRange([".\\SQLEXPRESS", "localhost\\SQLEXPRESS", "(localdb)\\MSSQLLocalDB", "localhost"]);
        AddRow(table, 0, "SQL Server 位址", _serverBox);

        AddRow(table, 1, "資料庫名稱", _databaseBox);

        _authenticationBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _authenticationBox.Items.AddRange(["Windows 身分驗證（建議）", "SQL Server 帳號密碼"]);
        _authenticationBox.SelectedIndexChanged += (_, _) => UpdateAuthenticationControls();
        AddRow(table, 2, "登入方式", _authenticationBox);

        AddRow(table, 3, "SQL 登入帳號", _userNameBox);

        _passwordBox.UseSystemPasswordChar = true;
        _showPasswordBox.Text = "顯示";
        _showPasswordBox.AutoSize = true;
        _showPasswordBox.CheckedChanged += (_, _) => _passwordBox.UseSystemPasswordChar = !_showPasswordBox.Checked;
        var passwordPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        passwordPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        passwordPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
        passwordPanel.Controls.Add(_passwordBox, 0, 0);
        passwordPanel.Controls.Add(_showPasswordBox, 1, 0);
        AddRow(table, 4, "SQL 登入密碼", passwordPanel);

        var optionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _encryptBox.Text = "加密連線";
        _encryptBox.AutoSize = true;
        _trustCertificateBox.Text = "信任伺服器憑證（內網／本機常用）";
        _trustCertificateBox.AutoSize = true;
        optionPanel.Controls.Add(_encryptBox);
        optionPanel.Controls.Add(_trustCertificateBox);
        AddRow(table, 5, "連線選項", optionPanel);

        var pathLabel = new Label
        {
            Text = $"儲存位置：{_defaultConfigPath}",
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(90, 99, 115),
            TextAlign = ContentAlignment.MiddleLeft
        };
        table.Controls.Add(pathLabel, 0, 6);
        table.SetColumnSpan(pathLabel, 2);

        _statusLabel.Text = "建議先測試連線，再儲存設定或執行資料庫更新。";
        _statusLabel.AutoEllipsis = true;
        _statusLabel.ForeColor = Color.FromArgb(90, 99, 115);
        _statusLabel.Location = new Point(35, 475);
        _statusLabel.Size = new Size(790, 44);
        _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_statusLabel);

        var operations = new FlowLayoutPanel
        {
            Location = new Point(32, 526),
            Size = new Size(796, 46),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        Controls.Add(operations);

        operations.Controls.Add(CreateOperationButton("測試連線", async button =>
            await RunOperationAsync(button, "正在測試 SQL 連線…", TestConnectionAsync)));
        operations.Controls.Add(CreateOperationButton("套用資料庫更新", async button =>
            await RunOperationAsync(button, "正在套用 EF Migration…", ApplyMigrationsAsync)));
        operations.Controls.Add(CreateOperationButton("檢查系統健康", async button =>
            await RunOperationAsync(button, "正在檢查健康端點與 App_Data 權限…", CheckHealthAsync)));
        operations.Controls.Add(CreateOperationButton("啟動系統", async button =>
            await RunOperationAsync(button, "正在以 Release 模式啟動系統…", LaunchProjectAsync)));

        var saveButton = new Button
        {
            Text = "儲存設定",
            Size = new Size(130, 40),
            Location = new Point(566, 612),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.FromArgb(31, 111, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += (_, _) => SaveSettings();
        Controls.Add(saveButton);

        var closeButton = new Button
        {
            Text = "關閉",
            Size = new Size(130, 40),
            Location = new Point(698, 612),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        closeButton.Click += (_, _) => Close();
        Controls.Add(closeButton);

        AcceptButton = saveButton;
        CancelButton = closeButton;
    }

    private Button CreateOperationButton(string text, Func<Button, Task> action)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(185, 38),
            Margin = new Padding(0, 0, 12, 0)
        };
        button.Click += async (_, _) => await action(button);
        _operationButtons.Add(button);
        return button;
    }

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control control)
    {
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 8, 4)
        };
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 4, 0, 7);
        table.Controls.Add(label, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void LoadCurrentSettings()
    {
        var connectionString = ReadConnectionString(_defaultConfigPath) ?? string.Empty;
        var values = ParseConnectionString(connectionString);

        _serverBox.Text = GetValue(values, "Server", "Data Source") ?? ".\\SQLEXPRESS";
        _databaseBox.Text = GetValue(values, "Database", "Initial Catalog") ?? "ProjectManager";
        var integratedSecurity = GetBoolean(values, "Trusted_Connection", "Integrated Security");
        _authenticationBox.SelectedIndex = integratedSecurity ? 0 : 1;
        _userNameBox.Text = GetValue(values, "User ID", "UID") ?? string.Empty;
        _passwordBox.Text = GetValue(values, "Password", "PWD") ?? string.Empty;
        _encryptBox.Checked = GetBoolean(values, "Encrypt", defaultValue: true);
        _trustCertificateBox.Checked = GetBoolean(values, "TrustServerCertificate", defaultValue: true);
    }

    private string BuildConnectionString()
    {
        var server = _serverBox.Text.Trim();
        var database = _databaseBox.Text.Trim();
        var sqlAuthentication = _authenticationBox.SelectedIndex == 1;

        if (server.Length == 0)
        {
            throw new InvalidOperationException("請填寫 SQL Server 位址。");
        }
        if (database.Length == 0)
        {
            throw new InvalidOperationException("請填寫資料庫名稱。");
        }
        if (sqlAuthentication && _userNameBox.Text.Trim().Length == 0)
        {
            throw new InvalidOperationException("使用 SQL Server 帳號密碼時，請填寫登入帳號。");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            Encrypt = _encryptBox.Checked,
            TrustServerCertificate = _trustCertificateBox.Checked,
            MultipleActiveResultSets = true,
            ConnectTimeout = 5,
            IntegratedSecurity = !sqlAuthentication
        };

        if (sqlAuthentication)
        {
            builder.UserID = _userNameBox.Text.Trim();
            builder.Password = _passwordBox.Text;
            builder.PersistSecurityInfo = false;
        }

        return builder.ConnectionString;
    }

    private string SaveSettings(bool showDialog = true)
    {
        try
        {
            var connectionString = BuildConnectionString();
            WriteConnectionString(_defaultConfigPath, connectionString);
            _statusLabel.ForeColor = Color.ForestGreen;
            _statusLabel.Text = "設定已儲存，可繼續測試、更新資料庫或啟動系統。";
            if (showDialog)
            {
                MessageBox.Show(
                    $"設定已儲存到：\n{_defaultConfigPath}\n\n原設定已備份為 appsettings.json.bak。",
                    "儲存成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return connectionString;
        }
        catch (Exception exception)
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = $"儲存失敗：{exception.Message}";
            if (showDialog)
            {
                MessageBox.Show(exception.Message, "儲存失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            throw;
        }
    }

    private async Task<string> TestConnectionAsync()
    {
        await using var connection = new SqlConnection(BuildConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME()";
        var databaseName = Convert.ToString(await command.ExecuteScalarAsync()) ?? _databaseBox.Text.Trim();
        return $"SQL 連線成功，目前資料庫：{databaseName}";
    }

    private async Task<string> ApplyMigrationsAsync()
    {
        SaveSettings(showDialog: false);
        var result = await RunDotNetAsync(
            $"ef database update --project {Quote(_webProjectPath)} --startup-project {Quote(_webProjectPath)} --configuration Release --no-build");
        return string.IsNullOrWhiteSpace(result)
            ? "資料庫更新完成。"
            : $"資料庫更新完成：{LastMeaningfulLine(result)}";
    }

    private async Task<string> CheckHealthAsync()
    {
        var live = await ReadHealthAsync($"{DefaultBaseUrl}/health/live");
        var ready = await ReadHealthAsync($"{DefaultBaseUrl}/health/ready");
        VerifyAppDataWritable();
        return $"系統健康：Live {live}、Ready {ready}；App_Data 可寫入。";
    }

    private async Task<string> LaunchProjectAsync()
    {
        try
        {
            await ReadHealthAsync($"{DefaultBaseUrl}/health/live");
            return $"系統已在 {DefaultBaseUrl} 執行。";
        }
        catch
        {
            // 尚未啟動時繼續建立背景程序。
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            Arguments = $"run --project {Quote(_webProjectPath)} -c Release --no-build --launch-profile http",
            WorkingDirectory = _projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 dotnet 程序。");

        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException($"系統啟動失敗，dotnet 已結束（代碼 {process.ExitCode}）。");
            }

            try
            {
                await ReadHealthAsync($"{DefaultBaseUrl}/health/live");
                return $"系統已啟動：{DefaultBaseUrl}";
            }
            catch
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException($"系統已送出啟動指令，但 30 秒內無法連線到 {DefaultBaseUrl}/health/live。");
    }

    private async Task RunOperationAsync(Button source, string progressText, Func<Task<string>> operation)
    {
        try
        {
            UseWaitCursor = true;
            SetOperationButtonsEnabled(false);
            _statusLabel.ForeColor = Color.FromArgb(90, 99, 115);
            _statusLabel.Text = progressText;
            var message = await operation();
            _statusLabel.ForeColor = Color.ForestGreen;
            _statusLabel.Text = message;
            MessageBox.Show(message, source.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = $"{source.Text}失敗：{exception.Message}";
            MessageBox.Show(exception.Message, $"{source.Text}失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetOperationButtonsEnabled(true);
            UseWaitCursor = false;
        }
    }

    private void SetOperationButtonsEnabled(bool enabled)
    {
        foreach (var button in _operationButtons)
        {
            button.Enabled = enabled;
        }
    }

    private void UpdateAuthenticationControls()
    {
        var enabled = _authenticationBox.SelectedIndex == 1;
        _userNameBox.Enabled = enabled;
        _passwordBox.Enabled = enabled;
        _showPasswordBox.Enabled = enabled;
    }

    private async Task<string> RunDotNetAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            Arguments = arguments,
            WorkingDirectory = _projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法執行 dotnet。");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(LastMeaningfulLine(error + Environment.NewLine + output));
        }
        return output.Trim();
    }

    private static async Task<string> ReadHealthAsync(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await client.GetAsync(url);
        var body = (await response.Content.ReadAsStringAsync()).Trim();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"{url} 回傳 {(int)response.StatusCode} {body}");
        }
        return string.IsNullOrWhiteSpace(body) ? "Healthy" : body;
    }

    private void VerifyAppDataWritable()
    {
        var appData = Path.Combine(_projectRoot, "src", "ProjectManager.Web", "App_Data");
        Directory.CreateDirectory(appData);
        var probe = Path.Combine(appData, $".sql-tool-write-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probe, "ok", new UTF8Encoding(false));
        File.Delete(probe);
    }

    private static string LastMeaningfulLine(string value)
    {
        return value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? "未知錯誤";
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static string? ReadConnectionString(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var root = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) as JsonObject;
        return root?["ConnectionStrings"]?["DefaultConnection"]?.GetValue<string>();
    }

    private static void WriteConnectionString(string path, string connectionString)
    {
        JsonObject root;
        if (File.Exists(path))
        {
            root = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) as JsonObject ?? new JsonObject();
            File.Copy(path, path + ".bak", true);
        }
        else
        {
            root = new JsonObject();
        }

        if (root["ConnectionStrings"] is not JsonObject connectionStrings)
        {
            connectionStrings = new JsonObject();
            root["ConnectionStrings"] = connectionStrings;
        }

        connectionStrings["DefaultConnection"] = connectionString;
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
    }

    private static Dictionary<string, string> ParseConnectionString(string connectionString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return result;
        }

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            foreach (string key in builder.Keys)
            {
                result[key] = Convert.ToString(builder[key]) ?? string.Empty;
            }
        }
        catch
        {
            // 損壞的舊設定不會阻止使用者重新填寫。
        }

        return result;
    }

    private static string? GetValue(Dictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool GetBoolean(
        Dictionary<string, string> values,
        string key,
        string? alternateKey = null,
        bool defaultValue = false)
    {
        var rawValue = GetValue(values, alternateKey is null ? [key] : [key, alternateKey]);
        return rawValue is null ? defaultValue : bool.TryParse(rawValue, out var value) && value;
    }
}
