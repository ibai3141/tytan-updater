namespace Tytan.Updater.Desktop;

internal sealed class MainForm : Form
{
    private readonly Label clientLabel = new() { AutoSize = true, Text = "Client folder: no file loaded" };
    private readonly Label fileLabel = new()
    {
        AutoSize = false,
        AutoEllipsis = true,
        Height = 26,
        Dock = DockStyle.Fill,
        Text = "Installation file: none"
    };
    private readonly Label statusLabel = new() { AutoSize = true, Text = "Load your installation file to view installed versions." };
    private readonly DataGridView productsGrid = new();
    private readonly DataGridView cloudGrid = new();
    private readonly Button openButton = new() { Text = "Open installation file", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
    private readonly Button exampleButton = new() { Text = "Load example", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
    private readonly Button queryButton = new() { Text = "Load cloud folder", AutoSize = true, Enabled = false };
    private readonly Button cancelButton = new() { Text = "Cancel", AutoSize = true, Enabled = false };
    private readonly TextBox usernameBox = new() { Width = 145, Text = Environment.GetEnvironmentVariable("TYTAN_API_USERNAME") ?? "TytanSQL" };
    private readonly TextBox passwordBox = new() { Width = 160, UseSystemPasswordChar = true, Text = Environment.GetEnvironmentVariable("TYTAN_API_PASSWORD") ?? "" };
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly CloudApiClient api;
    private CancellationTokenSource? queryCancellation;
    private LocalInstallation? installation;

    public MainForm(string? initialFile = null, CloudApiClient? api = null)
    {
        this.api = api ?? new CloudApiClient();
        Text = "Tytan Updater";
        ClientSize = new Size(980, 640);
        MinimumSize = new Size(900, 530);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 1,
            RowCount = 8
        };

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var heading = new Label
        {
            Text = "Tytan updates",
            AutoSize = true,
            Font = new Font("Segoe UI", 19, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 16)
        };

        clientLabel.Margin = new Padding(0, 0, 0, 6);
        fileLabel.Margin = new Padding(0, 0, 0, 16);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 16)
        };

        openButton.Click += (_, _) => ChooseFile();
        exampleButton.Click += (_, _) => TryLoad(Path.Combine(AppContext.BaseDirectory, "installation.example.json"), true);
        buttons.Controls.Add(openButton);
        buttons.Controls.Add(exampleButton);

        var connection = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 14) };
        connection.Controls.Add(new Label { Text = "Username", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        connection.Controls.Add(usernameBox);
        connection.Controls.Add(new Label { Text = "Password", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        connection.Controls.Add(passwordBox);
        connection.Controls.Add(queryButton);
        connection.Controls.Add(cancelButton);
        queryButton.Click += async (_, _) => await QueryCloudAsync();
        cancelButton.Click += (_, _) => queryCancellation?.Cancel();

        productsGrid.Dock = DockStyle.Fill;
        productsGrid.ReadOnly = true;
        productsGrid.AllowUserToAddRows = false;
        productsGrid.AllowUserToDeleteRows = false;
        productsGrid.RowHeadersVisible = false;
        productsGrid.MultiSelect = false;
        productsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        productsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        productsGrid.BackgroundColor = Color.White;
        productsGrid.BorderStyle = BorderStyle.FixedSingle;
        productsGrid.Columns.Add("Product", "Product");
        productsGrid.Columns.Add("InstalledVersion", "Installed version");
        productsGrid.Columns.Add("AvailableVersion", "Available version");
        productsGrid.Columns.Add("Status", "Status");

        var localTab = new TabPage("Installed products");
        localTab.Controls.Add(productsGrid);
        var cloudTab = new TabPage("Cloud folder");
        cloudGrid.Dock = DockStyle.Fill;
        cloudGrid.ReadOnly = true;
        cloudGrid.AllowUserToAddRows = false;
        cloudGrid.AllowUserToDeleteRows = false;
        cloudGrid.RowHeadersVisible = false;
        cloudGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        cloudGrid.BackgroundColor = Color.White;
        cloudGrid.Columns.Add("Name", "Name");
        cloudGrid.Columns.Add("Type", "Type");
        cloudGrid.Columns.Add("Size", "Size (bytes)");
        cloudGrid.Columns.Add("Modified", "Modified (UTC)");
        cloudGrid.Columns.Add("Path", "Relative path");
        cloudGrid.Columns[0].FillWeight = 150;
        cloudGrid.Columns[1].FillWeight = 45;
        cloudGrid.Columns[2].FillWeight = 70;
        cloudGrid.Columns[3].FillWeight = 120;
        cloudGrid.Columns[4].FillWeight = 180;
        cloudTab.Controls.Add(cloudGrid);
        tabs.TabPages.Add(localTab);
        tabs.TabPages.Add(cloudTab);

        statusLabel.Margin = new Padding(0, 14, 0, 8);
        statusLabel.MaximumSize = new Size(910, 0);
        var note = new Label
        {
            Text = "Cloud packages can be listed. Version comparison and downloads will be added next.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0)
        };

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(clientLabel, 0, 1);
        layout.Controls.Add(fileLabel, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        layout.Controls.Add(connection, 0, 4);
        layout.Controls.Add(tabs, 0, 5);
        layout.Controls.Add(statusLabel, 0, 6);
        layout.Controls.Add(note, 0, 7);
        Controls.Add(layout);

        if (initialFile is not null)
        {
            Shown += (_, _) => TryLoad(initialFile, false);
        }
    }

    private void ChooseFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open local installation information",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            TryLoad(dialog.FileName, false);
        }
    }

    private void TryLoad(string path, bool example)
    {
        try
        {
            LoadInstallation(path, example);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            statusLabel.Text = "The file could not be loaded. Previously loaded data is unchanged.";
            MessageBox.Show(this, error.Message, "Installation file error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal void LoadInstallation(string path, bool example)
    {
        if (queryCancellation is not null)
        {
            throw new InvalidOperationException("Wait for the current cloud request or cancel it before loading another file.");
        }
        // Validate the whole file before replacing any displayed state.
        LocalInstallation loaded = InstallationFileReader.Read(path);
        installation = loaded;
        clientLabel.Text = "Client folder: " + loaded.ClientFolder;
        fileLabel.Text = "Installation file: " + Path.GetFullPath(path);
        productsGrid.Rows.Clear();
        cloudGrid.Rows.Clear();
        tabs.SelectedIndex = 0;
        queryButton.Enabled = true;

        foreach (InstalledProduct product in loaded.Products)
        {
            productsGrid.Rows.Add(product.Name, product.InstalledVersion, "Not checked", "Not checked");
        }

        statusLabel.Text = example
            ? "Example data loaded. These versions do not describe this computer."
            : $"Loaded {loaded.Products.Count} installed products. Online versions have not been checked.";
    }

    internal async Task QueryCloudAsync()
    {
        if (installation is null || queryCancellation is not null)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        queryCancellation = cancellation;
        SetQueryBusy(true);
        cloudGrid.Rows.Clear();
        statusLabel.Text = "Loading cloud folder: " + installation.ClientFolder + "...";

        try
        {
            IReadOnlyList<RemoteEntry> entries = await api.ListAsync(
                installation.ClientFolder, usernameBox.Text, passwordBox.Text, cancellation.Token);
            if (!IsDisposed)
            {
                foreach (RemoteEntry entry in entries)
                {
                    cloudGrid.Rows.Add(entry.Name, entry.Type, entry.Size?.ToString() ?? "—", entry.Modified, entry.Path);
                }
                tabs.SelectedIndex = 1;
                statusLabel.Text = entries.Count == 0
                    ? "The client's cloud folder is empty."
                    : $"Loaded {entries.Count} cloud entries. Versions have not been compared.";
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                statusLabel.Text = cancellation.IsCancellationRequested
                    ? "Cloud request cancelled."
                    : "The cloud request timed out. Try again.";
            }
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        {
            if (!IsDisposed)
            {
                statusLabel.Text = error is HttpRequestException { StatusCode: null }
                    ? "Could not connect to the API. Check your connection and try again."
                    : error.Message;
            }
        }
        finally
        {
            queryCancellation = null;
            if (!IsDisposed)
            {
                SetQueryBusy(false);
            }
        }
    }

    private void SetQueryBusy(bool busy)
    {
        openButton.Enabled = exampleButton.Enabled = usernameBox.Enabled = passwordBox.Enabled = !busy;
        queryButton.Enabled = !busy && installation is not null;
        cancelButton.Enabled = busy;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel)
        {
            queryCancellation?.Cancel();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            queryCancellation?.Cancel();
            api.Dispose();
        }
        base.Dispose(disposing);
    }

    internal void SetCredentials(string username, string password)
    {
        usernameBox.Text = username;
        passwordBox.Text = password;
    }

    internal void CancelCloudQuery() => queryCancellation?.Cancel();
    internal int CloudEntryCount => cloudGrid.Rows.Count;
    internal string StatusText => statusLabel.Text;
    internal bool QueryBusy => queryCancellation is not null;

    // Small inspection surface for the window smoke check, without network access.
    internal string? LoadedClient => installation?.ClientFolder;
    internal int ProductCount => productsGrid.Rows.Count;
    internal string DisplayedVersion(int row) => productsGrid.Rows[row].Cells[1].Value as string
        ?? throw new InvalidOperationException("The product row has no installed version.");
}
