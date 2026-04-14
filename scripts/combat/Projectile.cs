using Godot;
using System;

public partial class Projectile : Area2D
{
    private const string CollisionShapeNodeName = "CollisionShape2D";
    private const string OmniSpriteNodeName = "OmniSprite";
    private static readonly StringName DefaultAnimationName = "default";

    [Export]
    public float Speed { get; set; } = 280.0f;

    [Export]
    public float Lifetime { get; set; } = 2.5f;

    [Export]
    public float MaxTravelDistance { get; set; } = 320.0f;

    [Export]
    public float CollisionRadius { get; set; } = 4.0f;

    private Vector2 _direction = Vector2.Right;
    private float _lifetimeTimer;
    private float _traveledDistance;
    private Damage _damage;
    private StatusEffect _statusEffect;
    private Node _source;
    private bool _isActive;
    private bool _hasHitTarget;
    private CollisionShape2D _collisionShape;
    private OmniSprite _omniSprite;
    private SpriteFrames _configuredVisualFrames;
    private DirectionalTextureSet _configuredDirectionalTextures;
    private string _configuredAnimationName = DefaultAnimationName.ToString();

    public override void _Ready()
    {
        CacheSceneReferences();
        BodyEntered += OnBodyEntered;
        AreaEntered += OnAreaEntered;
        Monitoring = true;
        Monitorable = true;
        CollisionLayer = 1;
        CollisionMask = 1;
        CollisionRadius = Math.Max(0.0f, CollisionRadius);
        ApplyCollisionRadius(CollisionRadius);
        ConfigureVisual(_configuredVisualFrames, _configuredDirectionalTextures, _configuredAnimationName);
        _lifetimeTimer = Mathf.Max(0.05f, Lifetime);
        SetPhysicsProcess(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isActive)
            return;

        var frameDelta = (float)delta;
        var movement = _direction * Speed * frameDelta;
        GlobalPosition += movement;
        _traveledDistance += movement.Length();

        _lifetimeTimer -= frameDelta;
        if (_lifetimeTimer <= 0.0f || (_traveledDistance >= MaxTravelDistance))
        {
            CallDeferred(nameof(Despawn));
            return;
        }
    }

    public void Initialize(
        Vector2 direction,
        Node source,
        Damage damage = null,
        StatusEffect statusEffect = null,
        SpriteFrames overrideVisualFrames = null,
        DirectionalTextureSet overrideDirectionalTextures = null,
        string overrideAnimationName = null,
        float? overrideSpeed = null,
        float? overrideLifetime = null,
        float? overrideMaxTravelDistance = null,
        float? overrideCollisionRadius = null)
    {
        _source = source;
        _damage = damage;
        _statusEffect = statusEffect;
        _configuredVisualFrames = overrideVisualFrames;
        _configuredDirectionalTextures = overrideDirectionalTextures;
        _configuredAnimationName = string.IsNullOrEmpty(overrideAnimationName)
            ? DefaultAnimationName.ToString()
            : overrideAnimationName;
        _direction = direction.Length() > 0.0f ? direction.Normalized() : Vector2.Right;
        if (overrideSpeed.HasValue)
            Speed = Mathf.Max(0.0f, overrideSpeed.Value);
        if (overrideLifetime.HasValue)
            _lifetimeTimer = Mathf.Max(0.05f, overrideLifetime.Value);
        else
            _lifetimeTimer = Mathf.Max(0.05f, Lifetime);

        if (overrideMaxTravelDistance.HasValue)
            MaxTravelDistance = Mathf.Max(0.0f, overrideMaxTravelDistance.Value);

        if (overrideCollisionRadius.HasValue)
            ApplyCollisionRadius(Mathf.Max(0.0f, overrideCollisionRadius.Value));
        else
            ApplyCollisionRadius(CollisionRadius);

        _traveledDistance = 0.0f;
        _hasHitTarget = false;
        _isActive = true;
        SetPhysicsProcess(true);

        ConfigureVisual(_configuredVisualFrames, _configuredDirectionalTextures, _configuredAnimationName);

        if (_damage != null)
            AddChild(_damage);

        if (_statusEffect != null)
            AddChild(_statusEffect);

        QueueRedraw();
    }

    private void TryDamageTarget(Node2D targetNode)
    {
        if (_hasHitTarget || !IsInstanceValid(targetNode))
            return;

        if (_source != null && _source == targetNode)
            return;

        if (!TargetingHelper.CanProjectileHitTarget(_source, targetNode))
            return;

        _hasHitTarget = true;
        if (_damage != null)
        {
            var attackable = (IAttackable)targetNode;
            attackable.ApplyDamage(_damage);
        }

        if (_statusEffect != null)
        {
            var controller = ResolveStatusEffectController(targetNode);
            controller?.ApplyStatusEffect(_statusEffect, _source as Node2D, ResolveSourceInstanceId(_source));
        }

        CallDeferred(nameof(Despawn));
    }

    private void Despawn()
    {
        if (!IsInstanceValid(this))
            return;

        QueueFree();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!_isActive)
            return;

        TryDamageTarget(body);
    }

    private void OnAreaEntered(Area2D area)
    {
        if (!_isActive || area == null)
            return;

        if (area == this)
            return;

        TryDamageTarget(area);
    }

    private static StatusEffectController ResolveStatusEffectController(Node target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return null;

        return target.GetNodeOrNull<StatusEffectController>("StatusEffectController");
    }

    private static ulong ResolveSourceInstanceId(Node source)
    {
        return source != null && GodotObject.IsInstanceValid(source) ? source.GetInstanceId() : 0UL;
    }

    private void CacheSceneReferences()
    {
        _collisionShape ??= GetNodeOrNull<CollisionShape2D>(CollisionShapeNodeName);
        _omniSprite ??= GetNodeOrNull<OmniSprite>(OmniSpriteNodeName);
    }

    private void ApplyCollisionRadius(float radius)
    {
        CacheSceneReferences();

        CollisionRadius = Mathf.Max(0.0f, radius);
        if (_collisionShape?.Shape is CircleShape2D circleShape)
            circleShape.Radius = CollisionRadius;
    }

    private void ConfigureVisual(SpriteFrames spriteFrames, DirectionalTextureSet directionalTextures, string animationName)
    {
        CacheSceneReferences();

        if (_omniSprite == null)
            return;

        var directionalTexture = directionalTextures?.ResolveTexture(_direction);
        if (directionalTexture != null)
        {
            _omniSprite.SetAnimatedSpriteFrames(null, animationName);
            _omniSprite.SetStaticTexture(directionalTexture);
            return;
        }

        var resolvedAnimationName = string.IsNullOrEmpty(animationName)
            ? DefaultAnimationName.ToString()
            : animationName;

        _omniSprite.SetStaticTexture(null);
        _omniSprite.SetAnimatedSpriteFrames(spriteFrames, resolvedAnimationName);
        _omniSprite.TryPlay(resolvedAnimationName);
    }
}
