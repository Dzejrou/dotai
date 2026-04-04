using Godot;

using System;

[GlobalClass]
public partial class BlinkSpell : Spell
{
    private const int BlinkDistanceSteps = 6;

    [Export]
    public float BlinkDistance { get; set; } = 64.0f;

    public BlinkSpell()
    {
        ManaCost = 15;
        Cooldown = 1.5f;
    }

    public override bool CanCast(ISpellCaster caster)
    {
        if (!base.CanCast(caster))
            return false;

        return ResolveBlinkBody(caster) != null &&
               caster.SpellDirection != Vector2.Zero;
    }

    public override bool TryCast(ISpellCaster caster)
    {
        if (!CanCast(caster))
            return false;

        var blinkBody = ResolveBlinkBody(caster);
        if (blinkBody == null)
            return false;

        var direction = caster.SpellDirection.Normalized();
        var blinkDistance = ResolveBlinkDistance(blinkBody, direction);
        if (blinkDistance <= 0.0f)
            return false;

        if (!TrySpendCastMana(caster))
            return false;

        blinkBody.GlobalPosition += direction * blinkDistance;
        StartCooldown();
        return true;
    }

    private static PhysicsBody2D ResolveBlinkBody(ISpellCaster caster)
    {
        return caster?.SpellOrigin as PhysicsBody2D;
    }

    private float ResolveBlinkDistance(PhysicsBody2D blinkBody, Vector2 direction)
    {
        var requestedDistance = Math.Max(0.0f, BlinkDistance);
        if (requestedDistance <= 0.0f || direction == Vector2.Zero)
            return 0.0f;

        for (var step = BlinkDistanceSteps; step >= 1; step--)
        {
            var candidateDistance = requestedDistance * step / BlinkDistanceSteps;
            var motion = direction * candidateDistance;
            if (!blinkBody.TestMove(blinkBody.GlobalTransform, motion))
                return candidateDistance;
        }

        return 0.0f;
    }
}
