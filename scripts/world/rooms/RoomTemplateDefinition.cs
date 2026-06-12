using Godot;

[GlobalClass]
public partial class RoomTemplateDefinition : Resource
{
    [Export]
    public StringName Id { get; set; } = default;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public PackedScene RoomScene { get; set; }

    [Export]
    public Godot.Collections.Array<RoomContentOption> ContentOptions { get; set; } = new();

    public RoomContentOption PickContentOption(RandomNumberGenerator random)
    {
        if (random == null)
        {
            GD.PushWarning($"{nameof(RoomTemplateDefinition)} '{GetLabel()}' cannot pick a content option without a random number generator.");
            return null;
        }

        var totalWeight = 0.0f;
        foreach (var option in ContentOptions)
        {
            if (option?.IsConfigured == true)
                totalWeight += option.Weight;
        }

        if (!(totalWeight > 0.0f))
        {
            GD.PushWarning($"{nameof(RoomTemplateDefinition)} '{GetLabel()}' has no valid content options configured.");
            return null;
        }

        var roll = random.Randf() * totalWeight;
        var cumulativeWeight = 0.0f;
        RoomContentOption fallbackOption = null;
        foreach (var option in ContentOptions)
        {
            if (option?.IsConfigured != true)
                continue;

            cumulativeWeight += option.Weight;
            fallbackOption = option;
            if (roll < cumulativeWeight)
                return option;
        }

        return fallbackOption;
    }

    public string GetLabel()
    {
        if (Id != null && !Id.IsEmpty)
            return Id;

        return !string.IsNullOrWhiteSpace(ResourcePath)
            ? ResourcePath
            : nameof(RoomTemplateDefinition);
    }
}
