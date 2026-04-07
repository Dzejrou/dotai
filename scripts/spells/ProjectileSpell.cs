using Godot;

using System;

[GlobalClass]
public abstract partial class ProjectileSpell : Spell
{
    private readonly RandomNumberGenerator _random = new();

    [Export]
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public float ProjectileSpeed { get; set; } = 280.0f;

    [Export]
    public float ProjectileLifetime { get; set; } = 2.5f;

    [Export]
    public float ProjectileMaxDistance { get; set; } = 320.0f;

    public override void _Ready()
    {
        base._Ready();
        _random.Randomize();
    }

    public override bool CanCast(ISpellCaster caster)
    {
        return base.CanCast(caster) && ProjectileScene != null;
    }

    public override bool TryCast(ISpellCaster caster)
    {
        if (!CanCast(caster))
            return false;

        var projectile = ProjectileScene.Instantiate<Projectile>();
        if (projectile == null)
            return false;

        var spellOrigin = caster.SpellOrigin;
        var parent = spellOrigin.GetParent();
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

        projectile.GlobalPosition = spellOrigin.GlobalPosition;
        parent.AddChild(projectile);
        projectile.Initialize(
            ResolveProjectileDirection(caster),
            (Node)spellOrigin,
            CreateDamagePayload(caster),
            CreateStatusEffectPayload(),
            ProjectileSpeed,
            ProjectileLifetime,
            ProjectileMaxDistance);

        StartCooldown();
        return true;
    }

    protected virtual Vector2 ResolveProjectileDirection(ISpellCaster caster)
    {
        var fireDirection = caster.SpellDirection;
        if (fireDirection == Vector2.Zero)
            fireDirection = DirectionHelper.GetDirectionVector(caster.SpellDirectionName);

        var activeTarget = caster.SpellTarget;
        if (activeTarget == null ||
            !GodotObject.IsInstanceValid(activeTarget) ||
            !activeTarget.IsInsideTree() ||
            activeTarget is not ITargetable targetable ||
            !targetable.CanBeTargeted)
        {
            return fireDirection;
        }

        var toTarget = activeTarget.GlobalPosition - caster.SpellOrigin.GlobalPosition;
        return toTarget != Vector2.Zero ? toTarget.Normalized() : fireDirection;
    }

    protected virtual Damage CreateDamagePayload(ISpellCaster caster)
    {
        var damageTemplate = GetNodeOrNull<Damage>("Damage");
        if (damageTemplate?.Duplicate() is not Damage damagePayload)
            return null;

        damagePayload.InitializeRuntime((Node)caster.SpellOrigin, Math.Max(1, ResolveDamage(damageTemplate)));
        return damagePayload;
    }

    protected virtual int ResolveDamage(Damage damageTemplate)
    {
        return damageTemplate?.ResolveAmount() ?? 0;
    }

    protected virtual StatusEffect CreateStatusEffectPayload()
    {
        var procChance = Math.Clamp(ResolveStatusProcChance(), 0.0f, 1.0f);
        if (procChance <= 0.0f || _random.Randf() >= procChance)
            return null;

        return GetNodeOrNull<StatusEffect>(ResolveStatusEffectTemplateName())?.Duplicate() as StatusEffect;
    }

    protected virtual string ResolveStatusEffectTemplateName()
    {
        return string.Empty;
    }

    protected virtual float ResolveStatusProcChance()
    {
        return 0.0f;
    }
}
