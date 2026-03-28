using Godot;

using System;

[GlobalClass]
public partial class RingOfFireArea : Node2D
{
    private bool _isPreview;
    private Node _damageSource;
    private Faction _sourceFaction;
    private float _radius = 48.0f;
    private float _duration = 5.0f;
    private float _tickInterval = 1.0f;
    private float _elapsedTime;
    private float _nextTickTime = 1.0f;
    private int _damagePerTick = 6;

    [Export]
    public Color FillColor { get; set; } = new Color(1.0f, 0.45f, 0.08f, 0.32f);

    [Export]
    public Color OutlineColor { get; set; } = new Color(1.0f, 0.62f, 0.14f, 0.9f);

    [Export]
    public float OutlineWidth { get; set; } = 2.0f;

    [Export]
    public Color PreviewFillColor { get; set; } = new Color(1.0f, 0.45f, 0.08f, 0.14f);

    [Export]
    public Color PreviewOutlineColor { get; set; } = new Color(1.0f, 0.62f, 0.14f, 0.45f);

    public void InitializePreview(float radius)
    {
        _isPreview = true;
        _radius = Math.Max(1.0f, radius);
        QueueRedraw();
    }

    public void Initialize(
        Node damageSource,
        Faction sourceFaction,
        float radius,
        float duration,
        float tickInterval,
        int damagePerTick)
    {
        _isPreview = false;
        _damageSource = damageSource;
        _sourceFaction = sourceFaction;
        _radius = Math.Max(1.0f, radius);
        _duration = Math.Max(0.1f, duration);
        _tickInterval = Math.Max(0.1f, tickInterval);
        _damagePerTick = Math.Max(0, damagePerTick);
        _elapsedTime = 0.0f;
        _nextTickTime = _tickInterval;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_isPreview)
            return;

        var deltaSeconds = Math.Max(0.0f, (float)delta);
        _elapsedTime += deltaSeconds;

        while (_elapsedTime >= _nextTickTime && _nextTickTime <= _duration + 0.001f)
        {
            ApplyTickDamage();
            _nextTickTime += _tickInterval;
        }

        if (_elapsedTime >= _duration)
            QueueFree();
    }

    public override void _Draw()
    {
        var fillColor = _isPreview ? PreviewFillColor : FillColor;
        var outlineColor = _isPreview ? PreviewOutlineColor : OutlineColor;
        DrawCircle(Vector2.Zero, _radius, fillColor);
        DrawArc(Vector2.Zero, _radius, 0.0f, Mathf.Tau, 48, outlineColor, Math.Max(1.0f, OutlineWidth));
    }

    private void ApplyTickDamage()
    {
        if (_sourceFaction == null || _damagePerTick <= 0)
            return;

        foreach (var target in TargetingHelper.EnumerateCandidateTargets(this))
        {
            if (target is not IAttackable attackable)
                continue;

            var targetFactionState = FactionState.ResolveFor(target);
            if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(_sourceFaction))
                continue;

            if (GlobalPosition.DistanceTo(target.GlobalPosition) > _radius)
                continue;

            attackable.ApplyDamage(new DamageInfo(_damagePerTick, ResolveDamageSource()));
        }
    }

    private Node ResolveDamageSource()
    {
        return _damageSource != null && GodotObject.IsInstanceValid(_damageSource) ? _damageSource : this;
    }
}
