using SystemAnalysis.Config;
using SystemAnalysis.Interop;

namespace SystemAnalysis.Automation;

public sealed class FlaskPressRunner
{
    private readonly AppConfig _config;
    private readonly Random _random = new();

    public FlaskPressRunner(AppConfig config)
    {
        _config = config;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var delay = NextFlaskDelay();
            await Task.Delay(delay, cancellationToken);

            var holdDuration = _random.Next(60, 101);
            InputController.PressKey(NativeMethods.VK_1, holdDuration);
        }
    }

    private int NextFlaskDelay()
    {
        var extra = _config.Timing.DelayFlaskPressExtraMs.Max <= _config.Timing.DelayFlaskPressExtraMs.Min
            ? _config.Timing.DelayFlaskPressExtraMs.Min
            : _random.Next(_config.Timing.DelayFlaskPressExtraMs.Min, _config.Timing.DelayFlaskPressExtraMs.Max + 1);

        return Math.Max(0, _config.Timing.DelayFlaskPressBaseMs) + Math.Max(0, extra);
    }
}
