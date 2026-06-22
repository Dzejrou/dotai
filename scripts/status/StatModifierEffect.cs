using Godot;

// Reusable, editor-configurable stat buff/debuff. Carries additive-percent and flat stat
// modifiers that StatusEffectController aggregates and CombatCharacter folds into its
// resolved stats - it never mutates base Stats on application or reverses them on removal.
// Multiple additive-percent modifiers of the same stat sum before being applied once; flat
// modifiers sum directly.
//
// This is the shared representation for the buff foundation (e.g. the Demon boss's
// permanent, undispellable Enrage, and the upcoming dungeon-difficulty enemy buffs). Each
// distinct configured buff must use its own StatusKeyName so they do not refresh/replace
// one another.
[GlobalClass]
public partial class StatModifierEffect : StatusEffect
{
    [Export]
    public string StatusKeyName { get; set; } = "stat_modifier";

    [Export]
    public bool Unique { get; set; } = true;

    // Additive percent (0.2 = +20%); summed with other active percents, then applied once.
    [Export]
    public float MaxHealthPercent { get; set; } = 0.0f;

    [Export]
    public float PowerPercent { get; set; } = 0.0f;

    // Flat Haste, in the same units as Stats.Haste.
    [Export]
    public int HasteFlat { get; set; } = 0;

    // Generic (all-school) additive damage bonus, in the same units as Stats damage bonus.
    [Export]
    public float DamageBonusFlat { get; set; } = 0.0f;

    // Flat per-school resistance additions, in the same units as Stats resistances.
    [Export]
    public float PhysicalResistanceFlat { get; set; } = 0.0f;

    [Export]
    public float FireResistanceFlat { get; set; } = 0.0f;

    [Export]
    public float IceResistanceFlat { get; set; } = 0.0f;

    [Export]
    public float PoisonResistanceFlat { get; set; } = 0.0f;

    [Export]
    public float ArcaneResistanceFlat { get; set; } = 0.0f;

    public override StringName StatusKey => StatusKeyName;

    public override bool IsUniqueByStatusKey => Unique;

    public override float MaxHealthPercentModifier => MaxHealthPercent;

    public override float PowerPercentModifier => PowerPercent;

    public override int HasteFlatModifier => HasteFlat;

    public override float DamageBonusFlatModifier => DamageBonusFlat;

    public override float ResolveResistanceFlatModifier(DamageSchool school) => school switch
    {
        DamageSchool.Physical => PhysicalResistanceFlat,
        DamageSchool.Fire => FireResistanceFlat,
        DamageSchool.Ice => IceResistanceFlat,
        DamageSchool.Poison => PoisonResistanceFlat,
        DamageSchool.Arcane => ArcaneResistanceFlat,
        _ => 0.0f,
    };

    protected override void CopyConfigurationFrom(StatusEffect replacement)
    {
        base.CopyConfigurationFrom(replacement);

        if (replacement is not StatModifierEffect other)
            return;

        StatusKeyName = other.StatusKeyName;
        Unique = other.Unique;
        MaxHealthPercent = other.MaxHealthPercent;
        PowerPercent = other.PowerPercent;
        HasteFlat = other.HasteFlat;
        DamageBonusFlat = other.DamageBonusFlat;
        PhysicalResistanceFlat = other.PhysicalResistanceFlat;
        FireResistanceFlat = other.FireResistanceFlat;
        IceResistanceFlat = other.IceResistanceFlat;
        PoisonResistanceFlat = other.PoisonResistanceFlat;
        ArcaneResistanceFlat = other.ArcaneResistanceFlat;
    }
}
