using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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
            MessageBox.Show(exception.Message, "SQL 配置工具错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            "找不到 Project Manager 项目。请把“SQL配置工具.exe”放在项目根目录后再双击运行。");
    }
}

internal sealed class SqlConfigForm : Form
{
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

    public SqlConfigForm(string projectRoot)
    {
        _defaultConfigPath = Path.Combine(projectRoot, "src", "ProjectManager.Web", "appsettings.json");

        Text = "Project Manager - SQL 配置工具";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(720, 590);
        MinimumSize = new Size(736, 629);
        MaximumSize = new Size(900, 700);
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = Color.FromArgb(247, 249, 252);

        BuildInterface();
        LoadCurrentSettings();
        UpdateAuthenticationControls();
    }

    private void BuildInterface()
    {
        var title = new Label
        {
            Text = "SQL 数据库配置",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(25, 32, 44),
            Location = new Point(32, 24)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "双击工具即可配置；项目换电脑或换目录后，工具仍会自动找到正确位置。",
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
            Size = new Size(656, 354),
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
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < 6; row++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        }
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        card.Controls.Add(table);

        _serverBox.DropDownStyle = ComboBoxStyle.DropDown;
        _serverBox.Items.AddRange([".\\SQLEXPRESS", "localhost\\SQLEXPRESS", "(localdb)\\MSSQLLocalDB", "localhost"]);
        AddRow(table, 0, "SQL Server 地址", _serverBox);

        AddRow(table, 1, "数据库名称", _databaseBox);

        _authenticationBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _authenticationBox.Items.AddRange(["Windows 身份验证（推荐）", "SQL Server 账号密码"]);
        _authenticationBox.SelectedIndexChanged += (_, _) => UpdateAuthenticationControls();
        AddRow(table, 2, "登录方式", _authenticationBox);

        AddRow(table, 3, "SQL 登录账号", _userNameBox);

        _passwordBox.UseSystemPasswordChar = true;
        _showPasswordBox.Text = "显示";
        _showPasswordBox.AutoSize = true;
        _showPasswordBox.CheckedChanged += (_, _) => _passwordBox.UseSystemPasswordChar = !_showPasswordBox.Checked;
        var passwordPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        passwordPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        passwordPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
        passwordPanel.Controls.Add(_passwordBox, 0, 0);
        passwordPanel.Controls.Add(_showPasswordBox, 1, 0);
        AddRow(table, 4, "SQL 登录密码", passwordPanel);

        var optionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _encryptBox.Text = "加密连接";
        _encryptBox.AutoSize = true;
        _trustCertificateBox.Text = "信任服务器证书（本机开发常用）";
        _trustCertificateBox.AutoSize = true;
        optionPanel.Controls.Add(_encryptBox);
        optionPanel.Controls.Add(_trustCertificateBox);
        AddRow(table, 5, "连接选项", optionPanel);

        var pathLabel = new Label
        {
            Text = $"保存位置：{_defaultConfigPath}",
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(90, 99, 115),
            TextAlign = ContentAlignment.MiddleLeft
        };
        table.Controls.Add(pathLabel, 0, 6);
        table.SetColumnSpan(pathLabel, 2);

        _statusLabel.Text = "填写完成后点击“保存配置”。";
        _statusLabel.AutoEllipsis = true;
        _statusLabel.ForeColor = Color.FromArgb(90, 99, 115);
        _statusLabel.Location = new Point(35, 475);
        _statusLabel.Size = new Size(650, 42);
        _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_statusLabel);

        var saveButton = new Button
        {
            Text = "保存配置",
            Size = new Size(120, 40),
            Location = new Point(436, 524),
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
            Text = "关闭",
            Size = new Size(120, 40),
            Location = new Point(568, 524),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        closeButton.Click += (_, _) => Close();
        Controls.Add(closeButton);

        AcceptButton = saveButton;
        CancelButton = closeButton;
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

    private void SaveSettings()
    {
        try
        {
            var server = _serverBox.Text.Trim();
            var database = _databaseBox.Text.Trim();
            var sqlAuthentication = _authenticationBox.SelectedIndex == 1;

            if (server.Length == 0)
            {
                throw new InvalidOperationException("请填写 SQL Server 地址。");
            }
            if (database.Length == 0)
            {
                throw new InvalidOperationException("请填写数据库名称。");
            }
            if (sqlAuthentication && _userNameBox.Text.Trim().Length == 0)
            {
                throw new InvalidOperationException("使用 SQL Server 账号密码时，请填写登录账号。");
            }

            var builder = new DbConnectionStringBuilder
            {
                ["Server"] = server,
                ["Database"] = database,
                ["Encrypt"] = _encryptBox.Checked,
                ["TrustServerCertificate"] = _trustCertificateBox.Checked,
                ["MultipleActiveResultSets"] = true,
                ["Connect Timeout"] = 5
            };

            if (sqlAuthentication)
            {
                builder["User ID"] = _userNameBox.Text.Trim();
                builder["Password"] = _passwordBox.Text;
                builder["Persist Security Info"] = false;
            }
            else
            {
                builder["Trusted_Connection"] = true;
            }

            WriteConnectionString(_defaultConfigPath, builder.ConnectionString);
            _statusLabel.ForeColor = Color.ForestGreen;
            _statusLabel.Text = "配置已保存。现在可以关闭工具并启动项目。";
            MessageBox.Show(
                $"配置已保存到：\n{_defaultConfigPath}\n\n原配置已备份为 appsettings.json.bak。",
                "保存成功",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = $"保存失败：{exception.Message}";
            MessageBox.Show(exception.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateAuthenticationControls()
    {
        var enabled = _authenticationBox.SelectedIndex == 1;
        _userNameBox.Enabled = enabled;
        _passwordBox.Enabled = enabled;
        _showPasswordBox.Enabled = enabled;
    }

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
            // 损坏的旧配置不会阻止用户重新填写。
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
