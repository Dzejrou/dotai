using Godot;

public enum AiSpellTargetRelation
{
    Hostile = 0,
    Friendly = 1,
    Self = 2,
    Any = 3,
}

[GlobalClass]
public partial class AiSpellOption : Resource
{
    [Export]
    public StringName SpellId { get; set; }

    [Export]
    public AiSpellTargetRelation TargetRelation { get; set; } = AiSpellTargetRelation.Hostile;

    [Export]
    public int Priority { get; set; } = 0;

    [Export(PropertyHint.Range, "0,100,0.1,or_greater")]
    public float Weight { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0,4096,0.1,or_greater")]
    public float MinRange { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0,4096,0.1,or_greater")]
    public float MaxRange { get; set; } = 1000000.0f;

    [Export(PropertyHint.Range, "0,600,0.1,or_greater")]
    public float CooldownSeconds { get; set; } = 0.0f;
}
