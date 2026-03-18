using SystemAnalysis.Config;
using SystemAnalysis.Interop;

namespace SystemAnalysis.Automation;

public sealed class AnalysisRunner
{
    private readonly AppConfig _config;
    private readonly Action<string>? _log;
    private readonly Random _random = new();
    private int _totalLeftClicks;
    private bool _summaryLogged;

    public AnalysisRunner(AppConfig config, Action<string>? log = null)
    {
        _config = config;
        _log = log;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startDelay = NextDelay(_config.Timing.DelayBeforeStartMs);
            Log($"SystemAnalysis {startDelay} ms sonra baslayacak. Kontrolu eline alirsan otomatik durur.");
            await Task.Delay(startDelay, cancellationToken);

            if (string.Equals(_config.Mode, "hold_shift_spam", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_config.Mode, "hold_ctrl_spam", StringComparison.OrdinalIgnoreCase))
            {
                await RunHoldShiftSpamAsync(cancellationToken);
                return;
            }

            await RunSingleCraftAsync(cancellationToken);
        }
        finally
        {
            LogSummary();
        }
    }

    private async Task RunSingleCraftAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Log("Currency noktasina sag tik yapiliyor.");
            RightClick(_config.SourcePoint);
            await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

            Log("Item noktasina sol tik yapiliyor.");
            LeftClick(_config.TargetPoint);
            _totalLeftClicks++;
            await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");

            if (await InspectAsync(cancellationToken))
            {
                Log("Hedef mod bulundu. Islem durduruldu.");
                LogSummary();
                break;
            }
        }
    }

    private async Task RunHoldShiftSpamAsync(CancellationToken cancellationToken)
    {
        try
        {
            Log("Currency noktasina sag tik yapiliyor.");
            RightClick(_config.SourcePoint);
            await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

            Log("Shift tusu basili tutuluyor.");
            InputController.KeyDown(NativeMethods.VK_SHIFT);
            await Delay(_config.Timing.DelayBetweenActionsMs, cancellationToken, "Aksiyon Arasi");

            while (!cancellationToken.IsCancellationRequested)
            {
                Log("Item noktasina Shift basili sol tik yapiliyor.");
                LeftClick(_config.TargetPoint);
                _totalLeftClicks++;
                await Delay(_config.Timing.DelayAfterCraftMs, cancellationToken, "Craft Sonrasi");

                if (await InspectAsync(cancellationToken))
                {
                    Log("Hedef mod bulundu. Shift birakiliyor ve islem durduruluyor.");
                    LogSummary();
                    break;
                }
            }
        }
        finally
        {
            InputController.ReleaseCommonModifiers();
            InputController.KeyUp(NativeMethods.VK_SHIFT);
            Log("Shift tusu birakildi.");
        }
    }

    private async Task<bool> InspectAsync(CancellationToken cancellationToken)
    {
        Log("Kontrol noktasi uzerine gidiliyor.");
        InputController.MoveMouse(_config.InspectPoint.X, _config.InspectPoint.Y);
        await Delay(_config.Timing.DelayBeforeInspectMs, cancellationToken, "Inspect Oncesi");

        var previousText = ClipboardHelper.GetText();
        Log("Kontrol kisayolu gonderiliyor.");
        TriggerClipboardShortcut();
        await Delay(_config.Timing.DelayAfterInspectShortcutMs, cancellationToken, "Kisayol Sonrasi");
        var currentText = ClipboardHelper.GetText();

        if (string.Equals(currentText, previousText, StringComparison.Ordinal))
        {
            Log("Clipboard degismedi, yine de icerik kontrol edildi.");
        }

        var comparison = _config.ClipboardCheck.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var rules = _config.ClipboardCheck.GetActiveRules().ToList();
        var matchedRule = rules.FirstOrDefault(rule => currentText.Contains(rule.Text, comparison));
        var found = matchedRule is not null;

        if (found)
        {
            Log($"Eslesme bulundu: '{matchedRule!.Text}'");
        }
        else
        {
            var activeText = rules.Count == 0
                ? "Aktif aranan mod yok"
                : string.Join(" | ", rules.Select(rule => rule.Text));
            Log($"Eslesme yok. Aranan ifadeler: '{activeText}'");
        }

        return found;
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

        if (!string.Equals(_config.ClipboardCheck.TriggerShortcut, "ctrl+alt+c", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Bu surum sadece ctrl+c veya ctrl+alt+c kisayolunu destekliyor.");
        }
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

    private static Task Delay(int milliseconds, CancellationToken cancellationToken)
    {
        return Task.Delay(Math.Max(0, milliseconds), cancellationToken);
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

    private void LogSummary()
    {
        if (_summaryLogged)
        {
            return;
        }

        Log($"Toplam sol tik sayisi: {_totalLeftClicks}");
        _summaryLogged = true;
    }
}
