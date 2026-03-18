using System.Text.Json.Serialization;

namespace SystemAnalysis.Config;

public sealed class AppConfig
{
    public string Mode { get; set; } = "hold_shift_spam";
    public int MaxClicksPerItemRound { get; set; } = 300;
    public PointConfig SourcePoint { get; set; } = new();
    public PointConfig SecondarySourcePoint { get; set; } = new();
    public PointConfig TargetPoint { get; set; } = new();
    public PointConfig InspectPoint { get; set; } = new();
    [JsonIgnore]
    public List<PointConfig> TargetPoints { get; set; } = new();
    public List<string> SavedMods { get; set; } = new();
    public ClipboardCheckConfig ClipboardCheck { get; set; } = new();
    public TimingConfig Timing { get; set; } = new();
    public SafetyConfig Safety { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();

    public void Normalize()
    {
        if (!string.Equals(Mode, "hold_shift_spam", StringComparison.OrdinalIgnoreCase))
        {
            Mode = "hold_shift_spam";
        }

        SavedMods ??= new List<string>();
        SavedMods = SavedMods
            .Where(mod => !string.IsNullOrWhiteSpace(mod))
            .Select(mod => mod.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(mod => mod, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var ruleText in ClipboardCheck.MatchRules
                     .Concat(ClipboardCheck.AugmentMatchRules)
                     .Where(rule => !string.IsNullOrWhiteSpace(rule.Text))
                     .Select(rule => rule.Text.Trim()))
        {
            if (!SavedMods.Contains(ruleText, StringComparer.OrdinalIgnoreCase))
            {
                SavedMods.Add(ruleText);
            }
        }

        SavedMods = SavedMods
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(mod => mod, StringComparer.OrdinalIgnoreCase)
            .ToList();

        MaxClicksPerItemRound = Math.Max(1, MaxClicksPerItemRound);
        TargetPoints ??= new List<PointConfig>();
    }
}

public sealed class PointConfig
{
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class ClipboardCheckConfig
{
    public string TriggerShortcut { get; set; } = "ctrl+alt+c";
    public string MustContain { get; set; } = string.Empty;
    public bool CaseSensitive { get; set; }
    public bool UseAugmentCycle { get; set; }
    public string PercentageThresholdText { get; set; } = string.Empty;
    public List<MatchRule> MatchRules { get; set; } = CreateDefaultRules();
    public List<MatchRule> AugmentMatchRules { get; set; } = CreateDefaultAugmentRules();

    public IEnumerable<MatchRule> GetActiveRules()
    {
        var rules = MatchRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Text))
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

    public IEnumerable<MatchRule> GetActiveAugmentRules()
    {
        return AugmentMatchRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Text))
            .ToList();
    }

    public void Normalize()
    {
        if (MatchRules is null || MatchRules.Count == 0)
        {
            MatchRules = CreateDefaultRules();
        }

        while (MatchRules.Count < 15)
        {
            MatchRules.Add(new MatchRule());
        }

        if (AugmentMatchRules is null || AugmentMatchRules.Count == 0)
        {
            AugmentMatchRules = CreateDefaultAugmentRules();
        }

        while (AugmentMatchRules.Count < 8)
        {
            AugmentMatchRules.Add(new MatchRule());
        }

        foreach (var rule in MatchRules.Concat(AugmentMatchRules))
        {
            if (string.IsNullOrWhiteSpace(rule.ThresholdText) && rule.Text.Contains('#') && !string.IsNullOrWhiteSpace(PercentageThresholdText))
            {
                rule.ThresholdText = PercentageThresholdText.Trim();
            }
        }
    }

    private static List<MatchRule> CreateDefaultRules()
    {
        return new List<MatchRule>
        {
            new(),
            new(),
            new(),
            new(),
            new(),
            new(),
            new(),
            new(),
            new(),
            new(),
            new(),
            new(),
            new(),
            new(),
            new()
        };
    }

    private static List<MatchRule> CreateDefaultAugmentRules()
    {
        return new List<MatchRule>
        {
            new(),
            new(),
            new(),
            new(),
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
    public string ThresholdText { get; set; } = string.Empty;
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
