using Godot;

[GlobalClass]
public partial class TEST_add_level : Spell
{
    public override bool ShouldFaceCastRequest => false;

    public override bool TryCast(ISpellCaster caster, SpellCastRequest request)
    {
        if (!CanCast(caster, request))
            return false;

        var player = ResolvePlayer(caster);
        if (player == null)
        {
            GD.PushWarning($"{GetPath()}: {nameof(TEST_add_level)} can only be cast by a player.");
            return false;
        }

        if (!TrySpendCastMana(caster))
            return false;

        if (!player.TryAdjustLevelForTesting(1))
        {
            player.ShowFloatingText("MAX LEVEL", new Color(1.0f, 0.95f, 0.2f, 1.0f));
            return false;
        }

        player.ShowFloatingText($"LEVEL {player.Level}", new Color(1.0f, 0.95f, 0.2f, 1.0f));
        StartCooldown();
        return true;
    }

    private static Player ResolvePlayer(ISpellCaster caster)
    {
        if (caster is Player player)
            return player;

        return caster?.SpellOrigin as Player;
    }
}
