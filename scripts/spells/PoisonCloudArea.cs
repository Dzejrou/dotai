using Godot;

using System;

[GlobalClass]
public partial class PoisonCloudArea : Node2D
{
    private static readonly StringName DefaultAnimationName = "default";

    private Node2D _damageSource;
    private Faction _sourceFaction = Factions.Enemies;
    private float _elapsedTime;
    private float _nextTickTime;
    private AnimatedSprite2D _sprite;

    [Export]
    public float CloudRadius { get; set; } = 48.0f;

    [Export]
    public float CloudLifetime { get; set; } = 14.0f;

    [Export]
    public float PoisonDuration { get; set; } = 10.0f;

    [Export]
    public float PoisonTickInterval { get; set; } = 2.0f;

    [Export]
    public int PoisonDamagePerTick { get; set; } = 5;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
            _sprite.Play(DefaultAnimationName);
    }

    public void Initialize(Node2D damageSource, Faction sourceFaction)
    {
        _damageSource = damageSource;
        _sourceFaction = sourceFaction ?? Factions.Enemies;
        _elapsedTime = 0.0f;
        _nextTickTime = 0.0f;
        if (_sprite != null && _sprite.SpriteFrames != null)
            _sprite.Play(DefaultAnimationName);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        var deltaSeconds = Math.Max(0.0f, (float)delta);
        _elapsedTime += deltaSeconds;

        var lifetime = Math.Max(0.1f, CloudLifetime);
        var tickInterval = Math.Max(0.1f, PoisonTickInterval);

        while (_elapsedTime >= _nextTickTime && _nextTickTime <= lifetime + 0.001f)
        {
            ApplyTickPoison();
            _nextTickTime += tickInterval;
        }

        if (_elapsedTime >= lifetime)
            QueueFree();
    }

    private void ApplyTickPoison()
    {
        var poisonDamagePerTick = Math.Max(0, PoisonDamagePerTick);
        if (_sourceFaction == null || poisonDamagePerTick <= 0)
            return;

        var radius = Math.Max(1.0f, CloudRadius);
        var poisonDuration = Math.Max(0.1f, PoisonDuration);
        var poisonTickInterval = Math.Max(0.1f, PoisonTickInterval);

        foreach (var target in TargetingHelper.EnumerateCandidateTargets(this))
        {
            if (target is not IAttackable)
                continue;

            var targetFactionState = FactionState.ResolveFor(target);
            if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(_sourceFaction))
                continue;

            if (GlobalPosition.DistanceTo(target.GlobalPosition) > radius)
                continue;

            var controller = ResolveStatusEffectController(target);
            if (controller == null)
                continue;

            var effect = new PoisonedEffect
            {
                DurationSeconds = poisonDuration,
                TickIntervalSeconds = poisonTickInterval,
                DamagePerTick = poisonDamagePerTick,
            };
            controller.ApplyStatusEffect(effect, _damageSource);
        }
    }

    private static StatusEffectController ResolveStatusEffectController(Node target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return null;

        var controller = target.GetNodeOrNull<StatusEffectController>("StatusEffectController");
        if (controller != null)
            return controller;

        return null;
    }
}
