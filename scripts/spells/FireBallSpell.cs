using Godot;

[GlobalClass]
public partial class FireBallSpell : ProjectileSpell
{
    [Export]
    public float BurnProcChance { get; set; } = 0.10f;

    public FireBallSpell()
    {
        ManaCost = 0;
        ProjectileColor = new Color(1.0f, 0.45f, 0.1f, 1.0f);
    }

    protected override string ResolveStatusEffectTemplateName()
    {
        return "BurningEffect";
    }

    protected override float ResolveStatusProcChance()
    {
        return BurnProcChance;
    }
}
