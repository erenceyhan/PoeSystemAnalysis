using System.Media;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.IO;
using Forms = System.Windows.Forms;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTextBox = System.Windows.Controls.TextBox;
using SystemAnalysis.Automation;
using SystemAnalysis.Config;
using SystemAnalysis.Interop;

namespace SystemAnalysis.UI;

public partial class MainWindow : Window
{
    private readonly string _configPath;
    private readonly AppConfig _config;
    private readonly GlobalInputMonitor _monitor;
    private readonly object _sync = new();
    private readonly object _fileLogSync = new();
    private readonly List<WpfComboBox> _matchRuleComboBoxes = new();
    private readonly List<WpfComboBox> _augmentRuleComboBoxes = new();
    private readonly List<WpfTextBox> _matchRuleThresholdTextBoxes = new();
    private readonly List<WpfTextBox> _augmentRuleThresholdTextBoxes = new();
    private readonly List<CraftModeOption> _craftModeOptions = new();
    private readonly List<string> _savedModOptions = new() { string.Empty };

    private CancellationTokenSource? _runCancellation;
    private Task? _currentRun;
    private string? _currentLogFilePath;
    private string _lastClipboardText = string.Empty;
    private RunStatusOverlayForm? _runStatusOverlay;
    private bool _isRefreshingUi;
    private string _runningCraftMode = ClipboardCheckConfig.SingleAlterationCraftMode;
    private bool _suppressRunLogging;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;

    public MainWindow(string configPath, AppConfig config)
    {
        _configPath = configPath;
        _config = config;
        _monitor = new GlobalInputMonitor(config.Safety);

        InitializeComponent();

        BuildRuleRows();
        PopulateCraftModes();
        LoadConfigToControls();
        RefreshSavedModsUi();
        RefreshPointLabels();
        HookMonitorEvents();
        HookUiEvents();

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;

        AppendLog("Arayuz hazir.");
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);
        NativeMethods.AddClipboardFormatListener(_windowHandle);
        _monitor.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        HideRunStatusOverlay();

        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(_windowHandle);
        }

        _monitor.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            HandleClipboardUpdate();
        }

        return IntPtr.Zero;
    }

    private void HookUiEvents()
    {
        StartButton.Click += (_, _) => StartRun();
        StopButton.Click += (_, _) => StopCurrentRun("Durdurma istendi.");
        ClearButton.Click += (_, _) => ClearSelectedMatchRules();
        CloseButton.Click += (_, _) => Close();
        ShowCopiedTextCheckBox.Checked += (_, _) => SaveUiToConfig();
        ShowCopiedTextCheckBox.Unchecked += (_, _) => SaveUiToConfig();

        CaptureAlterationPointButton.Click += (_, _) => CapturePoint(PointKind.Source);
        CaptureAugmentPointButton.Click += (_, _) => CapturePoint(PointKind.SecondarySource);
        CaptureTargetPointButton.Click += (_, _) => CapturePoint(PointKind.Target);
        RemoveAlterationPointButton.Click += (_, _) => RemoveLastAlterationPoint();
        ResetCurrencyPointsButton.Click += (_, _) => ResetCurrencyPoints();
        RemoveTargetPointButton.Click += (_, _) => RemoveLastTargetPoint();
        ClearTargetPointsButton.Click += (_, _) => ClearTargetPoints();

        AddSavedModButton.Click += (_, _) => AddSavedMod();
        RemoveSavedModButton.Click += (_, _) => RemoveSelectedSavedMod();
        ClearSavedModsButton.Click += (_, _) => ClearSavedMods();

        ChooseLogDirectoryButton.Click += (_, _) => ChooseLogDirectory();
        CraftModeComboBox.SelectionChanged += (_, _) => SaveUiToConfig();
        StashCompletedItemsCheckBox.Checked += (_, _) => SaveUiToConfig();
        StashCompletedItemsCheckBox.Unchecked += (_, _) => SaveUiToConfig();

        foreach (var textBox in GetNumericTextBoxes())
        {
            textBox.LostFocus += (_, _) => SaveUiToConfig();
        }

        LogDirectoryTextBox.LostFocus += (_, _) => SaveUiToConfig();
        NewSavedModTextBox.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                AddSavedMod();
            }
        };
    }

    private IEnumerable<WpfTextBox> GetNumericTextBoxes()
    {
        yield return MaxClicksPerItemRoundTextBox;
        yield return MaxAlterationsPerSourcePointTextBox;
        yield return DelayBeforeStartMinTextBox;
        yield return DelayBeforeStartMaxTextBox;
        yield return DelayBetweenActionsMinTextBox;
        yield return DelayBetweenActionsMaxTextBox;
        yield return DelayAfterCraftMinTextBox;
        yield return DelayAfterCraftMaxTextBox;
        yield return DelayShiftAltMinTextBox;
        yield return DelayShiftAltMaxTextBox;
        yield return DelayFlaskPressBaseTextBox;
        yield return DelayFlaskPressExtraMinTextBox;
        yield return DelayFlaskPressExtraMaxTextBox;
        yield return DelayBeforeInspectMinTextBox;
        yield return DelayBeforeInspectMaxTextBox;
        yield return DelayAfterInspectShortcutMinTextBox;
        yield return DelayAfterInspectShortcutMaxTextBox;
    }

    private void HookMonitorEvents()
    {
        _monitor.StartRequested += () => InvokeOnUi(HandleStartRequested);
        _monitor.StopRequested += () => InvokeOnUi(HandleStopRequested);
        _monitor.CaptureSourceRequested += () => InvokeOnUi(() => CapturePoint(PointKind.Source));
        _monitor.CaptureSecondarySourceRequested += () => InvokeOnUi(() => CapturePoint(PointKind.SecondarySource));
        _monitor.CaptureTargetRequested += () => InvokeOnUi(() => CapturePoint(PointKind.Target));
    }

    private void HandleStartRequested()
    {
        if (IsCurrentRunFlaskPressMode())
        {
            StopCurrentRun("Flask basma modu durduruldu.");
            return;
        }

        StartRun();
    }

    private void HandleStopRequested()
    {
        if (IsCurrentRunFlaskPressMode())
        {
            return;
        }

        StopCurrentRun("Durdurma istendi.");
    }

    private void BuildRuleRows()
    {
        AddMatchRuleRows(MatchRulesPanel1, 0, 7);
        AddMatchRuleRows(MatchRulesPanel2, 7, 8);
        AddAugmentRuleRows(AugmentRulesPanel, 0, 8);
    }

    private void PopulateCraftModes()
    {
        _craftModeOptions.Clear();
        _craftModeOptions.Add(new CraftModeOption(ClipboardCheckConfig.SingleAlterationCraftMode, "1 Mod Alteration"));
        _craftModeOptions.Add(new CraftModeOption(ClipboardCheckConfig.FlaskAugmentCraftMode, "Flask Modu"));
        _craftModeOptions.Add(new CraftModeOption(ClipboardCheckConfig.ItemAugmentCraftMode, "2 Mod Alteration + Augment"));
        _craftModeOptions.Add(new CraftModeOption(ClipboardCheckConfig.FractureClusterCraftMode, "Fracture Cluster"));
        _craftModeOptions.Add(new CraftModeOption(ClipboardCheckConfig.FlaskPressCraftMode, "Flask Basma Modu"));

        CraftModeComboBox.ItemsSource = _craftModeOptions;
        CraftModeComboBox.DisplayMemberPath = nameof(CraftModeOption.Label);
        SelectCraftMode();
    }

    private void LoadConfigToControls()
    {
        _isRefreshingUi = true;
        try
        {
            MaxClicksPerItemRoundTextBox.Text = _config.MaxClicksPerItemRound.ToString();
            MaxAlterationsPerSourcePointTextBox.Text = _config.MaxAlterationsPerSourcePoint.ToString();
            StashCompletedItemsCheckBox.IsChecked = _config.StashCompletedItems;
            LogDirectoryTextBox.Text = _config.Logging.DirectoryPath;
            ShowCopiedTextCheckBox.IsChecked = _config.Logging.ShowCopiedText;

            DelayBeforeStartMinTextBox.Text = _config.Timing.DelayBeforeStartMs.Min.ToString();
            DelayBeforeStartMaxTextBox.Text = _config.Timing.DelayBeforeStartMs.Max.ToString();
            DelayBetweenActionsMinTextBox.Text = _config.Timing.DelayBetweenActionsMs.Min.ToString();
            DelayBetweenActionsMaxTextBox.Text = _config.Timing.DelayBetweenActionsMs.Max.ToString();
            DelayAfterCraftMinTextBox.Text = _config.Timing.DelayAfterCraftMs.Min.ToString();
            DelayAfterCraftMaxTextBox.Text = _config.Timing.DelayAfterCraftMs.Max.ToString();
            DelayShiftAltMinTextBox.Text = _config.Timing.DelayShiftAltMs.Min.ToString();
            DelayShiftAltMaxTextBox.Text = _config.Timing.DelayShiftAltMs.Max.ToString();
            DelayFlaskPressBaseTextBox.Text = _config.Timing.DelayFlaskPressBaseMs.ToString();
            DelayFlaskPressExtraMinTextBox.Text = _config.Timing.DelayFlaskPressExtraMs.Min.ToString();
            DelayFlaskPressExtraMaxTextBox.Text = _config.Timing.DelayFlaskPressExtraMs.Max.ToString();
            DelayBeforeInspectMinTextBox.Text = _config.Timing.DelayBeforeInspectMs.Min.ToString();
            DelayBeforeInspectMaxTextBox.Text = _config.Timing.DelayBeforeInspectMs.Max.ToString();
            DelayAfterInspectShortcutMinTextBox.Text = _config.Timing.DelayAfterInspectShortcutMs.Min.ToString();
            DelayAfterInspectShortcutMaxTextBox.Text = _config.Timing.DelayAfterInspectShortcutMs.Max.ToString();
        }
        finally
        {
            _isRefreshingUi = false;
        }
    }

    private void AddMatchRuleRows(WpfPanel panel, int startIndex, int count)
    {
        for (var offset = 0; offset < count; offset++)
        {
            var index = startIndex + offset;
            var rule = _config.ClipboardCheck.MatchRules[index];
            panel.Children.Add(CreateRuleRow($"Aranan Mod {index + 1}", rule.Text, rule.ThresholdText, _matchRuleComboBoxes, _matchRuleThresholdTextBoxes));
        }
    }

    private void AddAugmentRuleRows(WpfPanel panel, int startIndex, int count)
    {
        for (var offset = 0; offset < count; offset++)
        {
            var index = startIndex + offset;
            var rule = _config.ClipboardCheck.AugmentMatchRules[index];
            panel.Children.Add(CreateRuleRow($"Augment Mod {index + 1}", rule.Text, rule.ThresholdText, _augmentRuleComboBoxes, _augmentRuleThresholdTextBoxes));
        }
    }

    private Grid CreateRuleRow(string labelText, string selectedText, string thresholdText, List<WpfComboBox> comboList, List<WpfTextBox> thresholdList)
    {
        var row = new Grid
        {
            Height = 34,
            MinHeight = 34,
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Top
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

        var label = new TextBlock
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        var comboBox = new WpfComboBox
        {
            ItemsSource = _savedModOptions,
            SelectedItem = string.IsNullOrWhiteSpace(selectedText) ? string.Empty : selectedText,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 300,
            Height = 26
        };
        comboBox.SelectionChanged += (_, _) =>
        {
            if (!_isRefreshingUi)
            {
                var index = comboList.IndexOf(comboBox);
                if (index >= 0 && index < thresholdList.Count)
                {
                    thresholdList[index].Clear();
                }
            }

            SaveUiToConfig();
        };
        Grid.SetColumn(comboBox, 1);
        row.Children.Add(comboBox);

        var thresholdBox = new WpfTextBox
        {
            Text = thresholdText,
            Width = 60,
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center
        };
        thresholdBox.TextChanged += (_, _) => SaveUiToConfig();
        Grid.SetColumn(thresholdBox, 2);
        row.Children.Add(thresholdBox);

        comboList.Add(comboBox);
        thresholdList.Add(thresholdBox);
        return row;
    }

    private void RefreshPointLabels()
    {
        AlterationPointsListBox.Items.Clear();
        if (_config.AlterationPoints.Count == 0)
        {
            AlterationPointsListBox.Items.Add("Alteration noktasi secili degil");
        }
        else
        {
            for (var i = 0; i < _config.AlterationPoints.Count; i++)
            {
                AlterationPointsListBox.Items.Add($"{i + 1}. Alteration: {FormatPoint(_config.AlterationPoints[i])}");
            }
        }

        AugmentPointTextBlock.Text = $"Augment: {FormatPoint(_config.SecondarySourcePoint)}";
        TargetCountTextBlock.Text = $"{_config.TargetPoints.Count} item noktasi secili";

        TargetPointsListBox.Items.Clear();
        for (var i = 0; i < _config.TargetPoints.Count; i++)
        {
            TargetPointsListBox.Items.Add($"{i + 1}. {FormatPoint(_config.TargetPoints[i])}");
        }
    }

    private void RefreshSavedModsUi()
    {
        _isRefreshingUi = true;
        try
        {
            SavedModsListBox.Items.Clear();
            foreach (var savedMod in _config.SavedMods)
            {
                SavedModsListBox.Items.Add(savedMod);
            }

            _savedModOptions.Clear();
            _savedModOptions.Add(string.Empty);
            foreach (var savedMod in _config.SavedMods)
            {
                _savedModOptions.Add(savedMod);
            }

            for (var i = 0; i < _matchRuleComboBoxes.Count; i++)
            {
                _matchRuleComboBoxes[i].ItemsSource = null;
                _matchRuleComboBoxes[i].ItemsSource = _savedModOptions;
                _matchRuleComboBoxes[i].SelectedItem = string.IsNullOrWhiteSpace(_config.ClipboardCheck.MatchRules[i].Text)
                    ? string.Empty
                    : _config.ClipboardCheck.MatchRules[i].Text;
                _matchRuleThresholdTextBoxes[i].Text = _config.ClipboardCheck.MatchRules[i].ThresholdText;
            }

            for (var i = 0; i < _augmentRuleComboBoxes.Count; i++)
            {
                _augmentRuleComboBoxes[i].ItemsSource = null;
                _augmentRuleComboBoxes[i].ItemsSource = _savedModOptions;
                _augmentRuleComboBoxes[i].SelectedItem = string.IsNullOrWhiteSpace(_config.ClipboardCheck.AugmentMatchRules[i].Text)
                    ? string.Empty
                    : _config.ClipboardCheck.AugmentMatchRules[i].Text;
                _augmentRuleThresholdTextBoxes[i].Text = _config.ClipboardCheck.AugmentMatchRules[i].ThresholdText;
            }
        }
        finally
        {
            _isRefreshingUi = false;
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

        _config.ClipboardCheck.CraftMode = GetSelectedCraftMode();
        _config.ClipboardCheck.Normalize();
        _config.MaxClicksPerItemRound = ParsePositiveInt(MaxClicksPerItemRoundTextBox.Text, _config.MaxClicksPerItemRound);
        _config.MaxAlterationsPerSourcePoint = ParsePositiveInt(MaxAlterationsPerSourcePointTextBox.Text, _config.MaxAlterationsPerSourcePoint);
        _config.StashCompletedItems = StashCompletedItemsCheckBox.IsChecked == true;

        var firstActiveRule = _config.ClipboardCheck.GetActiveRules().FirstOrDefault();
        _config.ClipboardCheck.MustContain = firstActiveRule?.Text ?? string.Empty;
        _config.Mode = "hold_shift_spam";

        _config.Timing.DelayBeforeStartMs = CreateRange(DelayBeforeStartMinTextBox, DelayBeforeStartMaxTextBox, _config.Timing.DelayBeforeStartMs);
        _config.Timing.DelayBetweenActionsMs = CreateRange(DelayBetweenActionsMinTextBox, DelayBetweenActionsMaxTextBox, _config.Timing.DelayBetweenActionsMs);
        _config.Timing.DelayAfterCraftMs = CreateRange(DelayAfterCraftMinTextBox, DelayAfterCraftMaxTextBox, _config.Timing.DelayAfterCraftMs);
        _config.Timing.DelayShiftAltMs = CreateRange(DelayShiftAltMinTextBox, DelayShiftAltMaxTextBox, _config.Timing.DelayShiftAltMs);
        _config.Timing.DelayFlaskPressBaseMs = ParseNonNegativeInt(DelayFlaskPressBaseTextBox.Text, _config.Timing.DelayFlaskPressBaseMs);
        _config.Timing.DelayFlaskPressExtraMs = CreateRange(DelayFlaskPressExtraMinTextBox, DelayFlaskPressExtraMaxTextBox, _config.Timing.DelayFlaskPressExtraMs);
        _config.Timing.DelayBeforeInspectMs = CreateRange(DelayBeforeInspectMinTextBox, DelayBeforeInspectMaxTextBox, _config.Timing.DelayBeforeInspectMs);
        _config.Timing.DelayAfterInspectShortcutMs = CreateRange(DelayAfterInspectShortcutMinTextBox, DelayAfterInspectShortcutMaxTextBox, _config.Timing.DelayAfterInspectShortcutMs);
        _config.Logging.DirectoryPath = LogDirectoryTextBox.Text.Trim();
        _config.Logging.ShowCopiedText = ShowCopiedTextCheckBox.IsChecked == true;

        ConfigLoader.Save(_configPath, _config);
    }

    private void CapturePoint(PointKind pointKind)
    {
        var (x, y) = InputController.GetMousePosition();

        switch (pointKind)
        {
            case PointKind.Source:
                _config.AlterationPoints.Add(new PointConfig { X = x, Y = y });
                AppendLog($"Alteration noktasi eklendi ({_config.AlterationPoints.Count}. sira): {x}, {y}");
                break;
            case PointKind.SecondarySource:
                _config.SecondarySourcePoint.X = x;
                _config.SecondarySourcePoint.Y = y;
                AppendLog($"Augment noktasi kaydedildi: {x}, {y}");
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

    private void RemoveLastAlterationPoint()
    {
        if (_config.AlterationPoints.Count == 0)
        {
            AppendLog("Silinecek alteration noktasi yok.");
            return;
        }

        var removedPoint = _config.AlterationPoints[^1];
        _config.AlterationPoints.RemoveAt(_config.AlterationPoints.Count - 1);
        ConfigLoader.Save(_configPath, _config);
        RefreshPointLabels();
        AppendLog($"Son alteration noktasi silindi: {FormatPoint(removedPoint)}");
    }

    private void ResetCurrencyPoints()
    {
        _config.AlterationPoints.Clear();
        _config.SourcePoint = new PointConfig();
        _config.SecondarySourcePoint = new PointConfig();
        ConfigLoader.Save(_configPath, _config);
        RefreshPointLabels();
        AppendLog("Currency noktalari sifirlandi.");
    }

    private void AddSavedMod()
    {
        var newMod = NewSavedModTextBox.Text.Trim();
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
        NewSavedModTextBox.Clear();
        AppendLog($"Kayitli mod eklendi: {newMod}");
    }

    private void RemoveSelectedSavedMod()
    {
        if (SavedModsListBox.SelectedItem is not string selectedMod)
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
            rule.ThresholdText = string.Empty;
        }

        foreach (var rule in _config.ClipboardCheck.AugmentMatchRules)
        {
            rule.Text = string.Empty;
            rule.Enabled = false;
            rule.ThresholdText = string.Empty;
        }

        RefreshSavedModsUi();
        SaveUiToConfig();
        AppendLog("Kayitli mod listesi temizlendi.");
    }

    private void ClearSelectedMatchRules()
    {
        foreach (var comboBox in _matchRuleComboBoxes)
        {
            comboBox.SelectedItem = string.Empty;
        }

        foreach (var textBox in _matchRuleThresholdTextBoxes)
        {
            textBox.Clear();
        }

        foreach (var comboBox in _augmentRuleComboBoxes)
        {
            comboBox.SelectedItem = string.Empty;
        }

        foreach (var textBox in _augmentRuleThresholdTextBoxes)
        {
            textBox.Clear();
        }

        SaveUiToConfig();
        AppendLog("Aranan ve augment mod secimleri temizlendi.");
    }

    private void StartRun()
    {
        lock (_sync)
        {
            if (_currentRun is { IsCompleted: false })
            {
                if (IsCurrentRunFlaskPressMode())
                {
                    StopCurrentRun("Flask basma modu durduruldu.");
                    return;
                }

                AppendLog("Islem zaten calisiyor.");
                return;
            }

            SaveUiToConfig();

            var selectedCraftMode = GetSelectedCraftMode();
            var useFlaskPressMode = string.Equals(selectedCraftMode, ClipboardCheckConfig.FlaskPressCraftMode, StringComparison.OrdinalIgnoreCase);

            if (!useFlaskPressMode)
            {
                if (_config.TargetPoints.Count == 0)
                {
                    SystemSounds.Exclamation.Play();
                    AppendLog("En az bir item noktasi eklemeden baslatamazsin.");
                    return;
                }

                if (_config.AlterationPoints.Count == 0)
                {
                    SystemSounds.Exclamation.Play();
                    AppendLog("Once en az bir alteration noktasi kaydetmelisin.");
                    return;
                }

                var useFlaskAugmentCycle = string.Equals(selectedCraftMode, ClipboardCheckConfig.FlaskAugmentCraftMode, StringComparison.OrdinalIgnoreCase);
                var useItemAugmentCycle = string.Equals(selectedCraftMode, ClipboardCheckConfig.ItemAugmentCraftMode, StringComparison.OrdinalIgnoreCase);

                if ((useFlaskAugmentCycle || useItemAugmentCycle) && IsUnset(_config.SecondarySourcePoint))
                {
                    SystemSounds.Exclamation.Play();
                    AppendLog("Augment akisi aciksa once augment noktasini kaydetmelisin.");
                    return;
                }

                if ((useFlaskAugmentCycle || useItemAugmentCycle) && !_config.ClipboardCheck.GetActiveAugmentRules().Any())
                {
                    SystemSounds.Exclamation.Play();
                    AppendLog("Augment akisi aciksa augment sekmesinden en az bir mod secmelisin.");
                    return;
                }

                if (useItemAugmentCycle && !_config.ClipboardCheck.GetActiveRules().Any())
                {
                    SystemSounds.Exclamation.Play();
                    AppendLog("Item augment akisi aciksa craft ayarlarindan en az bir mod secmelisin.");
                    return;
                }

                if (useItemAugmentCycle)
                {
                    var distinctSelectedMods = _config.ClipboardCheck.GetActiveRules()
                        .Concat(_config.ClipboardCheck.GetActiveAugmentRules())
                        .Select(rule => $"{rule.Text.Trim()}|{rule.ThresholdText.Trim()}")
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    if (distinctSelectedMods < 2)
                    {
                        SystemSounds.Exclamation.Play();
                        AppendLog("2 Mod Alteration + Augment icin toplamda en az iki farkli hedef mod secmelisin.");
                        return;
                    }
                }

                if (HasThresholdRuleWithoutValue())
                {
                    SystemSounds.Exclamation.Play();
                    AppendLog("Secili modlardan birinde '#' kullaniliyor. O modun yanindaki esik kutusunu doldurmalisin.");
                    return;
                }
            }

            var useFlaskAugmentCycleFinal = string.Equals(selectedCraftMode, ClipboardCheckConfig.FlaskAugmentCraftMode, StringComparison.OrdinalIgnoreCase);
            var useItemAugmentCycleFinal = string.Equals(selectedCraftMode, ClipboardCheckConfig.ItemAugmentCraftMode, StringComparison.OrdinalIgnoreCase);
            var useFractureClusterCycle = string.Equals(selectedCraftMode, ClipboardCheckConfig.FractureClusterCraftMode, StringComparison.OrdinalIgnoreCase);
            var useAugmentCycle = useFlaskAugmentCycleFinal
                                  && !IsUnset(_config.SecondarySourcePoint)
                                  && _config.ClipboardCheck.GetActiveAugmentRules().Any();
            var useItemAugment = useItemAugmentCycleFinal
                                 && !IsUnset(_config.SecondarySourcePoint)
                                 && _config.ClipboardCheck.GetActiveAugmentRules().Any()
                                 && _config.ClipboardCheck.GetActiveRules().Any();
            _config.ClipboardCheck.CraftMode = selectedCraftMode;
            _config.ClipboardCheck.Normalize();

            _runCancellation = new CancellationTokenSource();
            _runningCraftMode = selectedCraftMode;
            _suppressRunLogging = useFlaskPressMode;
            _currentLogFilePath = null;

            if (!useFlaskPressMode)
            {
                PrepareRunLogFile();
            }

            _monitor.IsArmed = !useFlaskPressMode;
            SetRunningState(true);

            if (!useFlaskPressMode)
            {
                var flowText = useItemAugment
                    ? "Item icin Mod + Augment"
                    : useAugmentCycle
                        ? "Alteration + Augment"
                        : useFractureClusterCycle
                            ? "Fracture Cluster"
                            : "Sadece Alteration";
                AppendLog($"Baslatma istendi. Calisacak akis: {flowText}");
                ShowRunStatusOverlay();
                UpdateRunStatusOverlay(new RunProgress(0, 0, 0));
            }
            else
            {
                HideRunStatusOverlay();
            }

            _currentRun = Task.Run(async () =>
            {
                try
                {
                    if (useFlaskPressMode)
                    {
                        var flaskRunner = new FlaskPressRunner(_config);
                        await flaskRunner.RunAsync(_runCancellation.Token);
                    }
                    else
                    {
                        var runner = new AnalysisRunner(
                            _config,
                            AppendLogThreadSafe,
                            point => InvokeOnUi(() => RemoveCompletedTargetPoint(point)),
                            progress => InvokeOnUi(() => UpdateRunStatusOverlay(progress)));
                        await runner.RunAsync(_runCancellation.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    if (!_suppressRunLogging)
                    {
                        AppendLogThreadSafe("Islem guvenli sekilde durduruldu.");
                    }
                }
                catch (Exception ex)
                {
                    if (!_suppressRunLogging)
                    {
                        AppendLogThreadSafe($"Hata: {ex.Message}");
                    }
                }
                finally
                {
                    lock (_sync)
                    {
                        _monitor.IsArmed = false;
                        _runCancellation?.Dispose();
                        _runCancellation = null;
                        _runningCraftMode = ClipboardCheckConfig.SingleAlterationCraftMode;
                    }

                    InvokeOnUi(() =>
                    {
                        SetRunningState(false);
                        HideRunStatusOverlay();
                    });

                    _suppressRunLogging = false;
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

    private void SetRunningState(bool isRunning)
    {
        StartButton.IsEnabled = !isRunning;
        StopButton.IsEnabled = isRunning;
    }

    private void SelectCraftMode()
    {
        var selected = _craftModeOptions.FirstOrDefault(option =>
            string.Equals(option.Value, _config.ClipboardCheck.CraftMode, StringComparison.OrdinalIgnoreCase));

        CraftModeComboBox.SelectedItem = selected ?? _craftModeOptions.FirstOrDefault();
    }

    private string GetSelectedCraftMode()
    {
        return (CraftModeComboBox.SelectedItem as CraftModeOption)?.Value
               ?? ClipboardCheckConfig.SingleAlterationCraftMode;
    }

    private void AppendLogThreadSafe(string message)
    {
        InvokeOnUi(() => AppendLog(message));
    }

    private void AppendLog(string message)
    {
        if (_suppressRunLogging)
        {
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        LogTextBox.AppendText(line);
        LogTextBox.ScrollToEnd();
        WriteLogToFile(line);
    }

    private void ChooseLogDirectory()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Log dosyalarinin kaydedilecegi klasoru sec",
            InitialDirectory = Directory.Exists(LogDirectoryTextBox.Text) ? LogDirectoryTextBox.Text : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            LogDirectoryTextBox.Text = dialog.SelectedPath;
            SaveUiToConfig();
            AppendLog($"Log klasoru guncellendi: {dialog.SelectedPath}");
        }
    }

    private void PrepareRunLogFile()
    {
        var directory = LogDirectoryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(AppContext.BaseDirectory, "logs");
            LogDirectoryTextBox.Text = directory;
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
            .AppendLine($"Alteration Nokta Limiti: {_config.MaxAlterationsPerSourcePoint}")
            .AppendLine($"Alteration Noktalari: {(_config.AlterationPoints.Count == 0 ? "Secili alteration noktasi yok" : string.Join(" | ", _config.AlterationPoints.Select((point, index) => $"{index + 1}. {FormatPoint(point)}")))}")
            .AppendLine($"Craft Modu: {GetAugmentFlowDescription()}")
            .AppendLine($"Tamamlanan Item Stashe Gonder: {(_config.StashCompletedItems ? "Acik" : "Kapali")}")
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

    private string GetAugmentFlowDescription()
    {
        if (string.Equals(_config.ClipboardCheck.CraftMode, ClipboardCheckConfig.FlaskPressCraftMode, StringComparison.OrdinalIgnoreCase))
        {
            return "Flask Basma Modu";
        }

        if (string.Equals(_config.ClipboardCheck.CraftMode, ClipboardCheckConfig.ItemAugmentCraftMode, StringComparison.OrdinalIgnoreCase))
        {
            return "2 Mod Alteration + Augment";
        }

        if (string.Equals(_config.ClipboardCheck.CraftMode, ClipboardCheckConfig.FractureClusterCraftMode, StringComparison.OrdinalIgnoreCase))
        {
            return "Fracture Cluster";
        }

        if (string.Equals(_config.ClipboardCheck.CraftMode, ClipboardCheckConfig.FlaskAugmentCraftMode, StringComparison.OrdinalIgnoreCase))
        {
            return "Flask Modu";
        }

        return "1 Mod Alteration";
    }

    private void HandleClipboardUpdate()
    {
        if (ShowCopiedTextCheckBox.IsChecked != true)
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
        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.BeginInvoke(action);
    }

    private void ShowRunStatusOverlay()
    {
        if (_runStatusOverlay is null || _runStatusOverlay.IsDisposed)
        {
            _runStatusOverlay = new RunStatusOverlayForm();
        }

        _runStatusOverlay.PositionOnScreen();
        _runStatusOverlay.UpdateProgress(0, 0, 0);

        if (!_runStatusOverlay.Visible)
        {
            _runStatusOverlay.Show();
        }
    }

    private void HideRunStatusOverlay()
    {
        if (_runStatusOverlay is null || _runStatusOverlay.IsDisposed)
        {
            return;
        }

        _runStatusOverlay.Hide();
    }

    private void UpdateRunStatusOverlay(RunProgress progress)
    {
        if (_runStatusOverlay is null || _runStatusOverlay.IsDisposed)
        {
            return;
        }

        _runStatusOverlay.UpdateProgress(progress.TotalClicks, progress.CompletedItems, progress.StuckItems);
    }

    private bool IsCurrentRunFlaskPressMode()
    {
        lock (_sync)
        {
            return _currentRun is { IsCompleted: false }
                   && string.Equals(_runningCraftMode, ClipboardCheckConfig.FlaskPressCraftMode, StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool HasThresholdRuleWithoutValue()
    {
        for (var i = 0; i < _matchRuleComboBoxes.Count && i < _matchRuleThresholdTextBoxes.Count; i++)
        {
            var selectedText = _matchRuleComboBoxes[i].SelectedItem?.ToString()?.Trim() ?? string.Empty;
            if (selectedText.Contains('#') && string.IsNullOrWhiteSpace(_matchRuleThresholdTextBoxes[i].Text))
            {
                return true;
            }
        }

        for (var i = 0; i < _augmentRuleComboBoxes.Count && i < _augmentRuleThresholdTextBoxes.Count; i++)
        {
            var selectedText = _augmentRuleComboBoxes[i].SelectedItem?.ToString()?.Trim() ?? string.Empty;
            if (selectedText.Contains('#') && string.IsNullOrWhiteSpace(_augmentRuleThresholdTextBoxes[i].Text))
            {
                return true;
            }
        }

        return false;
    }

    private static DelayRange CreateRange(WpfTextBox minTextBox, WpfTextBox maxTextBox, DelayRange fallback)
    {
        var min = ParseNonNegativeInt(minTextBox.Text, fallback.Min);
        var max = ParseNonNegativeInt(maxTextBox.Text, fallback.Max);
        return new DelayRange(min, max);
    }

    private static int ParsePositiveInt(string? text, int fallback)
    {
        return int.TryParse(text, out var value) ? Math.Max(1, value) : Math.Max(1, fallback);
    }

    private static int ParseNonNegativeInt(string? text, int fallback)
    {
        return int.TryParse(text, out var value) ? Math.Max(0, value) : Math.Max(0, fallback);
    }

    private static string FormatPoint(PointConfig point)
    {
        return $"X: {point.X}, Y: {point.Y}";
    }

    private static bool IsUnset(PointConfig point)
    {
        return point.X == 0 && point.Y == 0;
    }

    private sealed record CraftModeOption(string Value, string Label);

    private enum PointKind
    {
        Source,
        SecondarySource,
        Target
    }
}
