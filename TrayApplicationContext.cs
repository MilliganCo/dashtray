namespace DashTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly object _settingsLock = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon? _ownedTrayIcon;
    private readonly ToolStripMenuItem _enabledMenuItem;
    private readonly ToolStripMenuItem _startupMenuItem;
    private readonly ToolStripMenuItem _excludeCurrentMenuItem;
    private readonly ReplacementEngine _engine = new();
    private readonly KeyboardHook _keyboardHook;
    private AppSettings _settings;
    private AppSettings _settingsSnapshot;
    private bool _paused;
    private bool _exiting;

    public TrayApplicationContext()
    {
        _settings = SettingsStore.Load();
        _settingsSnapshot = _settings.Clone();
        _engine.UpdateRules(_settingsSnapshot);

        try
        {
            StartupManager.SetEnabled(_settings.StartWithWindows);
        }
        catch
        {
            _settings.StartWithWindows = false;
            SettingsStore.Save(_settings);
            PublishSettings();
        }

        _enabledMenuItem = new ToolStripMenuItem("Замена включена")
        {
            CheckOnClick = true,
            Checked = _settings.Enabled
        };
        _enabledMenuItem.CheckedChanged += ToggleEnabled;

        _startupMenuItem = new ToolStripMenuItem("Запускать с Windows")
        {
            CheckOnClick = true,
            Checked = _settings.StartWithWindows
        };
        _startupMenuItem.CheckedChanged += ToggleStartup;

        _excludeCurrentMenuItem = new ToolStripMenuItem("Не обрабатывать текущее приложение");
        _excludeCurrentMenuItem.Click += (_, _) => ExcludeCurrentApplication();

        var settingsMenuItem = new ToolStripMenuItem("Настройки…");
        settingsMenuItem.Click += (_, _) => ShowSettings();

        var exitMenuItem = new ToolStripMenuItem("Выход");
        exitMenuItem.Click += (_, _) => ExitApplication();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledMenuItem);
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(_excludeCurrentMenuItem);
        menu.Items.Add(settingsMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitMenuItem);

        _ownedTrayIcon = TryLoadApplicationIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _ownedTrayIcon ?? SystemIcons.Application,
            Text = BuildTooltip(_settings, _paused),
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        try
        {
            _keyboardHook = new KeyboardHook(
                GetSettingsSnapshot,
                () => _paused,
                TogglePause,
                OnPauseChanged,
                _engine);
        }
        catch (Exception exception)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _ownedTrayIcon?.Dispose();
            MessageBox.Show(
                $"DashTray не смог запуститься.\n\n{exception.Message}",
                "DashTray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            throw;
        }
    }

    private AppSettings GetSettingsSnapshot() => _settingsSnapshot;

    private void PublishSettings()
    {
        _settingsSnapshot = _settings.Clone();
        _engine.UpdateRules(_settingsSnapshot);
    }

    private void TogglePause()
    {
        lock (_settingsLock)
        {
            _paused = !_paused;
        }
    }

    private void OnPauseChanged() => RefreshTrayState();

    private void ToggleEnabled(object? sender, EventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        lock (_settingsLock)
        {
            _settings.Enabled = _enabledMenuItem.Checked;
            SettingsStore.Save(_settings);
            PublishSettings();
            RefreshTrayState();
        }
    }

    private void ToggleStartup(object? sender, EventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        try
        {
            StartupManager.SetEnabled(_startupMenuItem.Checked);

            lock (_settingsLock)
            {
                _settings.StartWithWindows = _startupMenuItem.Checked;
                SettingsStore.Save(_settings);
            }
        }
        catch (Exception exception)
        {
            _startupMenuItem.CheckedChanged -= ToggleStartup;
            _startupMenuItem.Checked = !_startupMenuItem.Checked;
            _startupMenuItem.CheckedChanged += ToggleStartup;

            MessageBox.Show(
                $"Не удалось изменить автозапуск.\n\n{exception.Message}",
                "DashTray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void ExcludeCurrentApplication()
    {
        var processName = ProcessHelper.GetForegroundProcessName();
        if (string.IsNullOrWhiteSpace(processName)
            || processName.Equals("DashTray", StringComparison.OrdinalIgnoreCase))
        {
            processName = ForegroundTracker.LastExternalProcessName;
        }

        if (string.IsNullOrWhiteSpace(processName))
        {
            _notifyIcon.ShowBalloonTip(
                2500,
                "DashTray",
                "Не удалось определить текущее приложение. Кликните в его окно и наберите символ, затем повторите.",
                ToolTipIcon.Warning);
            return;
        }

        lock (_settingsLock)
        {
            var exclusions = _settings.GetExcludedProcessSet();
            if (!exclusions.Add(processName))
            {
                _notifyIcon.ShowBalloonTip(
                    2500,
                    "DashTray",
                    $"«{processName}» уже в исключениях.",
                    ToolTipIcon.Info);
                return;
            }

            _settings.ExcludedProcesses = string.Join(", ", exclusions.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            SettingsStore.Save(_settings);
            PublishSettings();
        }

        _notifyIcon.ShowBalloonTip(
            2500,
            "DashTray",
            $"Добавлено в исключения: {processName}",
            ToolTipIcon.Info);
    }

    private void ShowSettings()
    {
        AppSettings current;
        lock (_settingsLock)
        {
            current = _settings.Clone();
        }

        using var form = new SettingsForm(current);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        try
        {
            StartupManager.SetEnabled(form.Result.StartWithWindows);
            SettingsStore.Save(form.Result);

            lock (_settingsLock)
            {
                _settings = form.Result.Clone();
                PublishSettings();
            }

            RefreshTrayState();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Не удалось сохранить настройки.\n\n{exception.Message}",
                "DashTray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RefreshTrayState()
    {
        AppSettings snapshot;
        bool paused;
        lock (_settingsLock)
        {
            snapshot = _settings.Clone();
            paused = _paused;
        }

        _enabledMenuItem.CheckedChanged -= ToggleEnabled;
        _enabledMenuItem.Checked = snapshot.Enabled;
        _enabledMenuItem.Text = snapshot.Enabled ? "Замена включена" : "Замена выключена";
        _enabledMenuItem.CheckedChanged += ToggleEnabled;

        _startupMenuItem.CheckedChanged -= ToggleStartup;
        _startupMenuItem.Checked = snapshot.StartWithWindows;
        _startupMenuItem.CheckedChanged += ToggleStartup;

        _notifyIcon.Text = BuildTooltip(snapshot, paused);
    }

    private static Icon? TryLoadApplicationIcon()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            return string.IsNullOrWhiteSpace(executablePath)
                ? null
                : Icon.ExtractAssociatedIcon(executablePath);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildTooltip(AppSettings settings, bool paused)
    {
        if (paused)
        {
            return "DashTray: пауза (Ctrl+Alt+-)";
        }

        return settings.Enabled
            ? $"DashTray: -- → {settings.ReplacementText}"
            : "DashTray: замена выключена";
    }

    private void ExitApplication()
    {
        _exiting = true;
        _keyboardHook.Dispose();
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keyboardHook.Dispose();
            _notifyIcon.Dispose();
            _ownedTrayIcon?.Dispose();
        }

        base.Dispose(disposing);
    }
}
