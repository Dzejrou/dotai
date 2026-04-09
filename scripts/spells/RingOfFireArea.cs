using Godot;

[GlobalClass]
public partial class RingOfFireArea : AreaOfEffect
{
    private static readonly StringName DefaultAnimationName = "default";

    private AnimatedSprite2D _sprite;

    public RingOfFireArea()
    {
        EffectLifetime = 5.0f;
        TickInterval = 1.0f;
        ApplyOnEnter = false;
        ApplyOnTick = true;
        FillColor = Colors.Transparent;
        OutlineColor = Colors.Transparent;
        PreviewFillColor = Colors.Transparent;
        PreviewOutlineColor = Colors.Transparent;
    }

    protected override void OnAreaReady()
    {
        _sprite ??= GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _sprite?.Play(DefaultAnimationName);
    }

    protected override void OnRuntimeInitialized()
    {
        if (_sprite != null)
        {
            _sprite.Visible = true;
            _sprite.Play(DefaultAnimationName);
        }
    }
}
