using Godot;

[GlobalClass]
public partial class FireBallSpell : ProjectileSpell
{
    [Export]
    public float BurnProcChance { get; set; } = 0.10f;

    protected override string ResolveStatusEffectTemplateName()
    {
        return "BurningEffect";
    }

    protected override float ResolveStatusProcChance()
    {
        return BurnProcChance;
    }
}
