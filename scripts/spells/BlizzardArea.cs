using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class BlizzardArea : AreaOfEffect
{
    private sealed class IceBallVisual
    {
        public Sprite2D Sprite { get; init; }
        public float FallSpeed { get; set; }
    }

    private const string CloudNodeName = "Cloud";
    private const string IceBallNodePrefix = "__BlizzardIceBall";

    private readonly RandomNumberGenerator _random = new();
    private readonly List<IceBallVisual> _iceBallVisuals = new();
    private Sprite2D _cloudSprite;
    private float _cloudDriftTimeSeconds;
    private float _cloudDriftPhaseRadians;
    private bool _iceBallPoolActive;

    [Export]
    public float ImmobilizeChance { get; set; } = 0.33f;

    [Export]
    public float CloudAlpha { get; set; } = 0.8f;

    [Export]
    public int CloudZIndex { get; set; } = 100;

    [Export]
    public float CloudVerticalOffset { get; set; } = 256.0f;

    [Export]
    public float CloudDriftDistance { get; set; } = 24.0f;

    [Export]
    public float CloudDriftPeriodSeconds { get; set; } = 3.0f;

    [Export]
    public Texture2D IceBallTexture { get; set; }

    [Export]
    public int IceBallCount { get; set; } = 18;

    [Export]
    public float IceBallScale { get; set; } = 0.6f;

    [Export]
    public float IceBallScaleVariance { get; set; } = 0.1f;

    [Export]
    public float IceBallXRange { get; set; } = 320.0f;

    [Export]
    public float IceBallStartYOffset { get; set; } = -220.0f;

    [Export]
    public float IceBallEndY { get; set; } = 0.0f;

    [Export]
    public float IceBallMinFallSpeed { get; set; } = 120.0f;

    [Export]
    public float IceBallMaxFallSpeed { get; set; } = 220.0f;

    [Export]
    public int IceBallZIndex { get; set; } = 96;

    [Export]
    public float IceBallAlpha { get; set; } = 0.9f;

    public override void _Ready()
    {
        _random.Randomize();
        _cloudDriftPhaseRadians = _random.RandfRange(0.0f, Mathf.Tau);
        base._Ready();
    }

    public override void _Process(double delta)
    {
        var deltaSeconds = Math.Max(0.0f, (float)delta);
        _cloudDriftTimeSeconds += deltaSeconds;
        UpdateCloudPosition();

        if (!_iceBallPoolActive)
            return;

        ReconcileIceBallPool();
        ApplyIceBallPresentation(resetExistingPositions: false);
        UpdateIceBalls(deltaSeconds);
    }

    protected override void OnAreaReady()
    {
        _cloudSprite ??= GetNodeOrNull<Sprite2D>(CloudNodeName);

        if (!_iceBallPoolActive)
            ClearIceBallPool();

        ApplyCloudPresentation();
        SetProcess(_cloudSprite != null);
    }

    protected override void OnRuntimeInitialized()
    {
        _iceBallPoolActive = true;
        ApplyCloudPresentation();
        ReconcileIceBallPool();
        ApplyIceBallPresentation(resetExistingPositions: true);
    }

    protected override void OnPreviewInitialized()
    {
        _iceBallPoolActive = false;
        ClearIceBallPool();
        ApplyCloudPresentation();
    }

    protected override IEnumerable<StatusEffect> CreateStatusEffectsForTarget(Node2D target)
    {
        var templateName = _random.Randf() < Math.Clamp(ImmobilizeChance, 0.0f, 1.0f)
            ? "ImmobilizedEffect"
            : "SlowedEffect";

        if (DuplicateStatusTemplate(templateName) is StatusEffect effect)
            yield return effect;

        if (DuplicateStatusTemplate("FrozenEffect") is StatusEffect frozen)
            yield return frozen;
    }

    private void ApplyCloudPresentation()
    {
        if (_cloudSprite == null)
            return;

        _cloudSprite.Visible = true;
        _cloudSprite.ZAsRelative = false;
        _cloudSprite.ZIndex = CloudZIndex;
        _cloudSprite.Modulate = new Color(1.0f, 1.0f, 1.0f, Math.Clamp(CloudAlpha, 0.0f, 1.0f));
        UpdateCloudPosition();
    }

    private void ReconcileIceBallPool()
    {
        var targetCount = Math.Max(0, IceBallCount);
        while (_iceBallVisuals.Count < targetCount)
            _iceBallVisuals.Add(CreateIceBallVisual(_iceBallVisuals.Count));

        while (_iceBallVisuals.Count > targetCount)
        {
            var lastIndex = _iceBallVisuals.Count - 1;
            var visual = _iceBallVisuals[lastIndex];
            if (visual.Sprite != null && GodotObject.IsInstanceValid(visual.Sprite))
                visual.Sprite.QueueFree();

            _iceBallVisuals.RemoveAt(lastIndex);
        }
    }

    private void ClearIceBallPool()
    {
        foreach (var visual in _iceBallVisuals)
        {
            if (visual.Sprite != null && GodotObject.IsInstanceValid(visual.Sprite))
                visual.Sprite.QueueFree();
        }

        _iceBallVisuals.Clear();
    }

    private IceBallVisual CreateIceBallVisual(int index)
    {
        var sprite = new Sprite2D
        {
            Name = $"{IceBallNodePrefix}{index}",
            Centered = true,
            ZAsRelative = false,
        };
        AddChild(sprite);

        var visual = new IceBallVisual
        {
            Sprite = sprite,
        };

        ResetIceBall(visual, randomizeYAcrossColumn: true);
        return visual;
    }

    private void ApplyIceBallPresentation(bool resetExistingPositions)
    {
        foreach (var visual in _iceBallVisuals)
        {
            if (visual.Sprite == null || !GodotObject.IsInstanceValid(visual.Sprite))
                continue;

            visual.Sprite.Visible = IceBallTexture != null;
            visual.Sprite.Texture = IceBallTexture;
            visual.Sprite.ZIndex = IceBallZIndex;
            visual.Sprite.Modulate = new Color(1.0f, 1.0f, 1.0f, Math.Clamp(IceBallAlpha, 0.0f, 1.0f));

            if (resetExistingPositions)
                ResetIceBall(visual, randomizeYAcrossColumn: true);
        }
    }

    private void UpdateIceBalls(float deltaSeconds)
    {
        foreach (var visual in _iceBallVisuals)
        {
            if (visual.Sprite == null || !GodotObject.IsInstanceValid(visual.Sprite) || !visual.Sprite.Visible)
                continue;

            visual.Sprite.Position += new Vector2(0.0f, visual.FallSpeed * deltaSeconds);
            if (visual.Sprite.Position.Y >= IceBallEndY)
                ResetIceBall(visual, randomizeYAcrossColumn: false);
        }
    }

    private void UpdateCloudPosition()
    {
        if (_cloudSprite == null)
            return;

        var driftPeriod = Math.Max(0.01f, CloudDriftPeriodSeconds);
        var driftAngle = (_cloudDriftTimeSeconds / driftPeriod) * Mathf.Tau + _cloudDriftPhaseRadians;
        var driftX = Mathf.Sin(driftAngle) * CloudDriftDistance;
        _cloudSprite.Position = new Vector2(driftX, -CloudVerticalOffset);
    }

    private void ResetIceBall(IceBallVisual visual, bool randomizeYAcrossColumn)
    {
        if (visual?.Sprite == null || !GodotObject.IsInstanceValid(visual.Sprite))
            return;

        var minScale = Math.Max(0.01f, IceBallScale - Math.Abs(IceBallScaleVariance));
        var maxScale = Math.Max(minScale, IceBallScale + Math.Abs(IceBallScaleVariance));
        var minSpeed = Math.Max(1.0f, Math.Min(IceBallMinFallSpeed, IceBallMaxFallSpeed));
        var maxSpeed = Math.Max(minSpeed, Math.Max(IceBallMinFallSpeed, IceBallMaxFallSpeed));
        var xHalfRange = Math.Max(0.0f, IceBallXRange * 0.5f);
        var yMin = Math.Min(IceBallStartYOffset, IceBallEndY);
        var yMax = Math.Max(IceBallStartYOffset, IceBallEndY);
        var spawnY = randomizeYAcrossColumn
            ? _random.RandfRange(yMin, yMax)
            : IceBallStartYOffset;

        visual.Sprite.Position = new Vector2(_random.RandfRange(-xHalfRange, xHalfRange), spawnY);

        var spriteScale = _random.RandfRange(minScale, maxScale);
        visual.Sprite.Scale = Vector2.One * spriteScale;
        visual.FallSpeed = _random.RandfRange(minSpeed, maxSpeed);
    }
}
