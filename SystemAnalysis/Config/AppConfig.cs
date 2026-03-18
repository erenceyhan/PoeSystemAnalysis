namespace SystemAnalysis.Config;

public sealed class AppConfig
{
    public string Mode { get; set; } = "single_craft";
    public PointConfig SourcePoint { get; set; } = new();
    public PointConfig TargetPoint { get; set; } = new();
    public PointConfig InspectPoint { get; set; } = new();
    public ClipboardCheckConfig ClipboardCheck { get; set; } = new();
    public TimingConfig Timing { get; set; } = new();
    public SafetyConfig Safety { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public sealed class PointConfig
{
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class ClipboardCheckConfig
{
    public string TriggerShortcut { get; set; } = "ctrl+alt+c";
    public string MustContain { get; set; } = "wanted_mod";
    public bool CaseSensitive { get; set; }
    public List<MatchRule> MatchRules { get; set; } = CreateDefaultRules();

    public IEnumerable<MatchRule> GetActiveRules()
    {
        var rules = MatchRules
            .Where(rule => rule.Enabled && !string.IsNullOrWhiteSpace(rule.Text))
            .ToList();

        if (rules.Count > 0)
        {
            return rules;
        }

        if (!string.IsNullOrWhiteSpace(MustContain))
        {
            return new[] { new MatchRule { Enabled = true, Text = MustContain } };
        }

        return Array.Empty<MatchRule>();
    }

    public void Normalize()
    {
        if (MatchRules is null || MatchRules.Count == 0)
        {
            MatchRules = CreateDefaultRules();
        }

        while (MatchRules.Count < 5)
        {
            MatchRules.Add(new MatchRule());
        }
    }

    private static List<MatchRule> CreateDefaultRules()
    {
        return new List<MatchRule>
        {
            new() { Enabled = true, Text = "wanted_mod" },
            new(),
            new(),
            new(),
            new()
        };
    }
}

public sealed class MatchRule
{
    public bool Enabled { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class TimingConfig
{
    public DelayRange DelayBeforeStartMs { get; set; } = new(3000, 3000);
    public DelayRange DelayBetweenActionsMs { get; set; } = new(180, 180);
    public DelayRange DelayAfterCraftMs { get; set; } = new(220, 220);
    public DelayRange DelayBeforeInspectMs { get; set; } = new(180, 180);
    public DelayRange DelayAfterInspectShortcutMs { get; set; } = new(200, 200);

    public int? DelayBeforeStart { get; set; }
    public int? DelayBetweenActions { get; set; }
    public int? DelayAfterCraft { get; set; }
    public int? DelayBeforeInspect { get; set; }
    public int? DelayAfterInspectShortcut { get; set; }

    public void Normalize()
    {
        DelayBeforeStartMs = NormalizeRange(DelayBeforeStartMs, DelayBeforeStart, 3000);
        DelayBetweenActionsMs = NormalizeRange(DelayBetweenActionsMs, DelayBetweenActions, 180);
        DelayAfterCraftMs = NormalizeRange(DelayAfterCraftMs, DelayAfterCraft, 220);
        DelayBeforeInspectMs = NormalizeRange(DelayBeforeInspectMs, DelayBeforeInspect, 180);
        DelayAfterInspectShortcutMs = NormalizeRange(DelayAfterInspectShortcutMs, DelayAfterInspectShortcut, 200);
    }

    private static DelayRange NormalizeRange(DelayRange? range, int? legacyValue, int defaultValue)
    {
        if (range is null)
        {
            return new DelayRange(legacyValue ?? defaultValue, legacyValue ?? defaultValue);
        }

        range.Normalize();
        return range;
    }
}

public sealed class DelayRange
{
    public DelayRange()
    {
    }

    public DelayRange(int min, int max)
    {
        Min = min;
        Max = max;
        Normalize();
    }

    public int Min { get; set; }
    public int Max { get; set; }

    public void Normalize()
    {
        Min = Math.Max(0, Min);
        Max = Math.Max(0, Max);

        if (Max < Min)
        {
            (Min, Max) = (Max, Min);
        }
    }
}

public sealed class SafetyConfig
{
    public int MouseMoveTolerancePx { get; set; } = 4;
}

public sealed class LoggingConfig
{
    public string DirectoryPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "logs");
    public bool ShowCopiedText { get; set; }
}
