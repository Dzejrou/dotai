using Godot;

[GlobalClass]
public partial class LootEntry : Resource
{
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float DropChance { get; set; } = 1.0f;

    [Export]
    public DropDefinition Definition { get; set; }

    public bool IsConfigured => Definition?.DropScene != null;

    public bool ShouldDrop(RandomNumberGenerator random)
    {
        if (!IsConfigured)
            return false;

        return random.Randf() <= Mathf.Clamp(DropChance, 0.0f, 1.0f);
    }
}
