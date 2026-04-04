using Godot;

using System;

[GlobalClass]
public partial class FireNovaSpell : NovaSpell
{
    private readonly RandomNumberGenerator _random = new();

    [Export]
    public int MinimumDamage { get; set; } = 6;

    [Export]
    public int MaximumDamage { get; set; } = 10;

    public FireNovaSpell()
    {
        ManaCost = 20;
    }

    public override void _Ready()
    {
        base._Ready();
        _random.Randomize();
    }

    protected override int ResolveDamage(Node target)
    {
        var maximumDamage = Math.Max(MinimumDamage, MaximumDamage);
        return _random.RandiRange(Math.Min(MinimumDamage, maximumDamage), maximumDamage);
    }
}
