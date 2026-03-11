using Godot;

public partial class Projectile : Area2D
{
    [Export]
    public float Speed { get; set; } = 280.0f;

    [Export]
    public int Damage { get; set; } = 3;

    [Export]
    public float Lifetime { get; set; } = 2.5f;

    [Export]
    public float MaxTravelDistance { get; set; } = 320.0f;

    [Export]
    public StringName TargetGroup { get; set; } = CombatGroups.Enemies;

    private Vector2 _direction = Vector2.Right;
    private float _lifetimeTimer;
    private float _traveledDistance;
    private Node _source;
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
        int? overrideDamage = null,
        float? overrideSpeed = null,
        float? overrideLifetime = null,
        float? overrideMaxTravelDistance = null,
        string overrideTargetGroup = "")
    {
        _source = source;
        _direction = direction.Length() > 0.0f ? direction.Normalized() : Vector2.Right;
        if (overrideDamage.HasValue && overrideDamage.Value > 0)
            Damage = overrideDamage.Value;
        if (overrideSpeed.HasValue)
            Speed = Mathf.Max(0.0f, overrideSpeed.Value);
        if (overrideLifetime.HasValue)
            _lifetimeTimer = Mathf.Max(0.05f, overrideLifetime.Value);
        else
            _lifetimeTimer = Mathf.Max(0.05f, Lifetime);

        if (overrideMaxTravelDistance.HasValue)
            MaxTravelDistance = Mathf.Max(0.0f, overrideMaxTravelDistance.Value);
        if (!string.IsNullOrWhiteSpace(overrideTargetGroup))
            TargetGroup = overrideTargetGroup;

        _traveledDistance = 0.0f;
        _hasHitTarget = false;
        _isActive = true;
        SetPhysicsProcess(true);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 4.0f, new Color(1.0f, 0.45f, 0.1f, 1.0f));
    }

    private void TryDamageTarget(Node2D targetNode)
    {
        if (_hasHitTarget || !IsInstanceValid(targetNode))
            return;

        if (_source != null && _source == targetNode)
            return;

        if (!string.IsNullOrWhiteSpace(TargetGroup))
        {
            if (!targetNode.IsInGroup(TargetGroup))
                return;
        }

        if (targetNode is not IAttackable attackable || targetNode is not ITargetable targetable || !targetable.CanBeTargeted)
            return;

        _hasHitTarget = true;
        attackable.ApplyDamage(new DamageInfo(Damage, _source));
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
}
