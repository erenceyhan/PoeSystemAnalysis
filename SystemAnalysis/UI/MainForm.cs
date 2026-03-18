using SystemAnalysis.Automation;
using SystemAnalysis.Config;
using SystemAnalysis.Interop;
using System.Drawing;
using System.Text;

namespace SystemAnalysis.UI;

public sealed class MainForm : Form
{
    private readonly string _configPath;
    private readonly AppConfig _config;
    private readonly GlobalInputMonitor _monitor;
    private readonly object _sync = new();

    private CancellationTokenSource? _runCancellation;
    private Task? _currentRun;

    private readonly List<CheckBox> _matchRuleCheckBoxes = new();
    private readonly List<TextBox> _matchRuleTextBoxes = new();
    private readonly ComboBox _modeComboBox;
    private readonly Label _sourceLabel;
    private readonly Label _targetLabel;
    private readonly Label _inspectLabel;
    private readonly NumericUpDown _delayBeforeStartMinInput;
    private readonly NumericUpDown _delayBeforeStartMaxInput;
    private readonly NumericUpDown _delayBetweenActionsMinInput;
    private readonly NumericUpDown _delayBetweenActionsMaxInput;
    private readonly NumericUpDown _delayAfterCraftMinInput;
    private readonly NumericUpDown _delayAfterCraftMaxInput;
    private readonly NumericUpDown _delayBeforeInspectMinInput;
    private readonly NumericUpDown _delayBeforeInspectMaxInput;
    private readonly NumericUpDown _delayAfterInspectShortcutMinInput;
    private readonly NumericUpDown _delayAfterInspectShortcutMaxInput;
    private readonly TextBox _logDirectoryTextBox;
    private readonly CheckBox _showCopiedTextCheckBox;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly TextBox _logTextBox;
    private readonly object _fileLogSync = new();
    private string? _currentLogFilePath;
    private string _lastClipboardText = string.Empty;

    public MainForm(string configPath, AppConfig config)
    {
        _configPath = configPath;
        _config = config;
        _monitor = new GlobalInputMonitor(config.Safety);

        Text = "SystemAnalysis";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 860);
        Size = new Size(1160, 980);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8,
            FixedPanel = FixedPanel.Panel2
        };

        var topScrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12)
        };

        var topContent = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };

        var settingsGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = "Craft Ayarlari",
            AutoSize = true,
            Width = 1090
        };

        var settingsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            AutoSize = true
        };
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var i = 0; i < 5; i++)
        {
            var rule = _config.ClipboardCheck.MatchRules[i];
            settingsGrid.Controls.Add(new Label { Text = $"Aranan Mod {i + 1}", AutoSize = true, Anchor = AnchorStyles.Left }, 0, i);
            var rulePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true
            };
            rulePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rulePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var enabledCheckBox = new CheckBox { AutoSize = true, Checked = rule.Enabled, Text = "Aktif" };
            var ruleTextBox = new TextBox { Dock = DockStyle.Top, Text = rule.Text };
            _matchRuleCheckBoxes.Add(enabledCheckBox);
            _matchRuleTextBoxes.Add(ruleTextBox);
            rulePanel.Controls.Add(enabledCheckBox, 0, 0);
            rulePanel.Controls.Add(ruleTextBox, 1, 0);
            settingsGrid.Controls.Add(rulePanel, 1, i);
        }

        settingsGrid.Controls.Add(new Label { Text = "Calisma Modu", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        _modeComboBox = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _modeComboBox.Items.AddRange(new object[]
        {
            new ModeOption("single_craft", "Tekli Uretim"),
            new ModeOption("hold_shift_spam", "Shift Basili Tekrarli Tiklama")
        });
        SelectMode();
        settingsGrid.Controls.Add(_modeComboBox, 1, 5);

        settingsGrid.Controls.Add(new Label { Text = "Log Klasoru", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        var logPathPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true
        };
        logPathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logPathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _logDirectoryTextBox = new TextBox { Dock = DockStyle.Top, Text = _config.Logging.DirectoryPath };
        logPathPanel.Controls.Add(_logDirectoryTextBox, 0, 0);
        var chooseLogFolderButton = new Button { Text = "Klasor Sec", AutoSize = true };
        chooseLogFolderButton.Click += (_, _) => ChooseLogDirectory();
        logPathPanel.Controls.Add(chooseLogFolderButton, 1, 0);
        settingsGrid.Controls.Add(logPathPanel, 1, 6);

        settingsGrid.Controls.Add(new Label { Text = "Kopyala Goster", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        _showCopiedTextCheckBox = new CheckBox
        {
            AutoSize = true,
            Checked = _config.Logging.ShowCopiedText,
            Text = "Kopyalanan metni logla"
        };
        _showCopiedTextCheckBox.CheckedChanged += (_, _) => SaveUiToConfig();
        settingsGrid.Controls.Add(_showCopiedTextCheckBox, 1, 7);

        settingsGroup.Controls.Add(settingsGrid);

        var timingGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = "Zamanlama (ms)",
            AutoSize = true,
            Width = 1090
        };

        var timingGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 5,
            AutoSize = true
        };
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

        timingGrid.Controls.Add(new Label { Text = "Baslamadan Once", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        timingGrid.Controls.Add(new Label { Text = "Min", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 0);
        _delayBeforeStartMinInput = CreateTimingInput(_config.Timing.DelayBeforeStartMs.Min);
        timingGrid.Controls.Add(_delayBeforeStartMinInput, 2, 0);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 0);
        _delayBeforeStartMaxInput = CreateTimingInput(_config.Timing.DelayBeforeStartMs.Max);
        timingGrid.Controls.Add(_delayBeforeStartMaxInput, 4, 0);

        timingGrid.Controls.Add(new Label { Text = "Aksiyon Arasi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        timingGrid.Controls.Add(new Label { Text = "Min", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 1);
        _delayBetweenActionsMinInput = CreateTimingInput(_config.Timing.DelayBetweenActionsMs.Min);
        timingGrid.Controls.Add(_delayBetweenActionsMinInput, 2, 1);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 1);
        _delayBetweenActionsMaxInput = CreateTimingInput(_config.Timing.DelayBetweenActionsMs.Max);
        timingGrid.Controls.Add(_delayBetweenActionsMaxInput, 4, 1);

        timingGrid.Controls.Add(new Label { Text = "Craft Sonrasi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        timingGrid.Controls.Add(new Label { Text = "Min", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 2);
        _delayAfterCraftMinInput = CreateTimingInput(_config.Timing.DelayAfterCraftMs.Min);
        timingGrid.Controls.Add(_delayAfterCraftMinInput, 2, 2);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 2);
        _delayAfterCraftMaxInput = CreateTimingInput(_config.Timing.DelayAfterCraftMs.Max);
        timingGrid.Controls.Add(_delayAfterCraftMaxInput, 4, 2);

        timingGrid.Controls.Add(new Label { Text = "Inspect Oncesi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        timingGrid.Controls.Add(new Label { Text = "Min", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 3);
        _delayBeforeInspectMinInput = CreateTimingInput(_config.Timing.DelayBeforeInspectMs.Min);
        timingGrid.Controls.Add(_delayBeforeInspectMinInput, 2, 3);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 3);
        _delayBeforeInspectMaxInput = CreateTimingInput(_config.Timing.DelayBeforeInspectMs.Max);
        timingGrid.Controls.Add(_delayBeforeInspectMaxInput, 4, 3);

        timingGrid.Controls.Add(new Label { Text = "Kisayol Sonrasi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        timingGrid.Controls.Add(new Label { Text = "Min", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 4);
        _delayAfterInspectShortcutMinInput = CreateTimingInput(_config.Timing.DelayAfterInspectShortcutMs.Min);
        timingGrid.Controls.Add(_delayAfterInspectShortcutMinInput, 2, 4);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 4);
        _delayAfterInspectShortcutMaxInput = CreateTimingInput(_config.Timing.DelayAfterInspectShortcutMs.Max);
        timingGrid.Controls.Add(_delayAfterInspectShortcutMaxInput, 4, 4);

        timingGroup.Controls.Add(timingGrid);

        var pointsGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = "Noktalar",
            AutoSize = true,
            Width = 1090
        };

        var pointsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 3,
            AutoSize = true
        };
        pointsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        pointsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pointsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        pointsGrid.Controls.Add(new Label { Text = "Currency Noktasi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _sourceLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        pointsGrid.Controls.Add(_sourceLabel, 1, 0);
        var captureSourceButton = new Button { Text = "Mouse'tan Kaydet (F6)", AutoSize = true, Anchor = AnchorStyles.Right };
        captureSourceButton.Click += (_, _) => CapturePoint(PointKind.Source);
        pointsGrid.Controls.Add(captureSourceButton, 2, 0);

        pointsGrid.Controls.Add(new Label { Text = "Item Noktasi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _targetLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        pointsGrid.Controls.Add(_targetLabel, 1, 1);
        var captureTargetButton = new Button { Text = "Mouse'tan Kaydet (F7)", AutoSize = true, Anchor = AnchorStyles.Right };
        captureTargetButton.Click += (_, _) => CapturePoint(PointKind.Target);
        pointsGrid.Controls.Add(captureTargetButton, 2, 1);

        pointsGrid.Controls.Add(new Label { Text = "Kontrol Noktasi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _inspectLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        pointsGrid.Controls.Add(_inspectLabel, 1, 2);
        var captureInspectButton = new Button { Text = "Mouse'tan Kaydet (F10)", AutoSize = true, Anchor = AnchorStyles.Right };
        captureInspectButton.Click += (_, _) => CapturePoint(PointKind.Inspect);
        pointsGrid.Controls.Add(captureInspectButton, 2, 2);

        pointsGroup.Controls.Add(pointsGrid);

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 4),
            Width = 1090,
            WrapContents = true
        };

        _startButton = new Button { Text = "Baslat (F8)", AutoSize = true };
        _startButton.Click += (_, _) => StartRun();
        actionsPanel.Controls.Add(_startButton);

        _stopButton = new Button { Text = "Durdur (F9)", AutoSize = true, Enabled = false };
        _stopButton.Click += (_, _) => StopCurrentRun("Durdurma istendi.");
        actionsPanel.Controls.Add(_stopButton);

        var saveButton = new Button { Text = "Ayarlari Kaydet", AutoSize = true };
        saveButton.Click += (_, _) =>
        {
            SaveUiToConfig();
            AppendLog("Ayarlar kaydedildi.");
        };
        actionsPanel.Controls.Add(saveButton);

        var helpLabel = new Label
        {
            Text = "Genelde kontrol noktasi item ile ayni olur. Esc kapatir; bot calisirken fare veya klavye hareketi otomatik durdurur.",
            AutoSize = true,
            Margin = new Padding(18, 8, 0, 0)
        };
        actionsPanel.Controls.Add(helpLabel);

        var logGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Log",
            Padding = new Padding(12)
        };

        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true
        };
        logGroup.Controls.Add(_logTextBox);

        topContent.Controls.Add(settingsGroup);
        topContent.Controls.Add(timingGroup);
        topContent.Controls.Add(pointsGroup);
        topContent.Controls.Add(actionsPanel);
        topScrollPanel.Controls.Add(topContent);

        splitContainer.Panel1.Controls.Add(topScrollPanel);
        splitContainer.Panel2.Padding = new Padding(12, 0, 12, 12);
        splitContainer.Panel2.Controls.Add(logGroup);

        Controls.Add(splitContainer);

        Shown += (_, _) =>
        {
            splitContainer.Panel2MinSize = 260;
            var desiredLogHeight = Math.Max(splitContainer.Panel2MinSize, 280);
            var targetDistance = Math.Max(200, splitContainer.Height - desiredLogHeight - splitContainer.SplitterWidth);

            if (targetDistance > 0)
            {
                splitContainer.SplitterDistance = targetDistance;
            }
        };

        RefreshPointLabels();
        HookMonitorEvents();
        _monitor.Start();
        NativeMethods.AddClipboardFormatListener(Handle);
        AppendLog("Arayuz hazir.");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        StopCurrentRun("Pencere kapatildi.");
        InputController.ReleaseCommonModifiers();
        NativeMethods.RemoveClipboardFormatListener(Handle);
        _monitor.Dispose();
        base.OnFormClosed(e);
    }

    private void HookMonitorEvents()
    {
        _monitor.StartRequested += () => InvokeOnUi(StartRun);
        _monitor.StopRequested += () => InvokeOnUi(() => StopCurrentRun("Durdurma istendi."));
        _monitor.ExitRequested += () => InvokeOnUi(Close);
        _monitor.CaptureSourceRequested += () => InvokeOnUi(() => CapturePoint(PointKind.Source));
        _monitor.CaptureTargetRequested += () => InvokeOnUi(() => CapturePoint(PointKind.Target));
        _monitor.CaptureInspectRequested += () => InvokeOnUi(() => CapturePoint(PointKind.Inspect));
    }

    private void StartRun()
    {
        lock (_sync)
        {
            if (_currentRun is { IsCompleted: false })
            {
                AppendLog("Islem zaten calisiyor.");
                return;
            }

            SaveUiToConfig();
            _runCancellation = new CancellationTokenSource();
            PrepareRunLogFile();
            _monitor.IsArmed = true;
            SetRunningState(true);
            AppendLog("Baslatma istendi.");

            _currentRun = Task.Run(async () =>
            {
                try
                {
                    var runner = new AnalysisRunner(_config, AppendLogThreadSafe);
                    await runner.RunAsync(_runCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    AppendLogThreadSafe("Islem guvenli sekilde durduruldu.");
                }
                catch (Exception ex)
                {
                    AppendLogThreadSafe($"Hata: {ex.Message}");
                }
                finally
                {
                    lock (_sync)
                    {
                        _monitor.IsArmed = false;
                        _runCancellation?.Dispose();
                        _runCancellation = null;
                    }

                    InvokeOnUi(() => SetRunningState(false));
                }
            });
        }
    }

    private void StopCurrentRun(string reason)
    {
        lock (_sync)
        {
            if (_runCancellation is null)
            {
                return;
            }

            AppendLog(reason);
            _monitor.IsArmed = false;
            _runCancellation.Cancel();
            InputController.ReleaseCommonModifiers();
        }
    }

    private void SaveUiToConfig()
    {
        for (var i = 0; i < _config.ClipboardCheck.MatchRules.Count && i < _matchRuleTextBoxes.Count; i++)
        {
            _config.ClipboardCheck.MatchRules[i].Enabled = _matchRuleCheckBoxes[i].Checked;
            _config.ClipboardCheck.MatchRules[i].Text = _matchRuleTextBoxes[i].Text.Trim();
        }

        var firstActiveRule = _config.ClipboardCheck.GetActiveRules().FirstOrDefault();
        _config.ClipboardCheck.MustContain = firstActiveRule?.Text ?? string.Empty;
        if (_modeComboBox.SelectedItem is ModeOption option)
        {
            _config.Mode = option.Value;
        }

        _config.Timing.DelayBeforeStartMs = CreateRange(_delayBeforeStartMinInput, _delayBeforeStartMaxInput);
        _config.Timing.DelayBetweenActionsMs = CreateRange(_delayBetweenActionsMinInput, _delayBetweenActionsMaxInput);
        _config.Timing.DelayAfterCraftMs = CreateRange(_delayAfterCraftMinInput, _delayAfterCraftMaxInput);
        _config.Timing.DelayBeforeInspectMs = CreateRange(_delayBeforeInspectMinInput, _delayBeforeInspectMaxInput);
        _config.Timing.DelayAfterInspectShortcutMs = CreateRange(_delayAfterInspectShortcutMinInput, _delayAfterInspectShortcutMaxInput);
        _config.Logging.DirectoryPath = _logDirectoryTextBox.Text.Trim();
        _config.Logging.ShowCopiedText = _showCopiedTextCheckBox.Checked;

        ConfigLoader.Save(_configPath, _config);
    }

    private void CapturePoint(PointKind pointKind)
    {
        var (x, y) = InputController.GetMousePosition();

        switch (pointKind)
        {
            case PointKind.Source:
                _config.SourcePoint.X = x;
                _config.SourcePoint.Y = y;
                break;
            case PointKind.Target:
                _config.TargetPoint.X = x;
                _config.TargetPoint.Y = y;
                break;
            case PointKind.Inspect:
                _config.InspectPoint.X = x;
                _config.InspectPoint.Y = y;
                break;
        }

        ConfigLoader.Save(_configPath, _config);
        RefreshPointLabels();
        AppendLog($"{pointKind} noktasi kaydedildi: {x}, {y}");
    }

    private void RefreshPointLabels()
    {
        _sourceLabel.Text = FormatPoint(_config.SourcePoint);
        _targetLabel.Text = FormatPoint(_config.TargetPoint);
        _inspectLabel.Text = FormatPoint(_config.InspectPoint);
    }

    private void SetRunningState(bool isRunning)
    {
        _startButton.Enabled = !isRunning;
        _stopButton.Enabled = isRunning;
    }

    private void SelectMode()
    {
        foreach (var item in _modeComboBox.Items)
        {
            if (item is ModeOption option && option.Value == _config.Mode)
            {
                _modeComboBox.SelectedItem = item;
                return;
            }
        }

        if (_modeComboBox.Items.Count > 0)
        {
            _modeComboBox.SelectedIndex = 0;
        }
    }

    private void AppendLogThreadSafe(string message)
    {
        InvokeOnUi(() => AppendLog(message));
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        _logTextBox.AppendText(line);
        WriteLogToFile(line);
    }

    private void ChooseLogDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Log dosyalarinin kaydedilecegi klasoru sec",
            InitialDirectory = Directory.Exists(_logDirectoryTextBox.Text) ? _logDirectoryTextBox.Text : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _logDirectoryTextBox.Text = dialog.SelectedPath;
            SaveUiToConfig();
            AppendLog($"Log klasoru guncellendi: {dialog.SelectedPath}");
        }
    }

    private void PrepareRunLogFile()
    {
        var directory = _logDirectoryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(AppContext.BaseDirectory, "logs");
            _logDirectoryTextBox.Text = directory;
        }

        Directory.CreateDirectory(directory);
        _currentLogFilePath = Path.Combine(directory, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        var header = new StringBuilder()
            .AppendLine($"SystemAnalysis log basladi: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Mod: {_config.Mode}")
            .AppendLine($"Aranan Mod: {_config.ClipboardCheck.MustContain}")
            .AppendLine($"Currency Noktasi: {FormatPoint(_config.SourcePoint)}")
            .AppendLine($"Item Noktasi: {FormatPoint(_config.TargetPoint)}")
            .AppendLine($"Kontrol Noktasi: {FormatPoint(_config.InspectPoint)}")
            .AppendLine(new string('-', 48))
            .ToString();

        File.WriteAllText(_currentLogFilePath, header, Encoding.UTF8);
    }

    private void WriteLogToFile(string line)
    {
        if (string.IsNullOrWhiteSpace(_currentLogFilePath))
        {
            return;
        }

        lock (_fileLogSync)
        {
            File.AppendAllText(_currentLogFilePath, line, Encoding.UTF8);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            HandleClipboardUpdate();
        }

        base.WndProc(ref m);
    }

    private void HandleClipboardUpdate()
    {
        if (!_showCopiedTextCheckBox.Checked)
        {
            return;
        }

        try
        {
            var text = ClipboardHelper.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (string.Equals(text, _lastClipboardText, StringComparison.Ordinal))
            {
                return;
            }

            _lastClipboardText = text;
            AppendLog($"Kopyalanan metin: {text.Replace(Environment.NewLine, " | ")}");
        }
        catch (Exception ex)
        {
            AppendLog($"Clipboard izleme hatasi: {ex.Message}");
        }
    }

    private void InvokeOnUi(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
            return;
        }

        action();
    }

    private static string FormatPoint(PointConfig point)
    {
        return $"X: {point.X}, Y: {point.Y}";
    }

    private static NumericUpDown CreateTimingInput(int value)
    {
        return new NumericUpDown
        {
            Minimum = 0,
            Maximum = 10000,
            Increment = 10,
            Value = Math.Max(0, Math.Min(value, 10000)),
            Width = 100,
            ThousandsSeparator = false
        };
    }

    private static DelayRange CreateRange(NumericUpDown minInput, NumericUpDown maxInput)
    {
        return new DelayRange(Decimal.ToInt32(minInput.Value), Decimal.ToInt32(maxInput.Value));
    }

    private enum PointKind
    {
        Source,
        Target,
        Inspect
    }

    private sealed record ModeOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }
}
