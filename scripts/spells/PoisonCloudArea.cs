using Godot;

using System;

[GlobalClass]
public partial class PoisonCloudArea : Area2D
{
    private static readonly StringName DefaultAnimationName = "default";

    private Node2D _damageSource;
    private ulong _damageSourceInstanceId;
    private Faction _sourceFaction = Factions.Enemies;
    private float _elapsedTime;
    private float _nextTickTime;
    private AnimatedSprite2D _sprite;
    private StatusEffect _statusEffectTemplate;

    [Export]
    public float CloudLifetime { get; set; } = 14.0f;

    public override void _Ready()
    {
        CacheSceneReferences();

        BodyEntered += OnBodyEntered;
        if (_sprite != null)
            _sprite.Play(DefaultAnimationName);

        if (_statusEffectTemplate == null)
            GD.PushError($"{GetPath()}: PoisonCloudArea requires a StatusEffect child template.");
    }

    public void Initialize(Node2D damageSource, Faction sourceFaction)
    {
        CacheSceneReferences();
        Visible = true;
        ProcessMode = ProcessModeEnum.Inherit;
        SetProcess(true);
        SetPhysicsProcess(true);
        Monitoring = true;
        Monitorable = false;
        CollisionLayer = 1;
        CollisionMask = 1;
        if (_sprite != null)
        {
            _sprite.Visible = true;
            _sprite.Play(DefaultAnimationName);
        }

        _damageSource = damageSource;
        _damageSourceInstanceId = damageSource != null && GodotObject.IsInstanceValid(damageSource)
            ? damageSource.GetInstanceId()
            : 0UL;
        _sourceFaction = sourceFaction ?? Factions.Enemies;
        _elapsedTime = 0.0f;
        _nextTickTime = GetTickInterval();
    }

    public override void _Process(double delta)
    {
        var deltaSeconds = Math.Max(0.0f, (float)delta);
        _elapsedTime += deltaSeconds;

        var lifetime = Math.Max(0.1f, CloudLifetime);
        var tickInterval = GetTickInterval();

        while (_elapsedTime >= _nextTickTime && _nextTickTime <= lifetime + 0.001f)
        {
            ApplyTickPoison();
            _nextTickTime += tickInterval;
        }

        if (_elapsedTime >= lifetime)
            QueueFree();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body == null || !GodotObject.IsInstanceValid(body) || !body.IsInsideTree())
            return;

        ApplyPoisonToTarget(body);
    }

    private void ApplyTickPoison()
    {
        foreach (var target in GetOverlappingBodies())
            ApplyPoisonToTarget(target);
    }

    private void ApplyPoisonToTarget(Node target)
    {
        if (target is not IAttackable)
            return;

        var targetFactionState = FactionState.ResolveFor(target);
        if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(_sourceFaction))
            return;

        var targetNode = target as Node2D;
        if (targetNode == null || !GodotObject.IsInstanceValid(targetNode) || !targetNode.IsInsideTree())
            return;

        var controller = ResolveStatusEffectController(target);
        if (controller == null || _statusEffectTemplate == null)
            return;

        var effect = _statusEffectTemplate.Duplicate() as StatusEffect;
        if (effect == null)
            return;

        controller.ApplyStatusEffect(effect, _damageSource, _damageSourceInstanceId);
    }

    private void CacheSceneReferences()
    {
        _sprite ??= GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _statusEffectTemplate ??= FindStatusEffectTemplate();
    }

    private float GetTickInterval()
    {
        if (_statusEffectTemplate == null)
            return 0.1f;

        return Math.Max(0.1f, _statusEffectTemplate.TickIntervalSeconds);
    }

    private StatusEffect FindStatusEffectTemplate()
    {
        foreach (var child in GetChildren())
        {
            if (child is StatusEffect statusEffect)
                return statusEffect;
        }

        return null;
    }

    private static StatusEffectController ResolveStatusEffectController(Node target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return null;

        return target.GetNodeOrNull<StatusEffectController>("StatusEffectController");
    }
}
