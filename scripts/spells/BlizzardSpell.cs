using Godot;

[GlobalClass]
public partial class BlizzardSpell : GroundPlacedSpell
{
    public BlizzardSpell()
    {
        ManaCost = 30;
        Cooldown = 6.0f;
    }
}
