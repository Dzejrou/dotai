using Godot;

[GlobalClass]
public partial class Portal : RoomTransition
{
    [Export]
    public NodePath OmniSpritePath { get; set; } = new NodePath("OmniSprite");

    [Export]
    public StringName AnimationName { get; set; } = "default";

    [Export(PropertyHint.Range, "0.01,8.0,0.01")]
    public float AnimationSpeedScale { get; set; } = 1.0f;

    public override void _Ready()
    {
        base._Ready();

        var omniSprite = GetNodeOrNull<OmniSprite>(OmniSpritePath);
        if (omniSprite == null || !HasValue(AnimationName))
            return;

        omniSprite.TryPlay(AnimationName.ToString(), AnimationSpeedScale);
    }
}
