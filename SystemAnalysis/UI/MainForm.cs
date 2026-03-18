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
    private readonly object _fileLogSync = new();

    private CancellationTokenSource? _runCancellation;
    private Task? _currentRun;
    private string? _currentLogFilePath;
    private string _lastClipboardText = string.Empty;

    private readonly List<ComboBox> _matchRuleComboBoxes = new();
    private readonly List<ComboBox> _augmentRuleComboBoxes = new();
    private readonly List<TextBox> _matchRuleThresholdTextBoxes = new();
    private readonly List<TextBox> _augmentRuleThresholdTextBoxes = new();
    private readonly ComboBox _modeComboBox;
    private readonly CheckBox _useAugmentCycleCheckBox;
    private readonly NumericUpDown _maxClicksPerItemRoundInput;
    private readonly Label _nextCurrencyCaptureLabel;
    private readonly ListBox _currencyPointsListBox;
    private readonly Label _targetLabel;
    private readonly ListBox _targetPointsListBox;
    private readonly ListBox _savedModsListBox;
    private readonly TextBox _newSavedModTextBox;
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
    private PointKind _nextCurrencyCaptureKind = PointKind.Source;
    private bool _isRefreshingUi;

    public MainForm(string configPath, AppConfig config)
    {
        _configPath = configPath;
        _config = config;
        _monitor = new GlobalInputMonitor(config.Safety);

        Text = "SystemAnalysis";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 760);
        Size = new Size(1040, 860);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8,
            FixedPanel = FixedPanel.Panel2
        };

        var panel1Container = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        var settingsTab = CreateTabPage("Craft Ayarlari 1");
        var settingsTabTwo = CreateTabPage("Craft Ayarlari 2");
        var augmentTab = CreateTabPage("Augment");
        var timingTab = CreateTabPage("Zamanlama");
        var pointsTab = CreateTabPage("Noktalar");
        var modsTab = CreateTabPage("Kayitli Modlar");

        var settingsGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = "Craft Ayarlari",
            AutoSize = true,
            Width = 970
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

        AddMatchRuleRows(settingsGrid, 0, 7);

        _modeComboBox = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = false
        };
        _modeComboBox.Items.Add(new ModeOption("hold_shift_spam", "Shift Basili Tekrarli Tiklama"));
        SelectMode();

        settingsGrid.Controls.Add(new Label { Text = "Item Tur Limiti", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        _maxClicksPerItemRoundInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 10000,
            Increment = 10,
            Value = Math.Max(1, Math.Min(_config.MaxClicksPerItemRound, 10000)),
            Width = 100
        };
        _maxClicksPerItemRoundInput.ValueChanged += (_, _) => SaveUiToConfig();
        settingsGrid.Controls.Add(_maxClicksPerItemRoundInput, 1, 7);

        settingsGrid.Controls.Add(new Label { Text = "Augment Akisi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
        _useAugmentCycleCheckBox = new CheckBox
        {
            AutoSize = true,
            Checked = _config.ClipboardCheck.UseAugmentCycle,
            Text = "Flask icin alteration sonrasi augment uygula"
        };
        _useAugmentCycleCheckBox.CheckedChanged += (_, _) => SaveUiToConfig();
        settingsGrid.Controls.Add(_useAugmentCycleCheckBox, 1, 8);

        settingsGrid.Controls.Add(new Label { Text = "Log Klasoru", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 9);
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
        settingsGrid.Controls.Add(logPathPanel, 1, 9);

        settingsGrid.Controls.Add(new Label { Text = "Kopyala Goster", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 10);
        _showCopiedTextCheckBox = new CheckBox
        {
            AutoSize = true,
            Checked = _config.Logging.ShowCopiedText,
            Text = "Kopyalanan metni logla"
        };
        _showCopiedTextCheckBox.CheckedChanged += (_, _) => SaveUiToConfig();
        settingsGrid.Controls.Add(_showCopiedTextCheckBox, 1, 10);

        settingsGroup.Controls.Add(settingsGrid);

        var settingsGroupTwo = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = "Craft Ayarlari 2",
            AutoSize = true,
            Width = 970
        };

        var settingsGridTwo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            AutoSize = true
        };
        settingsGridTwo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        settingsGridTwo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddMatchRuleRows(settingsGridTwo, 7, 8);
        settingsGroupTwo.Controls.Add(settingsGridTwo);

        var augmentGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = "Augment Hedefleri",
            AutoSize = true,
            Width = 970
        };

        var augmentGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            AutoSize = true
        };
        augmentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        augmentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        augmentGrid.Controls.Add(new Label
        {
            Text = "Bu sekmedeki modlardan herhangi biri, alteration ile bulunan modun yanina geldiyse item tamam sayilir.",
            AutoSize = true,
            MaximumSize = new Size(760, 0)
        }, 1, 0);

        AddAugmentRuleRows(augmentGrid, 0, 8, 1);
        augmentGroup.Controls.Add(augmentGrid);

        var timingGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = "Zamanlama (ms)",
            AutoSize = true,
            Width = 970
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
        _delayBeforeStartMinInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayBeforeStartMinInput, 2, 0);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 0);
        _delayBeforeStartMaxInput = CreateTimingInput(_config.Timing.DelayBeforeStartMs.Max);
        _delayBeforeStartMaxInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayBeforeStartMaxInput, 4, 0);

        timingGrid.Controls.Add(new Label { Text = "Aksiyon Arasi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        timingGrid.Controls.Add(new Label { Text = "Min", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 1);
        _delayBetweenActionsMinInput = CreateTimingInput(_config.Timing.DelayBetweenActionsMs.Min);
        _delayBetweenActionsMinInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayBetweenActionsMinInput, 2, 1);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 1);
        _delayBetweenActionsMaxInput = CreateTimingInput(_config.Timing.DelayBetweenActionsMs.Max);
        _delayBetweenActionsMaxInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayBetweenActionsMaxInput, 4, 1);

        timingGrid.Controls.Add(new Label { Text = "Craft Sonrasi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        timingGrid.Controls.Add(new Label { Text = "Min", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 2);
        _delayAfterCraftMinInput = CreateTimingInput(_config.Timing.DelayAfterCraftMs.Min);
        _delayAfterCraftMinInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayAfterCraftMinInput, 2, 2);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 2);
        _delayAfterCraftMaxInput = CreateTimingInput(_config.Timing.DelayAfterCraftMs.Max);
        _delayAfterCraftMaxInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayAfterCraftMaxInput, 4, 2);

        timingGrid.Controls.Add(new Label { Text = "Inspect Oncesi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        timingGrid.Controls.Add(new Label { Text = "Min", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 3);
        _delayBeforeInspectMinInput = CreateTimingInput(_config.Timing.DelayBeforeInspectMs.Min);
        _delayBeforeInspectMinInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayBeforeInspectMinInput, 2, 3);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 3);
        _delayBeforeInspectMaxInput = CreateTimingInput(_config.Timing.DelayBeforeInspectMs.Max);
        _delayBeforeInspectMaxInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayBeforeInspectMaxInput, 4, 3);

        timingGrid.Controls.Add(new Label { Text = "Kisayol Sonrasi", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        timingGrid.Controls.Add(new Label { Text = "Min", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 4);
        _delayAfterInspectShortcutMinInput = CreateTimingInput(_config.Timing.DelayAfterInspectShortcutMs.Min);
        _delayAfterInspectShortcutMinInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayAfterInspectShortcutMinInput, 2, 4);
        timingGrid.Controls.Add(new Label { Text = "Max", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 4);
        _delayAfterInspectShortcutMaxInput = CreateTimingInput(_config.Timing.DelayAfterInspectShortcutMs.Max);
        _delayAfterInspectShortcutMaxInput.ValueChanged += (_, _) => SaveUiToConfig();
        timingGrid.Controls.Add(_delayAfterInspectShortcutMaxInput, 4, 4);

        timingGroup.Controls.Add(timingGrid);

        var pointsGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = "Noktalar",
            AutoSize = true,
            Width = 970
        };

        var pointsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 3,
            AutoSize = true
        };
        pointsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        pointsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pointsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));

        pointsGrid.Controls.Add(new Label { Text = "Currency Noktalari", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        var currencyPointsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true
        };
        _nextCurrencyCaptureLabel = new Label { AutoSize = true };
        _currencyPointsListBox = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 72,
            IntegralHeight = false
        };
        currencyPointsPanel.Controls.Add(_nextCurrencyCaptureLabel, 0, 0);
        currencyPointsPanel.Controls.Add(_currencyPointsListBox, 0, 1);
        pointsGrid.Controls.Add(currencyPointsPanel, 1, 0);

        var currencyButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };
        var captureCurrencyButton = new Button { Text = "Mouse'tan Kaydet (F6)", AutoSize = true, Anchor = AnchorStyles.Right };
        captureCurrencyButton.Click += (_, _) => CapturePoint(PointKind.Source);
        currencyButtonsPanel.Controls.Add(captureCurrencyButton);

        var resetCurrencyButton = new Button { Text = "Currencyleri Sifirla", AutoSize = true };
        resetCurrencyButton.Click += (_, _) => ResetCurrencyPoints();
        currencyButtonsPanel.Controls.Add(resetCurrencyButton);
        pointsGrid.Controls.Add(currencyButtonsPanel, 2, 0);

        pointsGrid.Controls.Add(new Label { Text = "Item Noktalari", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        var itemPointsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true
        };
        itemPointsPanel.Controls.Add(new Label
        {
            Text = "Item noktalari oturumluktur.",
            AutoSize = true
        }, 0, 0);
        _targetLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        _targetPointsListBox = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 130,
            IntegralHeight = false
        };
        itemPointsPanel.Controls.Add(_targetLabel, 0, 1);
        itemPointsPanel.Controls.Add(_targetPointsListBox, 0, 2);
        pointsGrid.Controls.Add(itemPointsPanel, 1, 1);

        var itemButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };
        var captureTargetButton = new Button { Text = "Mouse'tan Ekle (F7)", AutoSize = true };
        captureTargetButton.Click += (_, _) => CapturePoint(PointKind.Target);
        itemButtonsPanel.Controls.Add(captureTargetButton);

        var removeLastTargetButton = new Button { Text = "Sonuncuyu Sil", AutoSize = true };
        removeLastTargetButton.Click += (_, _) => RemoveLastTargetPoint();
        itemButtonsPanel.Controls.Add(removeLastTargetButton);

        var clearTargetPointsButton = new Button { Text = "Listeyi Temizle", AutoSize = true };
        clearTargetPointsButton.Click += (_, _) => ClearTargetPoints();
        itemButtonsPanel.Controls.Add(clearTargetPointsButton);
        pointsGrid.Controls.Add(itemButtonsPanel, 2, 1);

        pointsGroup.Controls.Add(pointsGrid);

        var modsGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = "Kayitli Mod Listesi",
            AutoSize = true,
            Width = 970
        };

        var modsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            AutoSize = true
        };
        modsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        modsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));

        var leftModsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true
        };
        leftModsPanel.Controls.Add(new Label
        {
            Text = "Buraya ekledigin modlar kalici olarak kaydedilir ve Aranan Mod secim kutularinda gorunur.",
            AutoSize = true
        }, 0, 0);

        _savedModsListBox = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 220,
            IntegralHeight = false
        };
        leftModsPanel.Controls.Add(_savedModsListBox, 0, 1);
        modsGrid.Controls.Add(leftModsPanel, 0, 0);

        var rightModsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };
        _newSavedModTextBox = new TextBox
        {
            Width = 190
        };
        rightModsPanel.Controls.Add(_newSavedModTextBox);

        var addSavedModButton = new Button { Text = "Mod Ekle", AutoSize = true };
        addSavedModButton.Click += (_, _) => AddSavedMod();
        rightModsPanel.Controls.Add(addSavedModButton);

        var removeSavedModButton = new Button { Text = "Secileni Sil", AutoSize = true };
        removeSavedModButton.Click += (_, _) => RemoveSelectedSavedMod();
        rightModsPanel.Controls.Add(removeSavedModButton);

        var clearSavedModsButton = new Button { Text = "Listeyi Temizle", AutoSize = true };
        clearSavedModsButton.Click += (_, _) => ClearSavedMods();
        rightModsPanel.Controls.Add(clearSavedModsButton);
        modsGrid.Controls.Add(rightModsPanel, 1, 0);

        modsGroup.Controls.Add(modsGrid);

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 4),
            WrapContents = true
        };

        _startButton = new Button { Text = "Baslat (F8)", AutoSize = true };
        _startButton.Click += (_, _) => StartRun();
        actionsPanel.Controls.Add(_startButton);

        _stopButton = new Button { Text = "Durdur (F9)", AutoSize = true, Enabled = false };
        _stopButton.Click += (_, _) => StopCurrentRun("Durdurma istendi.");
        actionsPanel.Controls.Add(_stopButton);

        var clearMatchRulesButton = new Button { Text = "Temizle", AutoSize = true };
        clearMatchRulesButton.Click += (_, _) => ClearSelectedMatchRules();
        actionsPanel.Controls.Add(clearMatchRulesButton);

        var closeButton = new Button { Text = "Kapat", AutoSize = true };
        closeButton.Click += (_, _) => Close();
        actionsPanel.Controls.Add(closeButton);

        var helpLabel = new Label
        {
            Text = "F6 currency noktalarini sirayla kaydeder. F7 her basista yeni item noktasi ekler. Esc dahil fare veya klavye hareketi islemi durdurur.",
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

        ((Panel)settingsTab.Controls[0]).Controls.Add(settingsGroup);
        ((Panel)settingsTabTwo.Controls[0]).Controls.Add(settingsGroupTwo);
        ((Panel)augmentTab.Controls[0]).Controls.Add(augmentGroup);
        ((Panel)timingTab.Controls[0]).Controls.Add(timingGroup);
        ((Panel)pointsTab.Controls[0]).Controls.Add(pointsGroup);
        ((Panel)modsTab.Controls[0]).Controls.Add(modsGroup);
        tabs.TabPages.Add(settingsTab);
        tabs.TabPages.Add(settingsTabTwo);
        tabs.TabPages.Add(augmentTab);
        tabs.TabPages.Add(timingTab);
        tabs.TabPages.Add(pointsTab);
        tabs.TabPages.Add(modsTab);

        panel1Container.Controls.Add(tabs);
        panel1Container.Controls.Add(actionsPanel);

        splitContainer.Panel1.Controls.Add(panel1Container);
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

        _nextCurrencyCaptureKind = DetermineNextCurrencyCaptureKind();
        RefreshPointLabels();
        RefreshSavedModsUi();
        HookMonitorEvents();
        _monitor.Start();
        NativeMethods.AddClipboardFormatListener(Handle);
        AppendLog("Arayuz hazir.");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        StopCurrentRun("Pencere kapatildi.");
        InputController.ReleaseCommonModifiers();
        SaveUiToConfig();
        NativeMethods.RemoveClipboardFormatListener(Handle);
        _monitor.Dispose();
        base.OnFormClosed(e);
    }

    private void HookMonitorEvents()
    {
        _monitor.StartRequested += () => InvokeOnUi(StartRun);
        _monitor.StopRequested += () => InvokeOnUi(() => StopCurrentRun("Durdurma istendi."));
        _monitor.CaptureSourceRequested += () => InvokeOnUi(() => CapturePoint(PointKind.Source));
        _monitor.CaptureTargetRequested += () => InvokeOnUi(() => CapturePoint(PointKind.Target));
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

            if (_config.TargetPoints.Count == 0)
            {
                AppendLog("En az bir item noktasi eklemeden baslatamazsin.");
                return;
            }

            if (IsUnset(_config.SourcePoint))
            {
                AppendLog("Once alteration noktasini kaydetmelisin.");
                return;
            }

            if (_useAugmentCycleCheckBox.Checked && IsUnset(_config.SecondarySourcePoint))
            {
                AppendLog("Augment akisi aciksa once augment noktasini kaydetmelisin.");
                return;
            }

            if (_useAugmentCycleCheckBox.Checked && !_config.ClipboardCheck.GetActiveAugmentRules().Any())
            {
                AppendLog("Augment akisi aciksa augment sekmesinden en az bir mod secmelisin.");
                return;
            }

            var selectedRules = _config.ClipboardCheck.GetActiveRules()
                .Concat(_config.ClipboardCheck.GetActiveAugmentRules())
                .ToList();
            var hasThresholdRuleWithoutValue = selectedRules.Any(rule => rule.Text.Contains('#') && string.IsNullOrWhiteSpace(rule.ThresholdText));
            if (hasThresholdRuleWithoutValue)
            {
                AppendLog("Secili modlardan birinde '#' kullaniliyor. O modun yanindaki esik kutusunu doldurmalisin.");
                return;
            }

            var useAugmentCycle = _useAugmentCycleCheckBox.Checked
                                  && !IsUnset(_config.SecondarySourcePoint)
                                  && _config.ClipboardCheck.GetActiveAugmentRules().Any();
            _config.ClipboardCheck.UseAugmentCycle = useAugmentCycle;

            _runCancellation = new CancellationTokenSource();
            PrepareRunLogFile();
            _monitor.IsArmed = true;
            SetRunningState(true);
            AppendLog($"Baslatma istendi. Calisacak akis: {(useAugmentCycle ? "Alteration + Augment" : "Sadece Alteration")}");

            _currentRun = Task.Run(async () =>
            {
                try
                {
                    var runner = new AnalysisRunner(_config, AppendLogThreadSafe, point => InvokeOnUi(() => RemoveCompletedTargetPoint(point)));
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
        if (_isRefreshingUi)
        {
            return;
        }

        for (var i = 0; i < _config.ClipboardCheck.MatchRules.Count && i < _matchRuleComboBoxes.Count; i++)
        {
            _config.ClipboardCheck.MatchRules[i].Text = _matchRuleComboBoxes[i].SelectedItem?.ToString()?.Trim() ?? string.Empty;
            _config.ClipboardCheck.MatchRules[i].Enabled = !string.IsNullOrWhiteSpace(_config.ClipboardCheck.MatchRules[i].Text);
            _config.ClipboardCheck.MatchRules[i].ThresholdText = _matchRuleThresholdTextBoxes[i].Text.Trim();
        }

        for (var i = 0; i < _config.ClipboardCheck.AugmentMatchRules.Count && i < _augmentRuleComboBoxes.Count; i++)
        {
            _config.ClipboardCheck.AugmentMatchRules[i].Text = _augmentRuleComboBoxes[i].SelectedItem?.ToString()?.Trim() ?? string.Empty;
            _config.ClipboardCheck.AugmentMatchRules[i].Enabled = !string.IsNullOrWhiteSpace(_config.ClipboardCheck.AugmentMatchRules[i].Text);
            _config.ClipboardCheck.AugmentMatchRules[i].ThresholdText = _augmentRuleThresholdTextBoxes[i].Text.Trim();
        }

        _config.ClipboardCheck.UseAugmentCycle = _useAugmentCycleCheckBox.Checked;
        _config.MaxClicksPerItemRound = Decimal.ToInt32(_maxClicksPerItemRoundInput.Value);

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
                CaptureCurrencyPoint(x, y);
                break;
            case PointKind.Target:
                _config.TargetPoints.Add(new PointConfig { X = x, Y = y });
                AppendLog($"Item noktasi eklendi ({_config.TargetPoints.Count}. sira): {x}, {y}");
                break;
        }

        ConfigLoader.Save(_configPath, _config);
        RefreshPointLabels();
    }

    private void RemoveLastTargetPoint()
    {
        if (_config.TargetPoints.Count == 0)
        {
            AppendLog("Silinecek item noktasi yok.");
            return;
        }

        var removedPoint = _config.TargetPoints[^1];
        _config.TargetPoints.RemoveAt(_config.TargetPoints.Count - 1);
        ConfigLoader.Save(_configPath, _config);
        RefreshPointLabels();
        AppendLog($"Son item noktasi silindi: {FormatPoint(removedPoint)}");
    }

    private void ClearTargetPoints()
    {
        if (_config.TargetPoints.Count == 0)
        {
            AppendLog("Item noktasi listesi zaten bos.");
            return;
        }

        _config.TargetPoints.Clear();
        ConfigLoader.Save(_configPath, _config);
        RefreshPointLabels();
        AppendLog("Item noktasi listesi temizlendi.");
    }

    private void RefreshPointLabels()
    {
        _currencyPointsListBox.BeginUpdate();
        _currencyPointsListBox.Items.Clear();
        _currencyPointsListBox.Items.Add($"1. Alteration: {FormatPoint(_config.SourcePoint)}");
        _currencyPointsListBox.Items.Add($"2. Augment: {FormatPoint(_config.SecondarySourcePoint)}");
        _currencyPointsListBox.EndUpdate();

        _nextCurrencyCaptureLabel.Text = _nextCurrencyCaptureKind == PointKind.Source
            ? "Siradaki F6: Alteration noktasi"
            : "Siradaki F6: Augment noktasi";
        _targetLabel.Text = $"{_config.TargetPoints.Count} item noktasi secili";

        _targetPointsListBox.BeginUpdate();
        _targetPointsListBox.Items.Clear();

        for (var i = 0; i < _config.TargetPoints.Count; i++)
        {
            _targetPointsListBox.Items.Add($"{i + 1}. {FormatPoint(_config.TargetPoints[i])}");
        }

        _targetPointsListBox.EndUpdate();
    }

    private void RemoveCompletedTargetPoint(PointConfig completedPoint)
    {
        var index = _config.TargetPoints.FindIndex(point => point.X == completedPoint.X && point.Y == completedPoint.Y);
        if (index < 0)
        {
            return;
        }

        _config.TargetPoints.RemoveAt(index);
        RefreshPointLabels();
        AppendLog($"Tamamlanan item listeden kaldirildi: {FormatPoint(completedPoint)}");
    }

    private void CaptureCurrencyPoint(int x, int y)
    {
        if (_nextCurrencyCaptureKind == PointKind.Source)
        {
            _config.SourcePoint.X = x;
            _config.SourcePoint.Y = y;
            AppendLog($"Alteration noktasi kaydedildi: {x}, {y}");
            _nextCurrencyCaptureKind = PointKind.SecondarySource;
        }
        else
        {
            _config.SecondarySourcePoint.X = x;
            _config.SecondarySourcePoint.Y = y;
            AppendLog($"Augment noktasi kaydedildi: {x}, {y}");
            _nextCurrencyCaptureKind = PointKind.Source;
        }
    }

    private void ResetCurrencyPoints()
    {
        _config.SourcePoint = new PointConfig();
        _config.SecondarySourcePoint = new PointConfig();
        _nextCurrencyCaptureKind = PointKind.Source;
        ConfigLoader.Save(_configPath, _config);
        RefreshPointLabels();
        AppendLog("Currency noktalari sifirlandi.");
    }

    private void RefreshSavedModsUi()
    {
        _isRefreshingUi = true;

        try
        {
            _savedModsListBox.BeginUpdate();
            _savedModsListBox.Items.Clear();
            foreach (var savedMod in _config.SavedMods)
            {
                _savedModsListBox.Items.Add(savedMod);
            }
            _savedModsListBox.EndUpdate();

            for (var i = 0; i < _matchRuleComboBoxes.Count; i++)
            {
                var comboBox = _matchRuleComboBoxes[i];
                var selectedText = _config.ClipboardCheck.MatchRules[i].Text;
                comboBox.BeginUpdate();
                comboBox.Items.Clear();
                comboBox.Items.Add(string.Empty);
                foreach (var savedMod in _config.SavedMods)
                {
                    comboBox.Items.Add(savedMod);
                }

                var selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    for (var itemIndex = 0; itemIndex < comboBox.Items.Count; itemIndex++)
                    {
                        if (string.Equals(comboBox.Items[itemIndex]?.ToString(), selectedText, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = itemIndex;
                            break;
                        }
                    }
                }

                comboBox.SelectedIndex = selectedIndex;
                comboBox.EndUpdate();
            }

            for (var i = 0; i < _augmentRuleComboBoxes.Count; i++)
            {
                var comboBox = _augmentRuleComboBoxes[i];
                var selectedText = _config.ClipboardCheck.AugmentMatchRules[i].Text;
                comboBox.BeginUpdate();
                comboBox.Items.Clear();
                comboBox.Items.Add(string.Empty);
                foreach (var savedMod in _config.SavedMods)
                {
                    comboBox.Items.Add(savedMod);
                }

                var selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    for (var itemIndex = 0; itemIndex < comboBox.Items.Count; itemIndex++)
                    {
                        if (string.Equals(comboBox.Items[itemIndex]?.ToString(), selectedText, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = itemIndex;
                            break;
                        }
                    }
                }

                comboBox.SelectedIndex = selectedIndex;
                comboBox.EndUpdate();
            }
        }
        finally
        {
            _isRefreshingUi = false;
        }
    }

    private void AddSavedMod()
    {
        var newMod = _newSavedModTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(newMod))
        {
            AppendLog("Eklenecek mod bos olamaz.");
            return;
        }

        if (_config.SavedMods.Contains(newMod, StringComparer.OrdinalIgnoreCase))
        {
            AppendLog("Bu mod zaten kayitli.");
            return;
        }

        _config.SavedMods.Add(newMod);
        _config.Normalize();
        RefreshSavedModsUi();
        SaveUiToConfig();
        _newSavedModTextBox.Clear();
        AppendLog($"Kayitli mod eklendi: {newMod}");
    }

    private void RemoveSelectedSavedMod()
    {
        if (_savedModsListBox.SelectedItem is not string selectedMod)
        {
            AppendLog("Silmek icin listeden bir mod sec.");
            return;
        }

        _config.SavedMods.RemoveAll(mod => string.Equals(mod, selectedMod, StringComparison.OrdinalIgnoreCase));
        foreach (var rule in _config.ClipboardCheck.MatchRules.Where(rule => string.Equals(rule.Text, selectedMod, StringComparison.OrdinalIgnoreCase)))
        {
            rule.Text = string.Empty;
            rule.Enabled = false;
        }

        foreach (var rule in _config.ClipboardCheck.AugmentMatchRules.Where(rule => string.Equals(rule.Text, selectedMod, StringComparison.OrdinalIgnoreCase)))
        {
            rule.Text = string.Empty;
            rule.Enabled = false;
        }

        _config.Normalize();
        RefreshSavedModsUi();
        SaveUiToConfig();
        AppendLog($"Kayitli mod silindi: {selectedMod}");
    }

    private void ClearSavedMods()
    {
        if (_config.SavedMods.Count == 0)
        {
            AppendLog("Kayitli mod listesi zaten bos.");
            return;
        }

        _config.SavedMods.Clear();
        foreach (var rule in _config.ClipboardCheck.MatchRules)
        {
            rule.Text = string.Empty;
            rule.Enabled = false;
        }

        foreach (var rule in _config.ClipboardCheck.AugmentMatchRules)
        {
            rule.Text = string.Empty;
            rule.Enabled = false;
        }

        RefreshSavedModsUi();
        SaveUiToConfig();
        AppendLog("Kayitli mod listesi temizlendi.");
    }

    private void ClearSelectedMatchRules()
    {
        foreach (var comboBox in _matchRuleComboBoxes)
        {
            comboBox.SelectedIndex = 0;
        }

        foreach (var textBox in _matchRuleThresholdTextBoxes)
        {
            textBox.Clear();
        }

        foreach (var comboBox in _augmentRuleComboBoxes)
        {
            comboBox.SelectedIndex = 0;
        }

        foreach (var textBox in _augmentRuleThresholdTextBoxes)
        {
            textBox.Clear();
        }

        SaveUiToConfig();
        AppendLog("Aranan ve augment mod secimleri temizlendi.");
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
        var activeRules = _config.ClipboardCheck.GetActiveRules().Select(rule => rule.Text).ToList();
        var activeAugmentRules = _config.ClipboardCheck.GetActiveAugmentRules().Select(rule => rule.Text).ToList();
        var targetPointsText = _config.TargetPoints.Count == 0
            ? "Secili item noktasi yok"
            : string.Join(", ", _config.TargetPoints.Select((point, index) => $"{index + 1}. {FormatPoint(point)}"));

        var header = new StringBuilder()
            .AppendLine($"SystemAnalysis log basladi: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Mod: {_config.Mode}")
            .AppendLine($"Alteration Modlari: {(activeRules.Count == 0 ? "Aktif aranan mod yok" : string.Join(" | ", activeRules))}")
            .AppendLine($"Augment Modlari: {(activeAugmentRules.Count == 0 ? "Aktif augment modu yok" : string.Join(" | ", activeAugmentRules))}")
            .AppendLine($"Item Tur Limiti: {_config.MaxClicksPerItemRound}")
            .AppendLine($"Alteration Noktasi: {FormatPoint(_config.SourcePoint)}")
            .AppendLine($"Augment Akisi: {(_config.ClipboardCheck.UseAugmentCycle ? "Acik" : "Kapali")}")
            .AppendLine($"Augment Noktasi: {FormatPoint(_config.SecondarySourcePoint)}")
            .AppendLine($"Item Noktalari: {targetPointsText}")
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

    private void AddMatchRuleRows(TableLayoutPanel grid, int startIndex, int count)
    {
        for (var offset = 0; offset < count; offset++)
        {
            var matchRuleIndex = startIndex + offset;
            var rule = _config.ClipboardCheck.MatchRules[matchRuleIndex];
            grid.Controls.Add(new Label
            {
                Text = $"Aranan Mod {matchRuleIndex + 1}",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            }, 0, offset);

            var rulePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true
            };
            rulePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rulePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));

            var ruleComboBox = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            ruleComboBox.SelectedIndexChanged += (_, _) => SaveUiToConfig();
            _matchRuleComboBoxes.Add(ruleComboBox);
            rulePanel.Controls.Add(ruleComboBox, 0, 0);

            var thresholdTextBox = new TextBox
            {
                Dock = DockStyle.Top,
                Width = 60,
                Text = rule.ThresholdText
            };
            thresholdTextBox.TextChanged += (_, _) => SaveUiToConfig();
            _matchRuleThresholdTextBoxes.Add(thresholdTextBox);
            rulePanel.Controls.Add(thresholdTextBox, 1, 0);
            grid.Controls.Add(rulePanel, 1, offset);
        }
    }

    private void AddAugmentRuleRows(TableLayoutPanel grid, int startIndex, int count, int rowOffset)
    {
        for (var offset = 0; offset < count; offset++)
        {
            var matchRuleIndex = startIndex + offset;
            var row = rowOffset + offset;
            grid.Controls.Add(new Label
            {
                Text = $"Augment Mod {matchRuleIndex + 1}",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            }, 0, row);

            var comboBox = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBox.SelectedIndexChanged += (_, _) => SaveUiToConfig();
            _augmentRuleComboBoxes.Add(comboBox);

            var rowPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true
            };
            rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            rowPanel.Controls.Add(comboBox, 0, 0);

            var thresholdTextBox = new TextBox
            {
                Dock = DockStyle.Top,
                Width = 60,
                Text = _config.ClipboardCheck.AugmentMatchRules[matchRuleIndex].ThresholdText
            };
            thresholdTextBox.TextChanged += (_, _) => SaveUiToConfig();
            _augmentRuleThresholdTextBoxes.Add(thresholdTextBox);
            rowPanel.Controls.Add(thresholdTextBox, 1, 0);

            grid.Controls.Add(rowPanel, 1, row);
        }
    }

    private static TabPage CreateTabPage(string title)
    {
        var tab = new TabPage(title);
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(8)
        };

        tab.Controls.Add(panel);
        return tab;
    }

    private enum PointKind
    {
        Source,
        SecondarySource,
        Target
    }

    private static bool IsUnset(PointConfig point)
    {
        return point.X == 0 && point.Y == 0;
    }

    private PointKind DetermineNextCurrencyCaptureKind()
    {
        if (IsUnset(_config.SourcePoint))
        {
            return PointKind.Source;
        }

        if (IsUnset(_config.SecondarySourcePoint))
        {
            return PointKind.SecondarySource;
        }

        return PointKind.Source;
    }

    private sealed record ModeOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }
}
