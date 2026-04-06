using Godot;

[GlobalClass]
public partial class FireballSpell : Spell
{
    public FireballSpell()
    {
        ManaCost = 0;
    }

    [Export]
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public float ProjectileSpeed { get; set; } = 280.0f;

    [Export]
    public float ProjectileLifetime { get; set; } = 2.5f;

    [Export]
    public float ProjectileMaxDistance { get; set; } = 320.0f;

    public override bool CanCast(ISpellCaster caster)
    {
        if (!base.CanCast(caster) || ProjectileScene == null)
            return false;
        return true;
    }

    public override bool TryCast(ISpellCaster caster)
    {
        if (!CanCast(caster))
            return false;

        var fireDirection = caster.SpellDirection;
        if (fireDirection == Vector2.Zero)
            fireDirection = DirectionHelper.GetDirectionVector(caster.SpellDirectionName);
        var activeTarget = caster.SpellTarget;
        if (activeTarget != null &&
            GodotObject.IsInstanceValid(activeTarget) &&
            activeTarget.IsInsideTree() &&
            activeTarget is ITargetable targetable &&
            targetable.CanBeTargeted)
        {
            var toTarget = activeTarget.GlobalPosition - caster.SpellOrigin.GlobalPosition;
            if (toTarget != Vector2.Zero)
                fireDirection = toTarget.Normalized();
        }

        var projectile = ProjectileScene.Instantiate<Projectile>();
        if (projectile == null)
            return false;

        var parent = caster.SpellOrigin.GetParent();
        if (parent == null)
        {
            projectile.QueueFree();
            return false;
        }

        if (!TrySpendCastMana(caster))
        {
            projectile.QueueFree();
            return false;
        }

        projectile.GlobalPosition = caster.SpellOrigin.GlobalPosition;
        parent.AddChild(projectile);

        var damagePayload = Damage.DuplicateFrom(this);
        damagePayload?.InitializeRuntime((Node)caster.SpellOrigin, damagePayload.ResolveAmount());
        projectile.Initialize(
            fireDirection,
            (Node)caster.SpellOrigin,
            damagePayload,
            ProjectileSpeed,
            ProjectileLifetime,
            ProjectileMaxDistance);

        StartCooldown();
        return true;
    }
}
