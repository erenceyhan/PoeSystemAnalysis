using System.Diagnostics;
using System.Media;
using System.Text.RegularExpressions;
using SystemAnalysis.Config;
using SystemAnalysis.Interop;

namespace SystemAnalysis.Automation;

public sealed class AnalysisRunner
{
    private readonly AppConfig _config;
    private readonly Action<string>? _log;
    private readonly Action<PointConfig>? _itemCompleted;
    private readonly Random _random = new();
    private readonly List<ItemRunSummary> _allItems = new();
    private readonly Stopwatch _totalStopwatch = Stopwatch.StartNew();
    private bool _summaryLogged;

    public AnalysisRunner(AppConfig config, Action<string>? log = null, Action<PointConfig>? itemCompleted = null)
    {
        _config = config;
        _log = log;
        _itemCompleted = itemCompleted;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ItemRunSummary? currentItem = null;

        try
        {
            if (_config.TargetPoints.Count == 0)
            {
                Log("Calistirilacak item noktasi yok.");
                return;
            }

            var startDelay = NextDelay(_config.Timing.DelayBeforeStartMs);
            Log($"SystemAnalysis {startDelay} ms sonra baslayacak. Kontrolu eline alirsan otomatik durur.");
            await Task.Delay(startDelay, cancellationToken);

            _allItems.Clear();
            for (var i = 0; i < _config.TargetPoints.Count; i++)
            {
                _allItems.Add(new ItemRunSummary(i + 1, _config.TargetPoints[i]));
            }

            var pendingItems = new List<ItemRunSummary>(_allItems);
            var maxClicksPerRound = Math.Max(1, _config.MaxClicksPerItemRound);

            while (pendingItems.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                Log($"Yeni tur basladi. Bekleyen item sayisi: {pendingItems.Count}");
                var currentRoundItems = pendingItems.ToList();

                foreach (var item in currentRoundItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    currentItem = item;
                    item.ActiveStopwatch.Start();
                    Log($"Item {item.Index}/{_allItems.Count} turu basladi: {FormatPoint(item.Point)} | Bu tur limit: {maxClicksPerRound} sol tik");

                    bool completed;
                    try
                    {
                        completed = _config.ClipboardCheck.UseAugmentCycle
                            ? await RunAlterationAugmentCycleForItemAsync(item, maxClicksPerRound, cancellationToken)
                            : await RunHoldShiftSpamForItemAsync(item, maxClicksPerRound, cancellationToken);
                    }
                    finally
                    {
                        item.ActiveStopwatch.Stop();
                    }

                    if (completed)
                    {
                        item.Completed = true;
                        pendingItems.Remove(item);
                        _itemCompleted?.Invoke(item.Point);
                        LogItemSummary(item, "tamamlandi");
                    }
                    else if (!cancellationToken.IsCancellationRequested)
                    {
                        Log($"Item {item.Index}: Tur limiti doldu. Bu turde {item.LastRoundClicks} sol tik atildi, siradaki itema geciliyor.");
                    }

                    currentItem = null;
                }

                if (!cancellationToken.IsCancellationRequested && pendingItems.Count > 0)
                {
                    Log($"Tur bitti. Tamamlanmayan {pendingItems.Count} item ile basa donuluyor.");
                }
            }

            if (pendingItems.Count == 0)
            {
                Log("Secilen tum itemler tamamlandi.");
                SystemSounds.Asterisk.Play();
            }
        }
        finally
        {
            if (currentItem is not null && currentItem.ActiveStopwatch.IsRunning)
            {
                currentItem.ActiveStopwatch.Stop();
            }

            InputController.ReleaseCommonModifiers();
            LogSummary();
        }
    }

    private async Task<bool> RunHoldShiftSpamForItemAsync(ItemRunSummary item, int maxClicksThisRound, CancellationToken cancellationToken)
    {
        var shiftHeld = false;
        item.LastRoundClicks = 0;

        try
        {
            Log($"Item {item.Index}: Alteration noktasina sag tik yapiliyor.");
            RightClick(_config.SourcePoint);
            await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

            Log($"Item {item.Index}: Shift tusu basili tutuluyor.");
            InputController.KeyDown(NativeMethods.VK_SHIFT);
            shiftHeld = true;
            await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

            while (!cancellationToken.IsCancellationRequested && item.LastRoundClicks < maxClicksThisRound)
            {
                Log($"Item {item.Index}: Shift basili sol tik yapiliyor.");
                LeftClick(item.Point);
                item.AlterationUses++;
                item.TotalItemClicks++;
                item.LastRoundClicks++;
                await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");

                var inspection = await InspectAsync(item, cancellationToken);
                if (inspection.HasDesiredMod)
                {
                    Log($"Item {item.Index}: Hedef mod bulundu.");
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shiftHeld)
            {
                InputController.KeyUp(NativeMethods.VK_SHIFT);
                Log($"Item {item.Index}: Shift tusu birakildi.");
            }
        }
    }

    private async Task<bool> RunAlterationAugmentCycleForItemAsync(ItemRunSummary item, int maxClicksThisRound, CancellationToken cancellationToken)
    {
        var roundState = new RoundState();
        item.LastRoundClicks = 0;

        while (!cancellationToken.IsCancellationRequested && roundState.Clicks < maxClicksThisRound)
        {
            Log($"Item {item.Index}: Yeni alteration dongusu basliyor.");
            var alterationInspection = await ApplyAlterationUntilDesiredModAsync(item, roundState, maxClicksThisRound, cancellationToken);
            if (alterationInspection is null)
            {
                item.LastRoundClicks = roundState.Clicks;
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (roundState.Clicks >= maxClicksThisRound)
            {
                item.LastRoundClicks = roundState.Clicks;
                return false;
            }

            Log($"Item {item.Index}: Desired mod bulundu, augment asamasina geciliyor.");
            var augmentApplied = await ApplySingleAugmentAsync(item, roundState, maxClicksThisRound, cancellationToken);
            if (!augmentApplied)
            {
                item.LastRoundClicks = roundState.Clicks;
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            Log($"Item {item.Index}: Augment sonrasi kontrol basliyor.");
            var inspection = await InspectAsync(item, cancellationToken);
            var augmentRules = _config.ClipboardCheck.GetActiveAugmentRules().ToList();
            var matchedAugmentRule = augmentRules.FirstOrDefault(rule => IsRuleMatch(rule, inspection.Segments, inspection.Comparison, out _));

            if (inspection.HasDesiredMod && matchedAugmentRule is not null)
            {
                IsRuleMatch(matchedAugmentRule, inspection.Segments, inspection.Comparison, out var matchedAugmentText);
                item.LastRoundClicks = roundState.Clicks;
                Log($"Item {item.Index}: Flask tamamlandi. Alteration modu: '{inspection.MatchedRuleText}' | Augment modu: '{matchedAugmentText}'");
                return true;
            }

            var missingParts = new List<string>();
            if (!inspection.HasDesiredMod)
            {
                missingParts.Add("alteration modu");
            }

            if (matchedAugmentRule is null)
            {
                missingParts.Add("augment modu");
            }

            Log($"Item {item.Index}: Augment sonrasi flask tamamlanmadi. Eksik: {string.Join(" + ", missingParts)}. Alteration dongusu bastan basliyor.");
        }

        item.LastRoundClicks = roundState.Clicks;
        return false;
    }

    private async Task<InspectionResult?> ApplyAlterationUntilDesiredModAsync(ItemRunSummary item, RoundState roundState, int maxClicksThisRound, CancellationToken cancellationToken)
    {
        var shiftHeld = false;

        try
        {
            Log($"Item {item.Index}: Alteration noktasina sag tik yapiliyor.");
            RightClick(_config.SourcePoint);
            await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

            Log($"Item {item.Index}: Shift tusu basili tutuluyor.");
            InputController.KeyDown(NativeMethods.VK_SHIFT);
            shiftHeld = true;
            await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

            while (!cancellationToken.IsCancellationRequested && roundState.Clicks < maxClicksThisRound)
            {
                Log($"Item {item.Index}: Alteration icin Shift basili sol tik yapiliyor.");
                LeftClick(item.Point);
                item.AlterationUses++;
                item.TotalItemClicks++;
                roundState.Clicks++;
                await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");

                var inspection = await InspectAsync(item, cancellationToken);
                if (inspection.HasDesiredMod)
                {
                    Log($"Item {item.Index}: Alteration asamasi basarili. Eslesen mod: '{inspection.MatchedRuleText}'");
                    return inspection;
                }
            }

            return null;
        }
        finally
        {
            if (shiftHeld)
            {
                InputController.KeyUp(NativeMethods.VK_SHIFT);
                Log($"Item {item.Index}: Shift tusu birakildi.");
            }
        }
    }

    private async Task<bool> ApplySingleAugmentAsync(ItemRunSummary item, RoundState roundState, int maxClicksThisRound, CancellationToken cancellationToken)
    {
        if (roundState.Clicks >= maxClicksThisRound)
        {
            return false;
        }

        Log($"Item {item.Index}: Augment noktasina sag tik yapiliyor.");
        RightClick(_config.SecondarySourcePoint);
        item.AugmentUses++;
        await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

        Log($"Item {item.Index}: Item noktasina augment icin sol tik yapiliyor.");
        LeftClick(item.Point);
        item.TotalItemClicks++;
        roundState.Clicks++;
        await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");
        Log($"Item {item.Index}: Tek augment denemesi tamamlandi.");
        return true;
    }

    private async Task<InspectionResult> InspectAsync(ItemRunSummary item, CancellationToken cancellationToken)
    {
        Log($"Item {item.Index}: Kontrol icin item noktasina gidiliyor.");
        InputController.MoveMouse(item.Point.X, item.Point.Y);
        await Delay(_config.Timing.DelayBeforeInspectMs, cancellationToken, "Inspect Oncesi");

        var previousText = ClipboardHelper.GetText();
        Log($"Item {item.Index}: Kontrol kisayolu gonderiliyor.");
        TriggerClipboardShortcut();
        await Delay(_config.Timing.DelayAfterInspectShortcutMs, cancellationToken, "Kisayol Sonrasi");
        var currentText = ClipboardHelper.GetText();

        if (string.Equals(currentText, previousText, StringComparison.Ordinal))
        {
            Log($"Item {item.Index}: Clipboard degismedi, yine de icerik kontrol edildi.");
        }

        var comparison = _config.ClipboardCheck.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var segments = GetClipboardSegments(currentText);
        var rules = _config.ClipboardCheck.GetActiveRules().ToList();
        var matchedRule = rules.FirstOrDefault(rule => IsRuleMatch(rule, segments, comparison, out _));

        if (matchedRule is not null)
        {
            IsRuleMatch(matchedRule, segments, comparison, out var matchedText);
            Log($"Item {item.Index}: Eslesme bulundu: '{matchedText}'");
            return new InspectionResult(true, matchedText, segments, comparison);
        }

        var activeText = rules.Count == 0
            ? "Aktif aranan mod yok"
            : string.Join(" | ", rules.Select(FormatRuleForLog));
        Log($"Item {item.Index}: Eslesme yok. Aranan ifadeler: '{activeText}'");
        return new InspectionResult(false, string.Empty, segments, comparison);
    }

    private bool IsRuleMatch(MatchRule rule, IReadOnlyList<string> segments, StringComparison comparison, out string matchedText)
    {
        matchedText = rule.Text;

        if (string.IsNullOrWhiteSpace(rule.Text))
        {
            return false;
        }

        if (!rule.Text.Contains('#'))
        {
            var segmentMatch = segments.FirstOrDefault(segment => segment.Contains(rule.Text, comparison));
            if (segmentMatch is null)
            {
                return false;
            }

            matchedText = segmentMatch;
            return true;
        }

        if (!TryGetRuleMinimumPercentage(rule, out var minimumPercentage))
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (TryMatchThresholdRule(rule.Text, segment, minimumPercentage, comparison, out matchedText))
            {
                return true;
            }
        }

        matchedText = rule.Text;
        return false;
    }

    private static bool TryMatchThresholdRule(string template, string segment, int minimumPercentage, StringComparison comparison, out string matchedText)
    {
        matchedText = template;

        var split = template.Split('#');
        if (split.Length != 2)
        {
            if (!segment.Contains(template, comparison))
            {
                return false;
            }

            matchedText = segment;
            return true;
        }

        var prefix = split[0];
        var suffix = split[1];
        var regexPattern = $"^{Regex.Escape(prefix)}(?<value>\\d+){Regex.Escape(suffix)}$";
        var options = comparison == StringComparison.OrdinalIgnoreCase
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;
        var match = Regex.Match(segment, regexPattern, options);

        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["value"].Value, out var actualPercentage))
        {
            return false;
        }

        if (actualPercentage < minimumPercentage)
        {
            return false;
        }

        matchedText = match.Value;
        return true;
    }

    private string FormatRuleForLog(MatchRule rule)
    {
        if (rule.Text.Contains('#') && TryGetRuleMinimumPercentage(rule, out var minimumPercentage))
        {
            return $"{rule.Text} (Esik >= {minimumPercentage})";
        }

        return rule.Text;
    }

    private bool TryGetRuleMinimumPercentage(MatchRule rule, out int minimumPercentage)
    {
        minimumPercentage = 0;
        var thresholdText = string.IsNullOrWhiteSpace(rule.ThresholdText)
            ? _config.ClipboardCheck.PercentageThresholdText
            : rule.ThresholdText;

        return int.TryParse(thresholdText, out minimumPercentage);
    }

    private static IReadOnlyList<string> GetClipboardSegments(string currentText)
    {
        return currentText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment) && segment != "--------")
            .ToList();
    }

    private void TriggerClipboardShortcut()
    {
        if (string.Equals(_config.ClipboardCheck.TriggerShortcut, "ctrl+c", StringComparison.OrdinalIgnoreCase))
        {
            InputController.PressShortcut(NativeMethods.VK_CONTROL, NativeMethods.VK_C);
            return;
        }

        if (string.Equals(_config.ClipboardCheck.TriggerShortcut, "ctrl+alt+c", StringComparison.OrdinalIgnoreCase))
        {
            InputController.PressShortcut(NativeMethods.VK_CONTROL, NativeMethods.VK_MENU, NativeMethods.VK_C);
            return;
        }

        throw new NotSupportedException("Bu surum sadece ctrl+c veya ctrl+alt+c kisayolunu destekliyor.");
    }

    private static void RightClick(PointConfig point)
    {
        InputController.MoveMouse(point.X, point.Y);
        Thread.Sleep(50);
        InputController.RightClick();
    }

    private static void LeftClick(PointConfig point)
    {
        InputController.MoveMouse(point.X, point.Y);
        Thread.Sleep(50);
        InputController.LeftClick();
    }

    private Task Delay(DelayRange range, CancellationToken cancellationToken, string label)
    {
        var selectedDelay = NextDelay(range);
        Log($"{label} bekleme suresi secildi: {selectedDelay} ms");
        return Task.Delay(selectedDelay, cancellationToken);
    }

    private int NextDelay(DelayRange range)
    {
        range.Normalize();

        if (range.Min == range.Max)
        {
            return range.Min;
        }

        return _random.Next(range.Min, range.Max + 1);
    }

    private void Log(string message)
    {
        _log?.Invoke(message);
    }

    private void LogItemSummary(ItemRunSummary item, string status)
    {
        Log($"Item {item.Index} {status}. Konum: {FormatPoint(item.Point)} | Alteration: {item.AlterationUses} | Augment: {item.AugmentUses} | Toplam sol tik: {item.TotalItemClicks} | Sure: {FormatDuration(item.ActiveStopwatch.Elapsed)}");
    }

    private void LogSummary()
    {
        if (_summaryLogged)
        {
            return;
        }

        _totalStopwatch.Stop();
        var totalAlterations = _allItems.Sum(item => item.AlterationUses);
        var totalAugments = _allItems.Sum(item => item.AugmentUses);
        var totalClicks = _allItems.Sum(item => item.TotalItemClicks);
        Log("---- Ozet ----");

        foreach (var item in _allItems.OrderBy(item => item.Index))
        {
            var status = item.Completed
                ? "tamamlandi"
                : item.TotalItemClicks > 0 ? "yarida kesildi" : "baslamadi";
            Log($"Item {item.Index}: Alteration {item.AlterationUses} | Augment {item.AugmentUses} | Toplam sol tik {item.TotalItemClicks} | Sure: {FormatDuration(item.ActiveStopwatch.Elapsed)} | Durum: {status}");
        }

        Log($"Toplam item sayisi: {_allItems.Count}");
        Log($"Toplam alteration harcamasi: {totalAlterations}");
        Log($"Toplam augment harcamasi: {totalAugments}");
        Log($"Toplam currency harcamasi: {totalAlterations + totalAugments}");
        Log($"Toplam sol tik sayisi: {totalClicks}");
        Log($"Toplam calisma suresi: {FormatDuration(_totalStopwatch.Elapsed)}");
        _summaryLogged = true;
    }

    private static string FormatPoint(PointConfig point)
    {
        return $"X: {point.X}, Y: {point.Y}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return duration.ToString(@"hh\:mm\:ss");
        }

        return duration.ToString(@"mm\:ss");
    }

    private sealed class ItemRunSummary
    {
        public ItemRunSummary(int index, PointConfig point)
        {
            Index = index;
            Point = point;
        }

        public int Index { get; }
        public PointConfig Point { get; }
        public Stopwatch ActiveStopwatch { get; } = new();
        public int AlterationUses { get; set; }
        public int AugmentUses { get; set; }
        public int TotalItemClicks { get; set; }
        public int LastRoundClicks { get; set; }
        public bool Completed { get; set; }
    }

    private sealed class RoundState
    {
        public int Clicks { get; set; }
    }

    private sealed record InspectionResult(
        bool HasDesiredMod,
        string MatchedRuleText,
        IReadOnlyList<string> Segments,
        StringComparison Comparison);
}
