using Godot;

public abstract partial class AnimatedCharacter : CharacterBody2D
{
    public AnimatedSprite2D AnimatedSprite { get; private set; }
    public string LastDirection { get; private set; } = "south";

    protected void SetAnimatedSprite(AnimatedSprite2D animatedSprite)
    {
        AnimatedSprite = animatedSprite;
    }

    public void SetFacingDirection(Vector2 direction)
    {
        if (direction != Vector2.Zero)
            LastDirection = DirectionHelper.GetDirectionName(direction);
    }

    protected string GetDirectionalAnimationName(string animationPrefix)
    {
        return $"{animationPrefix}_{LastDirection}";
    }

    protected string GetIdleAnimationName()
    {
        return GetDirectionalAnimationName("breathing-idle");
    }

    protected string GetWalkAnimationName()
    {
        return GetDirectionalAnimationName("walk");
    }

    protected void SetAnimationSafe(string animationName)
    {
        if (AnimatedSprite?.SpriteFrames == null || !AnimatedSprite.SpriteFrames.HasAnimation(animationName))
            return;

        if (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != animationName)
            AnimatedSprite.Play(animationName);
    }

    public void PlayIdleIfAvailable()
    {
        SetAnimationSafe(GetIdleAnimationName());
    }
}
