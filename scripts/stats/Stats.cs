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

    public int ResolvedMaxHealth => Math.Max(1, MaxHealth);
    public int ResolvedMaxMana => Math.Max(0, MaxMana);
    public int ResolvedMP5 => Math.Max(0, MP5);
    public float ResolvedPower => Math.Max(0.0f, Power);
    public float ResolvedCritRate => Math.Max(0.0f, CritRate);
    public float ResolvedCritDamage => Math.Max(0.0f, CritDamage);
    public float ResolvedHaste => Math.Max(0.0f, Haste);
    public float ResolvedMovementSpeedMultiplier => Math.Max(0.0f, MovementSpeedMultiplier);
}
