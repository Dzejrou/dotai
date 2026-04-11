using Godot;

using System;

[GlobalClass]
public partial class Healing : Node
{
    [Export]
    public int MinimumHealing { get; set; } = 1;

    [Export]
    public int MaximumHealing { get; set; } = 1;

    public int Amount { get; private set; }

    public Node Source { get; private set; }

    public ulong SourceInstanceId { get; private set; }

    public int ResolveAmount(RandomNumberGenerator randomNumberGenerator = null)
    {
        var maximumHealing = Math.Max(MinimumHealing, MaximumHealing);
        var minimumHealing = Math.Min(MinimumHealing, maximumHealing);
        if (randomNumberGenerator != null)
            return Math.Max(1, randomNumberGenerator.RandiRange(minimumHealing, maximumHealing));

        return Math.Max(1, maximumHealing);
    }

    public void InitializeRuntime(Node source, int amount)
    {
        Source = source;
        SourceInstanceId = source != null && GodotObject.IsInstanceValid(source)
            ? source.GetInstanceId()
            : 0UL;
        Amount = Math.Max(0, amount);
    }

    public static Healing DuplicateFrom(Node owner)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner))
            return null;

        return owner.GetNodeOrNull<Healing>("Healing")?.Duplicate() as Healing;
    }
}
