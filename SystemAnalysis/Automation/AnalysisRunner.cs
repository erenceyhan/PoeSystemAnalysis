using System.Diagnostics;
using System.Media;
using SystemAnalysis.Config;
using SystemAnalysis.Interop;

namespace SystemAnalysis.Automation;

public sealed class AnalysisRunner
{
    private readonly AppConfig _config;
    private readonly Action<string>? _log;
    private readonly Random _random = new();
    private readonly List<ItemRunSummary> _completedItems = new();
    private readonly Stopwatch _totalStopwatch = Stopwatch.StartNew();
    private bool _summaryLogged;

    public AnalysisRunner(AppConfig config, Action<string>? log = null)
    {
        _config = config;
        _log = log;
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

            for (var i = 0; i < _config.TargetPoints.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var point = _config.TargetPoints[i];
                currentItem = new ItemRunSummary(i + 1, point);
                Log($"Item {currentItem.Index}/{_config.TargetPoints.Count} basladi: {FormatPoint(point)}");

                await RunHoldShiftSpamForItemAsync(currentItem, cancellationToken);

                currentItem.Completed = true;
                currentItem.Stopwatch.Stop();
                _completedItems.Add(currentItem);
                LogItemSummary(currentItem, "tamamlandi");
                currentItem = null;
            }

            Log("Secilen tum itemler tamamlandi.");
            SystemSounds.Asterisk.Play();
        }
        finally
        {
            if (currentItem is not null)
            {
                currentItem.Stopwatch.Stop();
                _completedItems.Add(currentItem);
                LogItemSummary(currentItem, currentItem.Completed ? "tamamlandi" : "yarida kesildi");
            }

            InputController.ReleaseCommonModifiers();
            LogSummary();
        }
    }

    private async Task RunHoldShiftSpamForItemAsync(ItemRunSummary item, CancellationToken cancellationToken)
    {
        var shiftHeld = false;

        try
        {
            Log($"Item {item.Index}: Currency noktasina sag tik yapiliyor.");
            RightClick(_config.SourcePoint);
            await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

            Log($"Item {item.Index}: Shift tusu basili tutuluyor.");
            InputController.KeyDown(NativeMethods.VK_SHIFT);
            shiftHeld = true;
            await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

            while (!cancellationToken.IsCancellationRequested)
            {
                Log($"Item {item.Index}: Shift basili sol tik yapiliyor.");
                LeftClick(item.Point);
                item.LeftClicks++;
                await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");

                if (await InspectAsync(item, cancellationToken))
                {
                    Log($"Item {item.Index}: Hedef mod bulundu.");
                    return;
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

    private async Task<bool> InspectAsync(ItemRunSummary item, CancellationToken cancellationToken)
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

        var rules = _config.ClipboardCheck.GetActiveRules().ToList();
        var matchedRule = rules.FirstOrDefault(rule => currentText.Contains(rule.Text, comparison));

        if (matchedRule is not null)
        {
            Log($"Item {item.Index}: Eslesme bulundu: '{matchedRule.Text}'");
            return true;
        }

        var activeText = rules.Count == 0
            ? "Aktif aranan mod yok"
            : string.Join(" | ", rules.Select(rule => rule.Text));
        Log($"Item {item.Index}: Eslesme yok. Aranan ifadeler: '{activeText}'");
        return false;
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
        Log($"Item {item.Index} {status}. Konum: {FormatPoint(item.Point)} | Sol tik: {item.LeftClicks} | Sure: {FormatDuration(item.Stopwatch.Elapsed)}");
    }

    private void LogSummary()
    {
        if (_summaryLogged)
        {
            return;
        }

        _totalStopwatch.Stop();
        var totalLeftClicks = _completedItems.Sum(item => item.LeftClicks);
        Log($"Toplam item sayisi: {_completedItems.Count}");
        Log($"Toplam sol tik sayisi: {totalLeftClicks}");
        Log($"Toplam sure: {FormatDuration(_totalStopwatch.Elapsed)}");
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
            Stopwatch = Stopwatch.StartNew();
        }

        public int Index { get; }
        public PointConfig Point { get; }
        public Stopwatch Stopwatch { get; }
        public int LeftClicks { get; set; }
        public bool Completed { get; set; }
    }
}
