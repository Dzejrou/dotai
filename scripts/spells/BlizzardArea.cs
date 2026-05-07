using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class BlizzardArea : AreaOfEffect
{
    private readonly RandomNumberGenerator _random = new();
    private Sprite2D _cloudSprite;
    private float _cloudDriftTimeSeconds;
    private float _cloudDriftPhaseRadians;

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

    public override void _Ready()
    {
        _random.Randomize();
        _cloudDriftPhaseRadians = _random.RandfRange(0.0f, Mathf.Tau);
        base._Ready();
    }

    public override void _Process(double delta)
    {
        if (_cloudSprite == null || !_cloudSprite.Visible)
            return;

        _cloudDriftTimeSeconds += Math.Max(0.0f, (float)delta);
        UpdateCloudPosition();
    }

    protected override void OnAreaReady()
    {
        _cloudSprite ??= GetNodeOrNull<Sprite2D>("Cloud");
        ApplyCloudPresentation();
        SetProcess(_cloudSprite != null);
    }

    protected override void OnRuntimeInitialized()
    {
        ApplyCloudPresentation();
    }

    protected override void OnPreviewInitialized()
    {
        ApplyCloudPresentation();
    }

    protected override IEnumerable<StatusEffect> CreateStatusEffectsForTarget(Node2D target)
    {
        var templateName = _random.Randf() < Math.Clamp(ImmobilizeChance, 0.0f, 1.0f)
            ? "ImmobilizedEffect"
            : "SlowedEffect";

        if (DuplicateStatusTemplate(templateName) is StatusEffect effect)
            yield return effect;
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

    private void UpdateCloudPosition()
    {
        if (_cloudSprite == null)
            return;

        var driftPeriod = Math.Max(0.01f, CloudDriftPeriodSeconds);
        var driftAngle = (_cloudDriftTimeSeconds / driftPeriod) * Mathf.Tau + _cloudDriftPhaseRadians;
        var driftX = Mathf.Sin(driftAngle) * CloudDriftDistance;
        _cloudSprite.Position = new Vector2(driftX, -CloudVerticalOffset);
    }
}
