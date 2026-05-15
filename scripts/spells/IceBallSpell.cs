using Godot;

[GlobalClass]
public partial class IceBallSpell : ProjectileSpell
{
    [Export]
    public float SlowProcChance { get; set; } = 0.10f;

    protected override string ResolveStatusEffectTemplateName()
    {
        return "SlowedEffect";
    }

    protected override float ResolveStatusProcChance()
    {
        return SlowProcChance;
    }

    protected override StatusEffect[] CreateAdditionalStatusEffectPayloads()
    {
        if (GetNodeOrNull<StatusEffect>("FrozenEffect")?.Duplicate() is not StatusEffect frozen)
            return null;

        DamageSchoolTag.EnsureOnChild(frozen, this);
        return new[] { frozen };
    }
}
