using Godot;

using System;

[GlobalClass]
public partial class LootEntry : Resource
{
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float DropChance { get; set; } = 1.0f;

    [Export]
    public DropDefinition Definition { get; set; }

    [Export(PropertyHint.Range, "0,9999,1")]
    public int Amount { get; set; } = 5;

    public bool IsConfigured => Definition?.DropScene != null;

    public bool ShouldDrop(RandomNumberGenerator random)
    {
        if (!IsConfigured)
            return false;

        return random.Randf() <= Mathf.Clamp(DropChance, 0.0f, 1.0f);
    }

    public Drop CreateDropInstance()
    {
        var drop = Definition?.CreateDropInstance();
        if (drop == null)
            return null;

        Definition.ConfigureDrop(drop, Math.Max(0, Amount));

        return drop;
    }
}
