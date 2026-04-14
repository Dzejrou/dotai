using Godot;
using System;

[GlobalClass]
public abstract partial class ProjectileSpell : Spell
{
    private readonly RandomNumberGenerator _random = new();
    private static readonly StringName DefaultProjectileAnimationName = "default";

    [Export]
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public float ProjectileSpeed { get; set; } = 280.0f;

    [Export]
    public float ProjectileLifetime { get; set; } = 2.5f;

    [Export]
    public float ProjectileMaxDistance { get; set; } = 320.0f;

    [Export]
    public float ProjectileCollisionRadius { get; set; } = 32.0f;

    [Export]
    public SpriteFrames ProjectileVisualFrames { get; set; }

    [Export]
    public StringName ProjectileAnimationName { get; set; } = DefaultProjectileAnimationName;

    public override void _Ready()
    {
        base._Ready();
        _random.Randomize();
    }

    public override bool CanCast(ISpellCaster caster, SpellCastRequest request)
    {
        return base.CanCast(caster, request) && ProjectileScene != null;
    }

    public override bool TryCast(ISpellCaster caster, SpellCastRequest request)
    {
        if (!CanCast(caster, request))
        {
            if (!TryResolveProjectileDirection(caster, request, out _))
                return LogMissingCastRequestData("Projectile spell requires a target node, target position, or direction.");

            return false;
        }

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
        if (!TryResolveProjectileDirection(caster, request, out var projectileDirection))
        {
            projectile.QueueFree();
            return LogMissingCastRequestData("Projectile spell requires a target node, target position, or direction.");
        }

        projectile.Initialize(
            projectileDirection,
            (Node)spellOrigin,
            CreateDamagePayload(caster),
            CreateStatusEffectPayload(),
            overrideVisualFrames: ProjectileVisualFrames,
            overrideAnimationName: ProjectileAnimationName.ToString(),
            overrideSpeed: ProjectileSpeed,
            overrideLifetime: ProjectileLifetime,
            overrideMaxTravelDistance: ProjectileMaxDistance,
            overrideCollisionRadius: ProjectileCollisionRadius);

        StartCooldown();
        return true;
    }

    protected virtual bool TryResolveProjectileDirection(
        ISpellCaster caster,
        SpellCastRequest request,
        out Vector2 projectileDirection)
    {
        projectileDirection = Vector2.Zero;

        if (request == null)
            return false;

        if (request.TryResolveTargetNode(out var activeTarget))
        {
            if (activeTarget is ITargetable targetable && targetable.CanBeTargeted)
            {
                var toTarget = activeTarget.GlobalPosition - caster.SpellOrigin.GlobalPosition;
                if (toTarget != Vector2.Zero)
                {
                    projectileDirection = toTarget.Normalized();
                    return true;
                }
            }
        }

        if (request.Direction.HasValue && request.Direction.Value != Vector2.Zero)
        {
            projectileDirection = request.Direction.Value.Normalized();
            return true;
        }

        return false;
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
