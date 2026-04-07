using Godot;

public partial class Projectile : Area2D
{
    private static readonly Color DefaultProjectileColor = new(1.0f, 0.45f, 0.1f, 1.0f);

    [Export]
    public float Speed { get; set; } = 280.0f;

    [Export]
    public float Lifetime { get; set; } = 2.5f;

    [Export]
    public float MaxTravelDistance { get; set; } = 320.0f;

    private Vector2 _direction = Vector2.Right;
    private float _lifetimeTimer;
    private float _traveledDistance;
    private Damage _damage;
    private StatusEffect _statusEffect;
    private Node _source;
    private Color _color = DefaultProjectileColor;
    private bool _isActive;
    private bool _hasHitTarget;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        AreaEntered += OnAreaEntered;
        Monitoring = true;
        Monitorable = true;
        CollisionLayer = 1;
        CollisionMask = 1;
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
        Color? overrideColor = null,
        float? overrideSpeed = null,
        float? overrideLifetime = null,
        float? overrideMaxTravelDistance = null)
    {
        _source = source;
        _damage = damage;
        _statusEffect = statusEffect;
        _color = overrideColor ?? DefaultProjectileColor;
        _direction = direction.Length() > 0.0f ? direction.Normalized() : Vector2.Right;
        if (overrideSpeed.HasValue)
            Speed = Mathf.Max(0.0f, overrideSpeed.Value);
        if (overrideLifetime.HasValue)
            _lifetimeTimer = Mathf.Max(0.05f, overrideLifetime.Value);
        else
            _lifetimeTimer = Mathf.Max(0.05f, Lifetime);

        if (overrideMaxTravelDistance.HasValue)
            MaxTravelDistance = Mathf.Max(0.0f, overrideMaxTravelDistance.Value);

        _traveledDistance = 0.0f;
        _hasHitTarget = false;
        _isActive = true;
        SetPhysicsProcess(true);

        if (_damage != null)
            AddChild(_damage);

        if (_statusEffect != null)
            AddChild(_statusEffect);

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 4.0f, _color);
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
}
