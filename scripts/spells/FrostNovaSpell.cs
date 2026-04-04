using Godot;

using System;

[GlobalClass]
public partial class FrostNovaSpell : NovaSpell
{
    [Export]
    public int DirectDamage { get; set; } = 5;

    public FrostNovaSpell()
    {
        ManaCost = 20;
        Cooldown = 15.0f;
    }

    protected override int ResolveDamage(Node target)
    {
        return Math.Max(1, DirectDamage);
    }
}
