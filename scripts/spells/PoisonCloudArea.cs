using Godot;

[GlobalClass]
public partial class PoisonCloudArea : AreaOfEffect
{
    private static readonly StringName DefaultAnimationName = "default";

    private AnimatedSprite2D _sprite;

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
