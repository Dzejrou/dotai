using Godot;

using System;

[GlobalClass]
public partial class FireNovaVfx : Node2D
{
    [Export]
    public float Duration { get; set; } = 0.28f;

    [Export]
    public float RadiusScale { get; set; } = 1.0f;

    [Export]
    public float LineThickness { get; set; } = 6.0f;

    [Export]
    public Color RingColor { get; set; } = new Color(1.0f, 0.45f, 0.1f, 0.85f);

    private float _elapsed;
    private float _targetRadius;
    private bool _isPlaying;

    public void Play(float radius)
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

        if (!_isPlaying)
            QueueFree();
    }

    public override void _Ready()
    {
        Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (!_isPlaying)
            return;

        _elapsed += (float)delta;
        if (_elapsed >= Duration)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_isPlaying)
            return;

        var progress = Mathf.Clamp(_elapsed / Duration, 0.0f, 1.0f);
        var easedProgress = 1.0f - Mathf.Pow(1.0f - progress, 2.0f);
        var currentRadius = Mathf.Lerp(4.0f, _targetRadius, easedProgress);
        var color = RingColor;
        color.A *= 1.0f - progress;
        var thickness = Mathf.Max(1.0f, LineThickness * (1.0f - (progress * 0.35f)));

        DrawArc(Vector2.Zero, currentRadius, 0.0f, Mathf.Tau, 64, color, thickness, true);
    }
}
