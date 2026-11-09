using System.Drawing;
using System.Windows.Forms;

namespace LanArena;

/// <summary>Settings dialog for configuring cloud discovery and other options.</summary>
public class SettingsDialog : Form
{
    private AppSettings settings;
    private CheckBox chkCloudDiscovery, chkLanDiscovery;
    private TextBox txtServerUrl;
    private NumericUpDown numPollingInterval;
    private Button btnSave, btnCancel, btnReset;

    public SettingsDialog(AppSettings settings)
    {
        this.settings = settings;
        BuildUi();
        LoadSettings();
    }

    void BuildUi()
    {
        Text = "LAN Arena Settings";
        ClientSize = new Size(500, 350);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Ui.Bg;
        ForeColor = Ui.Txt;
        Font = Ui.F(10);

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16),
            Margin = new Padding(0)
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // Discovery options
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Server settings
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // Polling interval
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Info
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // Buttons

        // --- Discovery Options
        var discoveryPanel = new GroupBox
        {
            Text = "Discovery Methods",
            Dock = DockStyle.Fill,
            BackColor = Ui.Card,
            ForeColor = Ui.Txt,
            Font = Ui.F(11, FontStyle.Bold)
        };

        var discoveryLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Padding = new Padding(12)
        };

        chkLanDiscovery = new CheckBox
        {
            Text = "Local LAN Discovery (UDP Broadcast)",
            Checked = true,
            AutoSize = true,
            Font = Ui.F(10),
            ForeColor = Ui.Txt
        };

        chkCloudDiscovery = new CheckBox
        {
            Text = "Cloud-Based Discovery (Internet)",
            Checked = true,
            AutoSize = true,
            Font = Ui.F(10),
            ForeColor = Ui.Txt
        };

        discoveryLayout.Controls.Add(chkLanDiscovery);
        discoveryLayout.Controls.Add(chkCloudDiscovery);
        discoveryPanel.Controls.Add(discoveryLayout);
        main.Controls.Add(discoveryPanel, 0, 0);

        // --- Server Configuration
        var serverPanel = new GroupBox
        {
            Text = "Cloud Server Settings",
            Dock = DockStyle.Fill,
            BackColor = Ui.Card,
            ForeColor = Ui.Txt,
            Font = Ui.F(11, FontStyle.Bold),
            Padding = new Padding(12)
        };

        var serverLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true
        };
        serverLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        serverLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lblServer = new Label
        {
            Text = "Server URL:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Ui.F(10)
        };
        txtServerUrl = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Ui.Card2,
            ForeColor = Ui.Txt,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Ui.F(10),
            Margin = new Padding(0, 0, 0, 6)
        };

        serverLayout.Controls.Add(lblServer, 0, 0);
        serverLayout.Controls.Add(txtServerUrl, 1, 0);

        var lblHelp = new Label
        {
            Text = "Leave empty to use default: " + ServerDiscovery.DefaultServerUrl,
            Dock = DockStyle.Fill,
            ForeColor = Ui.Muted,
            Font = Ui.F(9, FontStyle.Italic),
            AutoSize = true
        };
        serverLayout.Controls.Add(lblHelp, 0, 1);
        serverLayout.SetColumnSpan(lblHelp, 2);

        serverPanel.Controls.Add(serverLayout);
        main.Controls.Add(serverPanel, 0, 1);

        // --- Polling Interval
        var pollingPanel = new GroupBox
        {
            Text = "Cloud Polling",
            Dock = DockStyle.Fill,
            BackColor = Ui.Card,
            ForeColor = Ui.Txt,
            Font = Ui.F(11, FontStyle.Bold),
            Padding = new Padding(12)
        };

        var pollingLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };

        var lblInterval = new Label
        {
            Text = "Check for new rooms every (ms):",
            AutoSize = true,
            Font = Ui.F(10),
            Margin = new Padding(0, 0, 6, 0)
        };

        numPollingInterval = new NumericUpDown
        {
            Minimum = 500,
            Maximum = 10000,
            Value = 2000,
            Increment = 500,
            Width = 80,
            BackColor = Ui.Card2,
            ForeColor = Ui.Txt,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Ui.F(10)
        };

        pollingLayout.Controls.Add(lblInterval);
        pollingLayout.Controls.Add(numPollingInterval);
        pollingPanel.Controls.Add(pollingLayout);
        main.Controls.Add(pollingPanel, 0, 2);

        // --- Info
        var infoLabel = new Label
        {
            Text = "Settings are saved to: " + Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\LanArena",
            Dock = DockStyle.Fill,
            ForeColor = Ui.Muted,
            Font = Ui.F(8, FontStyle.Italic),
            AutoSize = true
        };
        main.Controls.Add(infoLabel, 0, 3);

        // --- Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        btnCancel = Ui.Btn("Cancel", Ui.Card2);
        btnSave = Ui.Btn("Save", Ui.Accent);
        btnReset = Ui.Btn("Reset to Defaults", Ui.Danger);

        btnSave.Click += (_, _) => SaveSettings();
        btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        btnReset.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Reset all settings to defaults?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                AppSettings.ResetToDefaults();
                LoadSettings();
            }
        };

        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnSave);
        buttonPanel.Controls.Add(btnReset);
        main.Controls.Add(buttonPanel, 0, 4);

        Controls.Add(main);
    }

    void LoadSettings()
    {
        chkLanDiscovery.Checked = settings.UseLanDiscovery;
        chkCloudDiscovery.Checked = settings.UseCloudDiscovery;
        txtServerUrl.Text = string.IsNullOrWhiteSpace(settings.ServerUrl) 
            ? "" 
            : settings.ServerUrl;
        numPollingInterval.Value = settings.CloudPollingIntervalMs;
    }

    void SaveSettings()
    {
        settings.UseLanDiscovery = chkLanDiscovery.Checked;
        settings.UseCloudDiscovery = chkCloudDiscovery.Checked;
        settings.ServerUrl = string.IsNullOrWhiteSpace(txtServerUrl.Text)
            ? ServerDiscovery.DefaultServerUrl
            : txtServerUrl.Text;
        settings.CloudPollingIntervalMs = (int)numPollingInterval.Value;

        settings.Save();
        DialogResult = DialogResult.OK;
    }
}
