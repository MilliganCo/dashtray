namespace DashTray;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _enabledCheckBox = new();
    private readonly NumericUpDown _intervalInput = new();
    private readonly CheckBox _numpadCheckBox = new();
    private readonly CheckBox _startupCheckBox = new();
    private readonly CheckBox _smartQuotesCheckBox = new();
    private readonly TextBox _replacementTextBox = new();
    private readonly TextBox _excludedProcessesTextBox = new();
    private readonly DataGridView _typographyGrid = new();
    private readonly DataGridView _templatesGrid = new();

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        Result = settings.Clone();

        Text = "Настройки DashTray";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 640);

        BuildInterface(settings);
    }

    private void BuildInterface(AppSettings settings)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeneralPage(settings));
        tabs.TabPages.Add(BuildRulesPage(
            "Типографика",
            _typographyGrid,
            settings.TypographyRules,
            "Правила вроде ... → … и (c) → ©"));
        tabs.TabPages.Add(BuildRulesPage(
            "Шаблоны",
            _templatesGrid,
            settings.TemplateRules,
            "Быстрые замены вроде ;mail → адрес"));

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            WrapContents = false
        };

        var okButton = new Button
        {
            Text = "Сохранить",
            AutoSize = true
        };
        okButton.Click += SaveAndClose;

        var cancelButton = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };

        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);

        root.Controls.Add(tabs, 0, 0);
        root.Controls.Add(buttons, 0, 1);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.Add(root);
    }

    private TabPage BuildGeneralPage(AppSettings settings)
    {
        var page = new TabPage("Основные");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            ColumnCount = 2,
            RowCount = 9
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        for (var i = 0; i < 8; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i is 6 ? 100 : 34));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _enabledCheckBox.Text = "Включить замены";
        _enabledCheckBox.Checked = settings.Enabled;
        _enabledCheckBox.AutoSize = true;
        layout.Controls.Add(_enabledCheckBox, 0, 0);
        layout.SetColumnSpan(_enabledCheckBox, 2);

        AddLabel(layout, "Интервал двойного нажатия:", 1);
        _intervalInput.Minimum = 100;
        _intervalInput.Maximum = 1500;
        _intervalInput.Increment = 25;
        _intervalInput.Value = settings.DoublePressIntervalMs;
        _intervalInput.Width = 120;
        layout.Controls.Add(CreateInlinePanel(_intervalInput, new Label
        {
            Text = "мс",
            AutoSize = true,
            Margin = new Padding(8, 7, 0, 0)
        }), 1, 1);

        AddLabel(layout, "Текст замены для --:", 2);
        _replacementTextBox.Text = settings.ReplacementText;
        _replacementTextBox.MaxLength = 8;
        _replacementTextBox.Width = 120;
        layout.Controls.Add(_replacementTextBox, 1, 2);

        _numpadCheckBox.Text = "Учитывать минус на цифровом блоке";
        _numpadCheckBox.Checked = settings.IncludeNumpadMinus;
        _numpadCheckBox.AutoSize = true;
        layout.Controls.Add(_numpadCheckBox, 0, 3);
        layout.SetColumnSpan(_numpadCheckBox, 2);

        _startupCheckBox.Text = "Запускать вместе с Windows";
        _startupCheckBox.Checked = settings.StartWithWindows;
        _startupCheckBox.AutoSize = true;
        layout.Controls.Add(_startupCheckBox, 0, 4);
        layout.SetColumnSpan(_startupCheckBox, 2);

        _smartQuotesCheckBox.Text = "Умные кавычки: \" → «ёлочки» / „лапки“";
        _smartQuotesCheckBox.Checked = settings.SmartQuotesEnabled;
        _smartQuotesCheckBox.AutoSize = true;
        layout.Controls.Add(_smartQuotesCheckBox, 0, 5);
        layout.SetColumnSpan(_smartQuotesCheckBox, 2);

        AddLabel(layout, "Не обрабатывать приложения:", 6);
        _excludedProcessesTextBox.Text = settings.ExcludedProcesses;
        _excludedProcessesTextBox.Multiline = true;
        _excludedProcessesTextBox.ScrollBars = ScrollBars.Vertical;
        _excludedProcessesTextBox.Dock = DockStyle.Fill;
        layout.Controls.Add(_excludedProcessesTextBox, 1, 6);

        var hint = new Label
        {
            Text = "Имена через запятую, например: code, devenv, cmd\nПауза всех замен: Ctrl+Alt+-",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 4, 3, 8)
        };
        layout.Controls.Add(hint, 0, 7);
        layout.SetColumnSpan(hint, 2);

        page.Controls.Add(layout);
        return page;
    }

    private static TabPage BuildRulesPage(
        string title,
        DataGridView grid,
        IEnumerable<ReplacementRule> rules,
        string hintText)
    {
        var page = new TabPage(title);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        ConfigureRulesGrid(grid);
        foreach (var rule in rules)
        {
            grid.Rows.Add(rule.Trigger, rule.Replacement, rule.IgnoreCase);
        }

        var hint = new Label
        {
            Text = hintText,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Dock = DockStyle.Fill
        };

        layout.Controls.Add(grid, 0, 0);
        layout.Controls.Add(hint, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private static void ConfigureRulesGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = true;
        grid.AllowUserToDeleteRows = true;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.BackgroundColor = SystemColors.Window;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Trigger",
            HeaderText = "Ввод",
            FillWeight = 35
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Replacement",
            HeaderText = "Результат",
            FillWeight = 45
        });
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "IgnoreCase",
            HeaderText = "Без регистра",
            FillWeight = 20
        });
    }

    private void SaveAndClose(object? sender, EventArgs e)
    {
        _typographyGrid.EndEdit();
        _templatesGrid.EndEdit();

        if (string.IsNullOrEmpty(_replacementTextBox.Text))
        {
            MessageBox.Show(
                this,
                "Укажите хотя бы один символ для замены --.",
                "DashTray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _replacementTextBox.Focus();
            return;
        }

        Result = new AppSettings
        {
            Enabled = _enabledCheckBox.Checked,
            DoublePressIntervalMs = (int)_intervalInput.Value,
            IncludeNumpadMinus = _numpadCheckBox.Checked,
            StartWithWindows = _startupCheckBox.Checked,
            SmartQuotesEnabled = _smartQuotesCheckBox.Checked,
            ReplacementText = _replacementTextBox.Text,
            ExcludedProcesses = _excludedProcessesTextBox.Text.Trim(),
            TypographyRules = ReadRules(_typographyGrid),
            TemplateRules = ReadRules(_templatesGrid)
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private static List<ReplacementRule> ReadRules(DataGridView grid)
    {
        var rules = new List<ReplacementRule>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var trigger = Convert.ToString(row.Cells[0].Value)?.Trim() ?? string.Empty;
            if (trigger.Length == 0)
            {
                continue;
            }

            rules.Add(new ReplacementRule
            {
                Trigger = trigger,
                Replacement = Convert.ToString(row.Cells[1].Value) ?? string.Empty,
                IgnoreCase = row.Cells[2].Value is true
            });
        }

        return rules;
    }

    private static void AddLabel(TableLayoutPanel layout, string text, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 7, 3, 3)
        }, 0, row);
    }

    private static Control CreateInlinePanel(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty
        };

        panel.Controls.AddRange(controls);
        return panel;
    }
}
