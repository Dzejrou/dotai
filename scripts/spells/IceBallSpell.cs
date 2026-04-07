using Godot;

[GlobalClass]
public partial class IceBallSpell : ProjectileSpell
{
    [Export]
    public float SlowProcChance { get; set; } = 0.10f;

    public IceBallSpell()
    {
        ManaCost = 0;
        ProjectileColor = new Color(0.35f, 0.72f, 1.0f, 1.0f);
    }

    protected override string ResolveStatusEffectTemplateName()
    {
        return "SlowedEffect";
    }

    protected override float ResolveStatusProcChance()
    {
        return SlowProcChance;
    }
}
