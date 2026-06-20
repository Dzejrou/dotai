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
            if (option?.IsRandomlySelectable == true)
                totalWeight += option.Weight;
        }

        if (!(totalWeight > 0.0f))
        {
            GD.PushWarning($"{nameof(RoomTemplateDefinition)} '{GetLabel()}' has no randomly-selectable content options configured.");
            return null;
        }

        var roll = random.Randf() * totalWeight;
        var cumulativeWeight = 0.0f;
        RoomContentOption fallbackOption = null;
        foreach (var option in ContentOptions)
        {
            if (option?.IsRandomlySelectable != true)
                continue;

            cumulativeWeight += option.Weight;
            fallbackOption = option;
            if (roll < cumulativeWeight)
                return option;
        }

        return fallbackOption;
    }

    // Weighted selection that excludes a single content id from the draw, used to stop the
    // generator placing the same option in two adjacent rooms. The remaining positive weights
    // are renormalized via one roll (no rerolling). If excluding the id would leave nothing
    // selectable - or no id is supplied - this falls back to the ordinary weighted draw, so a
    // sole configured option is still placed rather than failing.
    public RoomContentOption PickContentOption(RandomNumberGenerator random, StringName excludedId)
    {
        if (random == null)
        {
            GD.PushWarning($"{nameof(RoomTemplateDefinition)} '{GetLabel()}' cannot pick a content option without a random number generator.");
            return null;
        }

        if (excludedId == null || excludedId.IsEmpty)
            return PickContentOption(random);

        var totalWeight = 0.0f;
        foreach (var option in ContentOptions)
        {
            if (option?.IsRandomlySelectable == true && option.Id != excludedId)
                totalWeight += option.Weight;
        }

        // No alternative positive-weight option survives the exclusion: allow the only option
        // (the excluded one) rather than failing, by deferring to the ordinary draw.
        if (!(totalWeight > 0.0f))
            return PickContentOption(random);

        var roll = random.Randf() * totalWeight;
        var cumulativeWeight = 0.0f;
        RoomContentOption fallbackOption = null;
        foreach (var option in ContentOptions)
        {
            if (option?.IsRandomlySelectable != true || option.Id == excludedId)
                continue;

            cumulativeWeight += option.Weight;
            fallbackOption = option;
            if (roll < cumulativeWeight)
                return option;
        }

        return fallbackOption;
    }

    // Finds a content option by its id regardless of weight, so a guaranteed placement (e.g.
    // the zero-weight Pre-Boss option) can be resolved explicitly.
    public RoomContentOption FindContentOption(StringName id)
    {
        if (id == null || id.IsEmpty)
            return null;

        foreach (var option in ContentOptions)
        {
            if (option != null && option.Id == id)
                return option;
        }

        return null;
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
