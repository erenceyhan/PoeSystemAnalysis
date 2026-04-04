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
    private readonly Action<RunProgress>? _progressChanged;
    private readonly Random _random = new();
    private readonly List<ItemRunSummary> _allItems = new();
    private readonly List<AlterationPointState> _alterationPoints = new();
    private readonly Stopwatch _totalStopwatch = Stopwatch.StartNew();
    private bool _summaryLogged;
    private bool _alterationSourcesExhausted;
    private const int SameTextStashThreshold = 3;
    private const int ConsecutiveStuckItemsBeforeSourceSwitch = 2;

    public AnalysisRunner(AppConfig config, Action<string>? log = null, Action<PointConfig>? itemCompleted = null, Action<RunProgress>? progressChanged = null)
    {
        _config = config;
        _log = log;
        _itemCompleted = itemCompleted;
        _progressChanged = progressChanged;
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
            _alterationPoints.Clear();
            for (var i = 0; i < _config.AlterationPoints.Count; i++)
            {
                _alterationPoints.Add(new AlterationPointState(i + 1, _config.AlterationPoints[i]));
            }
            ReportProgress();

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
                    item.LastStuckDuringAlteration = false;
                    item.LastAlterationPointIndex = null;
                    item.ActiveStopwatch.Start();
                    Log($"Item {item.Index}/{_allItems.Count} turu basladi: {FormatPoint(item.Point)} | Bu tur limit: {maxClicksPerRound} sol tik");

                    ItemRunOutcome outcome;
                    try
                    {
                        outcome = string.Equals(_config.ClipboardCheck.CraftMode, ClipboardCheckConfig.FractureClusterCraftMode, StringComparison.OrdinalIgnoreCase)
                            ? await RunFractureClusterForItemAsync(item, maxClicksPerRound, cancellationToken)
                            : _config.ClipboardCheck.UseItemAugmentCycle
                            ? await RunItemAugmentCycleForItemAsync(item, maxClicksPerRound, cancellationToken)
                            : _config.ClipboardCheck.UseAugmentCycle
                                ? await RunAlterationAugmentCycleForItemAsync(item, maxClicksPerRound, cancellationToken)
                                : await RunHoldShiftSpamForItemAsync(item, maxClicksPerRound, cancellationToken);
                    }
                    finally
                    {
                        item.ActiveStopwatch.Stop();
                    }

                    UpdateAlterationPointStatusAfterItem(item, outcome);

                    if (outcome == ItemRunOutcome.Completed)
                    {
                        item.Completed = true;
                        if (_config.StashCompletedItems)
                        {
                            await SendCompletedItemToStashAsync(item, cancellationToken);
                        }
                        else
                        {
                            Log($"Item {item.Index}: Tamamlandi. Stash gonderimi kapali oldugu icin item yerinde birakiliyor.");
                        }
                        pendingItems.Remove(item);
                        _itemCompleted?.Invoke(item.Point);
                        ReportProgress();
                        LogItemSummary(item, "tamamlandi");
                    }
                    else if (outcome == ItemRunOutcome.StashedAsStuck)
                    {
                        item.StashedAsStuck = true;
                        await SendStuckItemToStashAsync(item, cancellationToken);
                        pendingItems.Remove(item);
                        _itemCompleted?.Invoke(item.Point);
                        ReportProgress();
                        LogItemSummary(item, "takildi, stashe gonderildi");
                    }
                    else if (!cancellationToken.IsCancellationRequested)
                    {
                        Log($"Item {item.Index}: Tur limiti doldu. Bu turde {item.LastRoundClicks} sol tik atildi, siradaki itema geciliyor.");
                    }

                    currentItem = null;

                    if (_alterationSourcesExhausted)
                    {
                        Log("Tum alteration noktalarinin limiti doldu. Islem durduruluyor.");
                        return;
                    }
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

    private async Task<ItemRunOutcome> RunHoldShiftSpamForItemAsync(ItemRunSummary item, int maxClicksThisRound, CancellationToken cancellationToken)
    {
        item.LastRoundClicks = 0;

        while (!cancellationToken.IsCancellationRequested && item.LastRoundClicks < maxClicksThisRound)
        {
            var alterationPoint = GetAvailableAlterationPoint(item.Index);
            if (alterationPoint is null)
            {
                return ItemRunOutcome.ContinueNextRound;
            }

            item.LastAlterationPointIndex = alterationPoint.Index;

            var shiftHeld = false;
            try
            {
                Log($"Item {item.Index}: Alteration noktasi {alterationPoint.Index} secildi, sag tik yapiliyor.");
                RightClick(alterationPoint.Point);
                await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

                Log($"Item {item.Index}: Shift tusu basili tutuluyor.");
                InputController.KeyDown(NativeMethods.VK_SHIFT);
                shiftHeld = true;
                await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

                while (!cancellationToken.IsCancellationRequested && item.LastRoundClicks < maxClicksThisRound)
                {
                    if (alterationPoint.Uses >= _config.MaxAlterationsPerSourcePoint)
                    {
                        Log($"Item {item.Index}: Alteration noktasi {alterationPoint.Index} limitine ulasti. Yeni noktaya gecilecek.");
                        break;
                    }

                    Log($"Item {item.Index}: Shift basili sol tik yapiliyor.");
                    LeftClick(item.Point);
                    item.AlterationUses++;
                    item.TotalItemClicks++;
                    item.LastRoundClicks++;
                    alterationPoint.Uses++;
                    ReportProgress();
                    await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");

                    var inspection = await InspectAsync(item, cancellationToken);
                    if (inspection.IsStuck)
                    {
                        item.LastStuckDuringAlteration = true;
                        return ItemRunOutcome.StashedAsStuck;
                    }

                    if (inspection.HasDesiredMod)
                    {
                        Log($"Item {item.Index}: Hedef mod bulundu.");
                        return ItemRunOutcome.Completed;
                    }
                }
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

        return ItemRunOutcome.ContinueNextRound;
    }

    private async Task<ItemRunOutcome> RunFractureClusterForItemAsync(ItemRunSummary item, int maxClicksThisRound, CancellationToken cancellationToken)
    {
        item.LastRoundClicks = 0;

        while (!cancellationToken.IsCancellationRequested && item.LastRoundClicks < maxClicksThisRound)
        {
            var alterationPoint = GetAvailableAlterationPoint(item.Index);
            if (alterationPoint is null)
            {
                return ItemRunOutcome.ContinueNextRound;
            }

            item.LastAlterationPointIndex = alterationPoint.Index;

            var shiftHeld = false;
            try
            {
                Log($"Item {item.Index}: Fracture Cluster icin alteration noktasi {alterationPoint.Index} secildi, sag tik yapiliyor.");
                RightClick(alterationPoint.Point);
                await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

                Log($"Item {item.Index}: Shift tusu basili tutuluyor.");
                InputController.KeyDown(NativeMethods.VK_SHIFT);
                shiftHeld = true;
                await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

                while (!cancellationToken.IsCancellationRequested && item.LastRoundClicks < maxClicksThisRound)
                {
                    if (alterationPoint.Uses >= _config.MaxAlterationsPerSourcePoint)
                    {
                        Log($"Item {item.Index}: Alteration noktasi {alterationPoint.Index} limitine ulasti. Yeni noktaya gecilecek.");
                        break;
                    }

                    Log($"Item {item.Index}: Fracture Cluster icin Shift basili sol tik yapiliyor.");
                    LeftClick(item.Point);
                    item.AlterationUses++;
                    item.TotalItemClicks++;
                    item.LastRoundClicks++;
                    alterationPoint.Uses++;
                    ReportProgress();
                    await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");

                    Log($"Item {item.Index}: Shift basiliyken Alt tusu basiliyor.");
                    InputController.KeyDown(NativeMethods.VK_MENU);

                    try
                    {
                        await Delay(_config.Timing.DelayShiftAltMs, cancellationToken, "Shift+Alt");
                        Log($"Item {item.Index}: Shift+Alt basili ikinci sol tik yapiliyor.");
                        LeftClick(item.Point);
                    }
                    finally
                    {
                        InputController.KeyUp(NativeMethods.VK_MENU);
                        Log($"Item {item.Index}: Alt tusu birakildi.");
                    }

                    item.TotalItemClicks++;
                    ReportProgress();
                    await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

                    var inspection = await InspectAsync(item, cancellationToken);
                    if (inspection.IsStuck)
                    {
                        item.LastStuckDuringAlteration = true;
                        return ItemRunOutcome.StashedAsStuck;
                    }

                    if (inspection.HasDesiredMod)
                    {
                        Log($"Item {item.Index}: Fracture Cluster hedef modu bulundu.");
                        return ItemRunOutcome.Completed;
                    }
                }
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

        return ItemRunOutcome.ContinueNextRound;
    }

    private async Task<ItemRunOutcome> RunAlterationAugmentCycleForItemAsync(ItemRunSummary item, int maxClicksThisRound, CancellationToken cancellationToken)
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
                return ItemRunOutcome.ContinueNextRound;
            }

            if (alterationInspection.IsStuck)
            {
                item.LastRoundClicks = roundState.Clicks;
                return ItemRunOutcome.StashedAsStuck;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (roundState.Clicks >= maxClicksThisRound)
            {
                item.LastRoundClicks = roundState.Clicks;
                return ItemRunOutcome.ContinueNextRound;
            }

            Log($"Item {item.Index}: Desired mod bulundu, augment asamasina geciliyor.");
            var augmentApplied = await ApplySingleAugmentAsync(item, roundState, maxClicksThisRound, cancellationToken);
            if (!augmentApplied)
            {
                item.LastRoundClicks = roundState.Clicks;
                return ItemRunOutcome.ContinueNextRound;
            }

            cancellationToken.ThrowIfCancellationRequested();

            Log($"Item {item.Index}: Augment sonrasi kontrol basliyor.");
            var inspection = await InspectAsync(item, cancellationToken);
            if (inspection.IsStuck)
            {
                item.LastStuckDuringAlteration = false;
                item.LastRoundClicks = roundState.Clicks;
                return ItemRunOutcome.StashedAsStuck;
            }

            var augmentRules = _config.ClipboardCheck.GetActiveAugmentRules().ToList();
            var matchedAugmentRule = augmentRules.FirstOrDefault(rule => IsRuleMatch(rule, inspection.Segments, inspection.Comparison, out _));

            if (inspection.HasDesiredMod && matchedAugmentRule is not null)
            {
                IsRuleMatch(matchedAugmentRule, inspection.Segments, inspection.Comparison, out var matchedAugmentText);
                item.LastRoundClicks = roundState.Clicks;
                Log($"Item {item.Index}: Flask tamamlandi. Alteration modu: '{inspection.MatchedRuleText}' | Augment modu: '{matchedAugmentText}'");
                return ItemRunOutcome.Completed;
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
        return ItemRunOutcome.ContinueNextRound;
    }

    private async Task<ItemRunOutcome> RunItemAugmentCycleForItemAsync(ItemRunSummary item, int maxClicksThisRound, CancellationToken cancellationToken)
    {
        var roundState = new RoundState();
        var craftRules = _config.ClipboardCheck.GetActiveRules().ToList();
        var augmentRules = _config.ClipboardCheck.GetActiveAugmentRules().ToList();
        var combinedRules = craftRules.Concat(augmentRules).ToList();
        item.LastRoundClicks = 0;

        while (!cancellationToken.IsCancellationRequested && roundState.Clicks < maxClicksThisRound)
        {
            Log($"Item {item.Index}: Yeni item augment dongusu basliyor.");
            var initialInspection = await ApplyAlterationUntilMatchAsync(
                item,
                roundState,
                maxClicksThisRound,
                combinedRules,
                "Item augment oncesi ilk mod bulundu",
                cancellationToken);
            if (initialInspection is null)
            {
                item.LastRoundClicks = roundState.Clicks;
                return ItemRunOutcome.ContinueNextRound;
            }

            if (initialInspection.IsStuck)
            {
                item.LastRoundClicks = roundState.Clicks;
                return ItemRunOutcome.StashedAsStuck;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (roundState.Clicks >= maxClicksThisRound)
            {
                item.LastRoundClicks = roundState.Clicks;
                return ItemRunOutcome.ContinueNextRound;
            }

            Log($"Item {item.Index}: Ilk mod bulundu ('{initialInspection.MatchedRuleText}'), item augment asamasina geciliyor.");
            var augmentApplied = await ApplySingleAugmentAsync(item, roundState, maxClicksThisRound, cancellationToken);
            if (!augmentApplied)
            {
                item.LastRoundClicks = roundState.Clicks;
                return ItemRunOutcome.ContinueNextRound;
            }

            cancellationToken.ThrowIfCancellationRequested();

            Log($"Item {item.Index}: Item augment sonrasi kontrol basliyor.");
            var inspection = await InspectAsync(item, combinedRules, cancellationToken);
            if (inspection.IsStuck)
            {
                item.LastStuckDuringAlteration = false;
                item.LastRoundClicks = roundState.Clicks;
                return ItemRunOutcome.StashedAsStuck;
            }

            var matchedRules = GetDistinctMatchedRuleTexts(combinedRules, inspection.Segments, inspection.Comparison);

            if (matchedRules.Count >= 2)
            {
                item.LastRoundClicks = roundState.Clicks;
                Log($"Item {item.Index}: Item tamamlandi. Bulunan modlar: '{string.Join("' | '", matchedRules)}'");
                return ItemRunOutcome.Completed;
            }

            Log($"Item {item.Index}: Item augment sonrasi tamamlanmadi. Bulunan farkli hedef mod sayisi {matchedRules.Count}/2. Alteration dongusu bastan basliyor.");
        }

        item.LastRoundClicks = roundState.Clicks;
        return ItemRunOutcome.ContinueNextRound;
    }

    private async Task<InspectionResult?> ApplyAlterationUntilDesiredModAsync(ItemRunSummary item, RoundState roundState, int maxClicksThisRound, CancellationToken cancellationToken)
    {
        var rules = _config.ClipboardCheck.GetActiveRules().ToList();
        return await ApplyAlterationUntilMatchAsync(item, roundState, maxClicksThisRound, rules, "Alteration asamasi basarili", cancellationToken);
    }

    private async Task<InspectionResult?> ApplyAlterationUntilMatchAsync(
        ItemRunSummary item,
        RoundState roundState,
        int maxClicksThisRound,
        IReadOnlyList<MatchRule> rules,
        string successLogPrefix,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && roundState.Clicks < maxClicksThisRound)
        {
            var alterationPoint = GetAvailableAlterationPoint(item.Index);
            if (alterationPoint is null)
            {
                return null;
            }

            item.LastAlterationPointIndex = alterationPoint.Index;

            var shiftHeld = false;
            try
            {
                Log($"Item {item.Index}: Alteration noktasi {alterationPoint.Index} secildi, sag tik yapiliyor.");
                RightClick(alterationPoint.Point);
                await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

                Log($"Item {item.Index}: Shift tusu basili tutuluyor.");
                InputController.KeyDown(NativeMethods.VK_SHIFT);
                shiftHeld = true;
                await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

                while (!cancellationToken.IsCancellationRequested && roundState.Clicks < maxClicksThisRound)
                {
                    if (alterationPoint.Uses >= _config.MaxAlterationsPerSourcePoint)
                    {
                        Log($"Item {item.Index}: Alteration noktasi {alterationPoint.Index} limitine ulasti. Yeni noktaya gecilecek.");
                        break;
                    }

                    Log($"Item {item.Index}: Alteration icin Shift basili sol tik yapiliyor.");
                    LeftClick(item.Point);
                    item.AlterationUses++;
                    item.TotalItemClicks++;
                    roundState.Clicks++;
                    alterationPoint.Uses++;
                    ReportProgress();
                    await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");

                    var inspection = await InspectAsync(item, rules, cancellationToken);
                    if (inspection.IsStuck)
                    {
                        item.LastStuckDuringAlteration = true;
                        return inspection;
                    }

                    if (inspection.HasDesiredMod)
                    {
                        Log($"Item {item.Index}: {successLogPrefix}. Eslesen mod: '{inspection.MatchedRuleText}'");
                        return inspection;
                    }
                }
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

        return null;
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
        ReportProgress();
        await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");
        Log($"Item {item.Index}: Tek augment denemesi tamamlandi.");
        return true;
    }

    private Task<InspectionResult> InspectAsync(ItemRunSummary item, CancellationToken cancellationToken)
    {
        return InspectAsync(item, _config.ClipboardCheck.GetActiveRules().ToList(), cancellationToken);
    }

    private async Task<InspectionResult> InspectAsync(ItemRunSummary item, IReadOnlyList<MatchRule> rules, CancellationToken cancellationToken)
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
        var normalizedText = NormalizeClipboardText(currentText);
        var sameTextCount = TrackRepeatedInspectionText(item, normalizedText);
        if (sameTextCount >= 2)
        {
            Log($"Item {item.Index}: Ayni metin tekrar sayisi {sameTextCount}/{SameTextStashThreshold}.");
        }

        if (sameTextCount >= SameTextStashThreshold)
        {
            Log($"Item {item.Index}: Ayni metin {SameTextStashThreshold} kez ust uste geldi. Item takildi kabul edilip stashe gonderilecek.");
            return new InspectionResult(false, string.Empty, Array.Empty<string>(), comparison, true);
        }

        var segments = GetClipboardSegments(currentText);
        var matchedRule = rules.FirstOrDefault(rule => IsRuleMatch(rule, segments, comparison, out _));

        if (matchedRule is not null)
        {
            IsRuleMatch(matchedRule, segments, comparison, out var matchedText);
            Log($"Item {item.Index}: Eslesme bulundu: '{matchedText}'");
            return new InspectionResult(true, matchedText, segments, comparison, false);
        }

        var activeText = rules.Count == 0
            ? "Aktif aranan mod yok"
            : string.Join(" | ", rules.Select(FormatRuleForLog));
        Log($"Item {item.Index}: Eslesme yok. Aranan ifadeler: '{activeText}'");
        return new InspectionResult(false, string.Empty, segments, comparison, false);
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

    private List<string> GetDistinctMatchedRuleTexts(IReadOnlyList<MatchRule> rules, IReadOnlyList<string> segments, StringComparison comparison)
    {
        var matchedTexts = new List<string>();
        var seenRuleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (!IsRuleMatch(rule, segments, comparison, out var matchedText))
            {
                continue;
            }

            var ruleKey = $"{rule.Text.Trim()}|{rule.ThresholdText.Trim()}";
            if (!seenRuleKeys.Add(ruleKey))
            {
                continue;
            }

            matchedTexts.Add(matchedText);
        }

        return matchedTexts;
    }

    private static IReadOnlyList<string> GetClipboardSegments(string currentText)
    {
        return currentText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment) && segment != "--------")
            .ToList();
    }

    private static string NormalizeClipboardText(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
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

    private static void ModifiedLeftClick(PointConfig point, params ushort[] modifierKeys)
    {
        InputController.MoveMouse(point.X, point.Y);
        Thread.Sleep(50);
        InputController.ModifiedLeftClick(modifierKeys);
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

    private void ReportProgress()
    {
        _progressChanged?.Invoke(new RunProgress(
            _allItems.Sum(item => item.TotalItemClicks),
            _allItems.Count(item => item.Completed),
            _allItems.Count(item => item.StashedAsStuck)));
    }

    private async Task SendCompletedItemToStashAsync(ItemRunSummary item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Log($"Item {item.Index}: Tamamlanan item stashe gonderiliyor (Ctrl basili sol tik).");
        InputController.MoveMouse(item.Point.X, item.Point.Y);
        await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");
        InputController.ModifiedLeftClick(NativeMethods.VK_CONTROL);
        await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");
        Log($"Item {item.Index}: Stash gonderimi tamamlandi.");
    }

    private async Task SendStuckItemToStashAsync(ItemRunSummary item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Log($"Item {item.Index}: Takilan item stashe gonderiliyor (Ctrl basili 3 sol tik).");
        InputController.MoveMouse(item.Point.X, item.Point.Y);
        await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");
        InputController.KeyDown(NativeMethods.VK_CONTROL);

        try
        {
            for (var i = 0; i < 3; i++)
            {
                InputController.LeftClick();
                await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");
            }
        }
        finally
        {
            InputController.KeyUp(NativeMethods.VK_CONTROL);
        }

        Log($"Item {item.Index}: Takilan item stash gonderimi tamamlandi.");
    }

    private int TrackRepeatedInspectionText(ItemRunSummary item, string normalizedText)
    {
        if (string.Equals(item.LastInspectionText, normalizedText, StringComparison.Ordinal))
        {
            item.SameInspectionTextCount++;
        }
        else
        {
            item.LastInspectionText = normalizedText;
            item.SameInspectionTextCount = 1;
        }

        return item.SameInspectionTextCount;
    }

    private void UpdateAlterationPointStatusAfterItem(ItemRunSummary item, ItemRunOutcome outcome)
    {
        if (!item.LastAlterationPointIndex.HasValue)
        {
            return;
        }

        var source = _alterationPoints.FirstOrDefault(point => point.Index == item.LastAlterationPointIndex.Value);
        if (source is null)
        {
            return;
        }

        if (outcome == ItemRunOutcome.StashedAsStuck && item.LastStuckDuringAlteration)
        {
            source.ConsecutiveStuckItems++;
            Log($"Alteration noktasi {source.Index}: Arka arkaya takilan item sayisi {source.ConsecutiveStuckItems}/{ConsecutiveStuckItemsBeforeSourceSwitch}.");

            if (source.ConsecutiveStuckItems >= ConsecutiveStuckItemsBeforeSourceSwitch)
            {
                source.Uses = Math.Max(source.Uses, _config.MaxAlterationsPerSourcePoint);
                source.ConsecutiveStuckItems = 0;
                Log($"Alteration noktasi {source.Index}: Arka arkaya {ConsecutiveStuckItemsBeforeSourceSwitch} item takildi. Currency bitti kabul edilip sonraki alteration noktasina gecilecek.");
            }

            return;
        }

        if (source.ConsecutiveStuckItems > 0)
        {
            Log($"Alteration noktasi {source.Index}: Takilma sayaci sifirlandi.");
            source.ConsecutiveStuckItems = 0;
        }
    }

    private AlterationPointState? GetAvailableAlterationPoint(int itemIndex)
    {
        if (_alterationPoints.Count == 0)
        {
            _alterationSourcesExhausted = true;
            Log($"Item {itemIndex}: Kullanilabilir alteration noktasi yok.");
            return null;
        }

        for (var attempt = 0; attempt < _alterationPoints.Count; attempt++)
        {
            var point = _alterationPoints[0];
            if (point.Uses < _config.MaxAlterationsPerSourcePoint)
            {
                return point;
            }

            _alterationPoints.RemoveAt(0);
            _alterationPoints.Add(point);
            Log($"Alteration noktasi {point.Index} limiti doldu. Siradaki alteration noktasina geciliyor.");
        }

        _alterationSourcesExhausted = true;
        Log($"Item {itemIndex}: Tum alteration noktalari limitine ulasti.");
        return null;
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
                : item.StashedAsStuck
                    ? "takildi, stashe gonderildi"
                : item.TotalItemClicks > 0 ? "yarida kesildi" : "baslamadi";
            Log($"Item {item.Index}: Alteration {item.AlterationUses} | Augment {item.AugmentUses} | Toplam sol tik {item.TotalItemClicks} | Sure: {FormatDuration(item.ActiveStopwatch.Elapsed)} | Durum: {status}");
        }

        Log($"Toplam item sayisi: {_allItems.Count}");
        Log($"Toplam alteration harcamasi: {totalAlterations}");
        Log($"Toplam augment harcamasi: {totalAugments}");
        Log($"Toplam currency harcamasi: {totalAlterations + totalAugments}");
        Log($"Toplam sol tik sayisi: {totalClicks}");
        foreach (var source in _alterationPoints.OrderBy(point => point.Index))
        {
            Log($"Alteration noktasi {source.Index}: {source.Uses}/{_config.MaxAlterationsPerSourcePoint} kullanim | Konum: {FormatPoint(source.Point)}");
        }
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
        public bool StashedAsStuck { get; set; }
        public string LastInspectionText { get; set; } = string.Empty;
        public int SameInspectionTextCount { get; set; }
        public int? LastAlterationPointIndex { get; set; }
        public bool LastStuckDuringAlteration { get; set; }
    }

    private enum ItemRunOutcome
    {
        ContinueNextRound,
        Completed,
        StashedAsStuck
    }

    private sealed class RoundState
    {
        public int Clicks { get; set; }
    }

    private sealed class AlterationPointState
    {
        public AlterationPointState(int index, PointConfig point)
        {
            Index = index;
            Point = point;
        }

        public int Index { get; }
        public PointConfig Point { get; }
        public int Uses { get; set; }
        public int ConsecutiveStuckItems { get; set; }
    }

    private sealed record InspectionResult(
        bool HasDesiredMod,
        string MatchedRuleText,
        IReadOnlyList<string> Segments,
        StringComparison Comparison,
        bool IsStuck);
}
