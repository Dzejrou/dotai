using Godot;

// Centralized damage-school display colors used by spell UI (tooltips). Shades are
// picked to stay readable on the dark opaque tooltip background.
public static class SpellSchoolColors
{
    private static readonly Color PhysicalColor = new(0.96f, 0.96f, 0.96f);
    private static readonly Color FireColor = new(1.00f, 0.55f, 0.24f);
    private static readonly Color IceColor = new(0.45f, 0.75f, 1.00f);
    private static readonly Color PoisonColor = new(0.45f, 0.85f, 0.35f);
    private static readonly Color ArcaneColor = new(0.78f, 0.52f, 1.00f);

    public static Color GetColor(DamageSchool school) => school switch
    {
        DamageSchool.Physical => PhysicalColor,
        DamageSchool.Fire => FireColor,
        DamageSchool.Ice => IceColor,
        DamageSchool.Poison => PoisonColor,
        DamageSchool.Arcane => ArcaneColor,
        _ => PhysicalColor,
    };
}
