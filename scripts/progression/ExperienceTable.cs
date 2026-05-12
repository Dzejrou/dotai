using Godot;

using System;

[GlobalClass]
public partial class ExperienceTable : Resource
{
    [Export]
    public Godot.Collections.Array<int> ExperienceToNextLevel { get; set; } = new();

    public int GetRequiredExperienceForLevel(int level, int fallback)
    {
        var safeFallback = Math.Max(1, fallback);
        if (level <= 0 || ExperienceToNextLevel == null)
            return safeFallback;

        var index = level - 1;
        if (index >= ExperienceToNextLevel.Count)
            return safeFallback;

        var entry = ExperienceToNextLevel[index];
        return entry <= 0 ? safeFallback : entry;
    }
}
