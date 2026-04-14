using Godot;

[GlobalClass]
public partial class RingOfFireArea : AreaOfEffect
{
    private static readonly StringName DefaultAnimationName = "default";

    private OmniSprite _omniSprite;

    protected override void OnAreaReady()
    {
        _omniSprite ??= GetNodeOrNull<OmniSprite>("OmniSprite");
        _omniSprite?.TryPlay(DefaultAnimationName);
    }

    protected override void OnRuntimeInitialized()
    {
        if (_omniSprite != null)
        {
            _omniSprite.Visible = true;
            _omniSprite.TryPlay(DefaultAnimationName);
        }
    }
}
