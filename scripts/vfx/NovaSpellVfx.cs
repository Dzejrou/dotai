using Godot;

using System;

[GlobalClass]
public partial class NovaSpellVfx : Node2D
{
    private static readonly StringName DefaultAnimationName = "default";
    private const string OmniSpriteNodeName = "OmniSprite";

    [Export]
    public float Duration { get; set; } = 0.28f;

    [Export]
    public float RadiusScale { get; set; } = 1.0f;

    [Export]
    public StringName AnimationName { get; set; } = DefaultAnimationName;

    [Export]
    public float VisualDiameterPixels { get; set; } = 128.0f;

    [Export]
    public float StartScaleMultiplier { get; set; } = 0.2f;

    [Export]
    public float EndScaleMultiplier { get; set; } = 1.0f;

    [Export]
    public float LineThickness { get; set; } = 6.0f;

    [Export]
    public Color RingColor { get; set; } = new Color(1.0f, 0.45f, 0.1f, 0.85f);

    private float _elapsed;
    private float _targetRadius;
    private bool _isPlaying;
    private OmniSprite _omniSprite;

    protected float TargetRadius => _targetRadius;
    protected bool IsPlayingEffect => _isPlaying;
    protected bool UsesSpriteVisual => _omniSprite != null;

    public virtual void Play(float radius)
    {
        Duration = Math.Max(0.01f, Duration);
        RadiusScale = Math.Max(0.0f, RadiusScale);
        LineThickness = Math.Max(1.0f, LineThickness);

        _elapsed = 0.0f;
        _targetRadius = Math.Max(0.0f, radius) * RadiusScale;
        _isPlaying = _targetRadius > 0.0f;
        Visible = _isPlaying;
        SetProcess(_isPlaying);
        QueueRedraw();

        if (_isPlaying)
        {
            _omniSprite?.TryPlay(AnimationName);
            ApplyVisualState(progress: 0.0f, easedProgress: 0.0f);
        }

        if (!_isPlaying)
            QueueFree();
    }

    public override void _Ready()
    {
        _omniSprite = GetNodeOrNull<OmniSprite>(OmniSpriteNodeName);
        Visible = false;
        SetProcess(false);
        Scale = Vector2.One;
        Modulate = Colors.White;
    }

    public override void _Process(double delta)
    {
        if (!AdvancePlayback(delta, out var progress, out var easedProgress))
            return;

        ApplyVisualState(progress, easedProgress);

        if (!UsesSpriteVisual)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_isPlaying || UsesSpriteVisual)
            return;

        var progress = Mathf.Clamp(_elapsed / Duration, 0.0f, 1.0f);
        var easedProgress = 1.0f - Mathf.Pow(1.0f - progress, 2.0f);
        var currentRadius = Mathf.Lerp(4.0f, _targetRadius, easedProgress);
        var color = RingColor;
        color.A *= 1.0f - progress;
        var thickness = Mathf.Max(1.0f, LineThickness * (1.0f - (progress * 0.35f)));

        DrawArc(Vector2.Zero, currentRadius, 0.0f, Mathf.Tau, 64, color, thickness, true);
    }

    protected bool AdvancePlayback(double delta, out float progress, out float easedProgress)
    {
        progress = 0.0f;
        easedProgress = 0.0f;

        if (!_isPlaying)
            return false;

        _elapsed += (float)delta;
        if (_elapsed >= Duration)
        {
            _isPlaying = false;
            QueueFree();
            return false;
        }

        progress = Mathf.Clamp(_elapsed / Duration, 0.0f, 1.0f);
        easedProgress = 1.0f - Mathf.Pow(1.0f - progress, 2.0f);
        return true;
    }

    private void ApplyVisualState(float progress, float easedProgress)
    {
        if (!UsesSpriteVisual)
        {
            Scale = Vector2.One;
            Modulate = Colors.White;
            return;
        }

        var scaleMultiplier = Mathf.Lerp(StartScaleMultiplier, EndScaleMultiplier, easedProgress);
        var targetScale = ResolveTargetScale();
        Scale = Vector2.One * Math.Max(0.01f, scaleMultiplier * targetScale);

        var modulate = Colors.White;
        modulate.A = 1.0f - progress;
        Modulate = modulate;
    }

    private float ResolveTargetScale()
    {
        var diameterPixels = Math.Max(1.0f, VisualDiameterPixels);
        var targetDiameter = Math.Max(0.0f, TargetRadius * RadiusScale * 2.0f);
        return Math.Max(0.01f, targetDiameter / diameterPixels);
    }
}
