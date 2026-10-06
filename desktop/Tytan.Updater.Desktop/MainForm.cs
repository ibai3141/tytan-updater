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
    private LocalInstallation? installation;

    public MainForm(string? initialFile = null)
    {
        Text = "Tytan Updater";
        ClientSize = new Size(940, 540);
        MinimumSize = new Size(760, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 1,
            RowCount = 7
        };

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var heading = new Label
        {
            Text = "Installed products",
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

        var openButton = new Button { Text = "Open installation file", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
        var exampleButton = new Button { Text = "Load example", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
        openButton.Click += (_, _) => ChooseFile();
        exampleButton.Click += (_, _) => TryLoad(Path.Combine(AppContext.BaseDirectory, "installation.example.json"), true);
        buttons.Controls.Add(openButton);
        buttons.Controls.Add(exampleButton);

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

        statusLabel.Margin = new Padding(0, 14, 0, 8);
        var note = new Label
        {
            Text = "This first phase reads local information. Online checks and downloads will be added next.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0)
        };

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(clientLabel, 0, 1);
        layout.Controls.Add(fileLabel, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        layout.Controls.Add(productsGrid, 0, 4);
        layout.Controls.Add(statusLabel, 0, 5);
        layout.Controls.Add(note, 0, 6);
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
        // Validate the whole file before replacing any displayed state.
        LocalInstallation loaded = InstallationFileReader.Read(path);
        installation = loaded;
        clientLabel.Text = "Client folder: " + loaded.ClientFolder;
        fileLabel.Text = "Installation file: " + Path.GetFullPath(path);
        productsGrid.Rows.Clear();

        foreach (InstalledProduct product in loaded.Products)
        {
            productsGrid.Rows.Add(product.Name, product.InstalledVersion, "Not checked", "Not checked");
        }

        statusLabel.Text = example
            ? "Example data loaded. These versions do not describe this computer."
            : $"Loaded {loaded.Products.Count} installed products. Online versions have not been checked.";
    }

    // Small inspection surface for the window smoke check, without network access.
    internal string? LoadedClient => installation?.ClientFolder;
    internal int ProductCount => productsGrid.Rows.Count;
    internal string DisplayedVersion(int row) => productsGrid.Rows[row].Cells[1].Value as string
        ?? throw new InvalidOperationException("The product row has no installed version.");
}
