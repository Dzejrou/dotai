using Godot;

using System;

[GlobalClass]
public partial class FireNovaVfx : NovaSpellVfx
{
    private static readonly StringName DefaultAnimationName = "default";

    [Export]
    public float VisualDiameterPixels { get; set; } = 128.0f;

    [Export]
    public float StartScaleMultiplier { get; set; } = 0.2f;

    [Export]
    public float EndScaleMultiplier { get; set; } = 1.0f;

    private OmniSprite _omniSprite;

    public override void _Ready()
    {
        base._Ready();
        _omniSprite = GetNodeOrNull<OmniSprite>("OmniSprite");
        if (_omniSprite == null)
            GD.PushError($"{GetPath()}: missing required OmniSprite child.");
    }

    public override void Play(float radius)
    {
        base.Play(radius);
        if (!IsPlayingEffect)
            return;

        _omniSprite?.TryPlay(DefaultAnimationName);
        ApplyVisualState(progress: 0.0f, easedProgress: 0.0f);
    }

    public override void _Process(double delta)
    {
        if (!AdvancePlayback(delta, out var progress, out var easedProgress))
            return;

        ApplyVisualState(progress, easedProgress);
    }

    public override void _Draw()
    {
    }

    private void ApplyVisualState(float progress, float easedProgress)
    {
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
