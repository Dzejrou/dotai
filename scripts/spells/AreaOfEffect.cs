using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class AreaOfEffect : Area2D
{
    private readonly Dictionary<ulong, Node2D> _occupants = new();
    private readonly List<StatusEffect> _statusTemplates = new();

    private CollisionShape2D _collisionShape;
    private Node2D _damageSource;
    private ulong _damageSourceInstanceId;
    private Faction _sourceFaction = Factions.Enemies;
    private bool _isPreview;
    private bool _runtimeInitialized;
    private bool _pendingInitialOverlapSync;
    private float _elapsedTime;
    private float _nextTickTime;

    [Export]
    public float EffectLifetime { get; set; } = 5.0f;

    [Export]
    public float TickInterval { get; set; } = 1.0f;

    [Export]
    public bool ApplyOnEnter { get; set; } = true;

    [Export]
    public bool ApplyOnTick { get; set; } = true;

    [Export]
    public Color FillColor { get; set; } = new Color(1.0f, 1.0f, 1.0f, 0.0f);

    [Export]
    public Color OutlineColor { get; set; } = new Color(1.0f, 1.0f, 1.0f, 0.0f);

    [Export]
    public float OutlineWidth { get; set; } = 2.0f;

    [Export]
    public Color PreviewFillColor { get; set; } = new Color(1.0f, 1.0f, 1.0f, 0.0f);

    [Export]
    public Color PreviewOutlineColor { get; set; } = new Color(1.0f, 1.0f, 1.0f, 0.0f);

    protected bool IsPreviewMode => _isPreview;
    protected Node2D DamageSourceNode => _damageSource;
    protected ulong DamageSourceInstanceId => _damageSourceInstanceId;
    protected Faction SourceFaction => _sourceFaction;

    public override void _Ready()
    {
        CacheSceneReferences();

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        Monitoring = _runtimeInitialized;
        Monitorable = false;
        CollisionLayer = 0;
        CollisionMask = 1;
        SetPhysicsProcess(_runtimeInitialized);

        OnAreaReady();

        if (!_isPreview && !_runtimeInitialized && GetParent() is not Spell)
            ActivateRuntime();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_runtimeInitialized || _isPreview)
            return;

        if (_pendingInitialOverlapSync)
        {
            SyncTrackedOccupants(applyOnNewOccupants: ApplyOnEnter);
            _pendingInitialOverlapSync = false;
        }

        var deltaSeconds = Math.Max(0.0f, (float)delta);
        _elapsedTime += deltaSeconds;

        var lifetime = Math.Max(0.1f, EffectLifetime);
        var tickInterval = Math.Max(0.1f, TickInterval);
        while (ApplyOnTick && _elapsedTime >= _nextTickTime && _nextTickTime <= lifetime + 0.001f)
        {
            ApplyEffectsToOccupants();
            _nextTickTime += tickInterval;
        }

        if (_elapsedTime >= lifetime)
            QueueFree();
    }

    public virtual void InitializePreview()
    {
        _isPreview = true;
        _runtimeInitialized = false;
        _pendingInitialOverlapSync = false;
        Visible = true;
        Monitoring = false;
        SetPhysicsProcess(false);
        QueueRedraw();
        OnPreviewInitialized();
    }

    public virtual void InitializeRuntime(Node2D damageSource, Faction sourceFaction)
    {
        _isPreview = false;
        _damageSource = damageSource;
        _damageSourceInstanceId = damageSource != null && GodotObject.IsInstanceValid(damageSource)
            ? damageSource.GetInstanceId()
            : 0UL;
        _sourceFaction = sourceFaction ?? Factions.Enemies;
        Visible = true;
        ActivateRuntime();
    }

    public override void _Draw()
    {
        var radius = ResolveFootprintRadius();
        if (radius <= 0.0f)
            return;

        var fillColor = _isPreview ? PreviewFillColor : FillColor;
        var outlineColor = _isPreview ? PreviewOutlineColor : OutlineColor;
        if (fillColor.A > 0.0f)
            DrawCircle(Vector2.Zero, radius, fillColor);

        if (outlineColor.A > 0.0f)
            DrawArc(Vector2.Zero, radius, 0.0f, Mathf.Tau, 48, outlineColor, Math.Max(1.0f, OutlineWidth));
    }

    protected virtual void OnAreaReady()
    {
    }

    protected virtual void OnPreviewInitialized()
    {
    }

    protected virtual void OnRuntimeInitialized()
    {
    }

    protected virtual IEnumerable<StatusEffect> CreateStatusEffectsForTarget(Node2D target)
    {
        foreach (var template in _statusTemplates)
        {
            if (template?.Duplicate() is StatusEffect effect)
                yield return effect;
        }
    }

    protected StatusEffect DuplicateStatusTemplate(string templateName)
    {
        return GetNodeOrNull<StatusEffect>(templateName)?.Duplicate() as StatusEffect;
    }

    private void ActivateRuntime()
    {
        _runtimeInitialized = true;
        _elapsedTime = 0.0f;
        _nextTickTime = Math.Max(0.1f, TickInterval);
        _pendingInitialOverlapSync = true;
        _occupants.Clear();
        Monitoring = true;
        SetPhysicsProcess(true);
        QueueRedraw();
        OnRuntimeInitialized();
    }

    private void ApplyEffectsToOccupants()
    {
        SyncTrackedOccupants(applyOnNewOccupants: false);

        var occupants = new List<Node2D>(_occupants.Values);
        foreach (var target in occupants)
            ApplyEffectsToTarget(target);
    }

    private void ApplyEffectsToTarget(Node2D targetNode)
    {
        if (targetNode == null || !GodotObject.IsInstanceValid(targetNode) || !targetNode.IsInsideTree())
            return;

        var targetFactionState = FactionState.ResolveFor(targetNode);
        if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(_sourceFaction))
            return;

        if (Damage.DuplicateFrom(this) is Damage damagePayload && targetNode is IAttackable attackable)
        {
            damagePayload.InitializeRuntime(ResolveDamageSource(), damagePayload.ResolveAmount());
            attackable.ApplyDamage(damagePayload);
        }

        var controller = ResolveStatusEffectController(targetNode);
        if (controller == null)
            return;

        foreach (var effect in CreateStatusEffectsForTarget(targetNode))
            controller.ApplyStatusEffect(effect, _damageSource, _damageSourceInstanceId);
    }

    private void CacheSceneReferences()
    {
        _collisionShape ??= GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

        _statusTemplates.Clear();
        foreach (var child in GetChildren())
        {
            if (child is StatusEffect statusEffect)
                _statusTemplates.Add(statusEffect);
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!_runtimeInitialized || _isPreview)
            return;

        if (!TryTrackOccupant(body))
            return;

        if (ApplyOnEnter)
            ApplyEffectsToTarget(body);
    }

    private void OnBodyExited(Node2D body)
    {
        if (body == null)
            return;

        _occupants.Remove(body.GetInstanceId());
    }

    private bool TryTrackOccupant(Node2D body)
    {
        if (body == null ||
            !GodotObject.IsInstanceValid(body) ||
            !body.IsInsideTree() ||
            body == _damageSource)
        {
            return false;
        }

        _occupants[body.GetInstanceId()] = body;
        return true;
    }

    private void SyncTrackedOccupants(bool applyOnNewOccupants)
    {
        var seen = new HashSet<ulong>();
        foreach (var body in GetOverlappingBodies())
        {
            if (body is not Node2D node2D)
                continue;

            var instanceId = node2D.GetInstanceId();
            seen.Add(instanceId);

            var wasTracked = _occupants.ContainsKey(instanceId);
            if (!TryTrackOccupant(node2D))
                continue;

            if (applyOnNewOccupants && !wasTracked)
                ApplyEffectsToTarget(node2D);
        }

        var occupantsToRemove = new List<ulong>();
        foreach (var pair in _occupants)
        {
            var target = pair.Value;
            if (target == null ||
                !GodotObject.IsInstanceValid(target) ||
                !target.IsInsideTree() ||
                !seen.Contains(pair.Key))
            {
                occupantsToRemove.Add(pair.Key);
            }
        }

        foreach (var instanceId in occupantsToRemove)
            _occupants.Remove(instanceId);
    }

    private float ResolveFootprintRadius()
    {
        return _collisionShape?.Shape switch
        {
            CircleShape2D circleShape => Math.Max(0.0f, circleShape.Radius),
            RectangleShape2D rectangleShape => Math.Max(rectangleShape.Size.X, rectangleShape.Size.Y) * 0.5f,
            CapsuleShape2D capsuleShape => Math.Max(capsuleShape.Radius, capsuleShape.Height * 0.5f),
            _ => 0.0f,
        };
    }

    private Node ResolveDamageSource()
    {
        return _damageSource != null && GodotObject.IsInstanceValid(_damageSource) ? _damageSource : this;
    }

    private static StatusEffectController ResolveStatusEffectController(Node target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
            return null;

        return target.GetNodeOrNull<StatusEffectController>("StatusEffectController");
    }
}
