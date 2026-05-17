using Godot;

using System;

[GlobalClass]
public partial class Stats : Node
{
    [Export]
    public int MaxHealth { get; set; } = 100;

    [Export]
    public int MaxMana { get; set; } = 100;

    [Export]
    public int MP5 { get; set; } = 0;

    [Export]
    public float Power { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float CritRate { get; set; } = 0.05f;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public float CritDamage { get; set; } = 1.0f;

    [Export]
    public float Haste { get; set; } = 0.0f;

    [Export]
    public float MovementSpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public float PhysicalDamageBonus { get; set; } = 0.0f;

    [Export]
    public float FireDamageBonus { get; set; } = 0.0f;

    [Export]
    public float IceDamageBonus { get; set; } = 0.0f;

    [Export]
    public float PoisonDamageBonus { get; set; } = 0.0f;

    [Export]
    public float ArcaneDamageBonus { get; set; } = 0.0f;

    [Export]
    public float PhysicalResistance { get; set; } = 0.0f;

    [Export]
    public float FireResistance { get; set; } = 0.0f;

    [Export]
    public float IceResistance { get; set; } = 0.0f;

    [Export]
    public float PoisonResistance { get; set; } = 0.0f;

    [Export]
    public float ArcaneResistance { get; set; } = 0.0f;

    public int ResolvedMaxHealth => Math.Max(1, MaxHealth);
    public int ResolvedMaxMana => Math.Max(0, MaxMana);
    public int ResolvedMP5 => Math.Max(0, MP5);
    public float ResolvedPower => Math.Max(0.0f, Power);
    public float ResolvedCritRate => Math.Max(0.0f, CritRate);
    public float ResolvedCritDamage => Math.Max(0.0f, CritDamage);
    public float ResolvedHaste => Math.Max(0.0f, Haste);
    public float ResolvedMovementSpeedMultiplier => Math.Max(0.0f, MovementSpeedMultiplier);

    public float ResolveDamageBonus(DamageSchool school) => school switch
    {
        DamageSchool.Physical => PhysicalDamageBonus,
        DamageSchool.Fire => FireDamageBonus,
        DamageSchool.Ice => IceDamageBonus,
        DamageSchool.Poison => PoisonDamageBonus,
        DamageSchool.Arcane => ArcaneDamageBonus,
        _ => 0.0f,
    };

    public float ResolveResistance(DamageSchool school) => school switch
    {
        DamageSchool.Physical => PhysicalResistance,
        DamageSchool.Fire => FireResistance,
        DamageSchool.Ice => IceResistance,
        DamageSchool.Poison => PoisonResistance,
        DamageSchool.Arcane => ArcaneResistance,
        _ => 0.0f,
    };
}
