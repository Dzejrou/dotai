public static class EquipmentStatIds
{
    public const string MaxHealth = "MaxHealth";
    public const string MaxMana = "MaxMana";
    public const string MP5 = "MP5";
    public const string Power = "Power";
    public const string CritRate = "CritRate";
    public const string CritDamage = "CritDamage";
    public const string Haste = "Haste";
    public const string MovementSpeedMultiplier = "MovementSpeedMultiplier";

    public const string DamageBonus = "DamageBonus";

    public const string PhysicalDamageBonus = "PhysicalDamageBonus";
    public const string FireDamageBonus = "FireDamageBonus";
    public const string IceDamageBonus = "IceDamageBonus";
    public const string PoisonDamageBonus = "PoisonDamageBonus";
    public const string ArcaneDamageBonus = "ArcaneDamageBonus";

    public const string PhysicalResistance = "PhysicalResistance";
    public const string FireResistance = "FireResistance";
    public const string IceResistance = "IceResistance";
    public const string PoisonResistance = "PoisonResistance";
    public const string ArcaneResistance = "ArcaneResistance";

    public static string DamageBonusFor(DamageSchool school) => school switch
    {
        DamageSchool.Physical => PhysicalDamageBonus,
        DamageSchool.Fire => FireDamageBonus,
        DamageSchool.Ice => IceDamageBonus,
        DamageSchool.Poison => PoisonDamageBonus,
        DamageSchool.Arcane => ArcaneDamageBonus,
        _ => string.Empty,
    };

    public static string ResistanceFor(DamageSchool school) => school switch
    {
        DamageSchool.Physical => PhysicalResistance,
        DamageSchool.Fire => FireResistance,
        DamageSchool.Ice => IceResistance,
        DamageSchool.Poison => PoisonResistance,
        DamageSchool.Arcane => ArcaneResistance,
        _ => string.Empty,
    };
}
