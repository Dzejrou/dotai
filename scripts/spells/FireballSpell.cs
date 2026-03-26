using Godot;

using System;

[GlobalClass]
public partial class FireballSpell : Spell
{
    [Export]
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public int ManaCost { get; set; } = 0;

    [Export]
    public float ProjectileSpeed { get; set; } = 280.0f;

    [Export]
    public int ProjectileDamage { get; set; } = 4;

    [Export]
    public float ProjectileLifetime { get; set; } = 2.5f;

    [Export]
    public float ProjectileMaxDistance { get; set; } = 320.0f;

    public override bool TryCast(ISpellCaster caster)
    {
        if (caster == null || !caster.CanCastSpells || caster.SpellOrigin == null || ProjectileScene == null)
            return false;

        var manaState = caster.ManaState;
        if (manaState == null)
            return false;

        if (!manaState.TrySpend(Math.Max(0, ManaCost)))
            return false;

        if (ManaCost > 0)
            caster.NotifyManaChanged();

        var fireDirection = DirectionHelper.GetDirectionVector(caster.SpellDirectionName);
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

        projectile.GlobalPosition = caster.SpellOrigin.GlobalPosition;
        parent.AddChild(projectile);
        projectile.Initialize(
            fireDirection,
            (Node)caster.SpellOrigin,
            ProjectileDamage,
            ProjectileSpeed,
            ProjectileLifetime,
            ProjectileMaxDistance);

        return true;
    }
}
