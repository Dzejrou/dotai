using Godot;

using System;

[GlobalClass]
public partial class ContentSet : Resource
{
    [Export]
    public Godot.Collections.Array<ContentTemplateEntry> Entries { get; set; } = new();

    public PackedScene PickTemplate(RandomNumberGenerator random)
    {
        if (random == null)
        {
            GD.PushWarning($"{nameof(ContentSet)} '{GetLabel()}' cannot pick a template without a random number generator.");
            return null;
        }

        var totalWeight = 0.0f;
        foreach (var entry in Entries)
        {
            if (entry?.IsConfigured == true)
                totalWeight += entry.Weight;
        }

        if (!(totalWeight > 0.0f))
        {
            GD.PushWarning($"{nameof(ContentSet)} '{GetLabel()}' has no valid content templates configured.");
            return null;
        }

        var roll = random.Randf() * totalWeight;
        var cumulativeWeight = 0.0f;
        PackedScene fallbackScene = null;
        foreach (var entry in Entries)
        {
            if (entry?.IsConfigured != true)
                continue;

            cumulativeWeight += entry.Weight;
            fallbackScene = entry.ContentScene;
            if (roll < cumulativeWeight)
                return entry.ContentScene;
        }

        return fallbackScene;
    }

    private string GetLabel()
    {
        return !string.IsNullOrWhiteSpace(ResourcePath)
            ? ResourcePath
            : nameof(ContentSet);
    }
}
