using Godot;

public abstract partial class AnimatedCharacter : CharacterBody2D
{
    protected static readonly Color SlowedSpriteTintColor = new(0.62f, 0.78f, 1.0f, 1.0f);

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

    public string ResolveDirectionalAnimationName(string animationPrefix)
    {
        if (AnimatedSprite?.SpriteFrames == null || string.IsNullOrEmpty(animationPrefix))
            return null;

        var exactAnimationName = $"{animationPrefix}_{LastDirection}";
        if (HasAnimation(exactAnimationName))
            return exactAnimationName;

        var fallbackDirection = DirectionHelper.GetCardinalFallbackDirectionName(LastDirection);
        if (fallbackDirection == LastDirection)
            return null;

        var fallbackAnimationName = $"{animationPrefix}_{fallbackDirection}";
        return HasAnimation(fallbackAnimationName) ? fallbackAnimationName : null;
    }

    protected string GetIdleAnimationName()
    {
        return ResolveDirectionalAnimationName("idle") ?? GetDirectionalAnimationName("idle");
    }

    protected string GetWalkAnimationName()
    {
        return ResolveDirectionalAnimationName("walk") ?? GetDirectionalAnimationName("walk");
    }

    protected void SetAnimationSafe(string animationName)
    {
        if (!HasAnimation(animationName))
            return;

        if (!AnimatedSprite.IsPlaying() || AnimatedSprite.Animation != animationName)
            AnimatedSprite.Play(animationName);
    }

    public bool TryPlayDirectionalAnimation(string animationPrefix, float customSpeed = 1.0f)
    {
        var animationName = ResolveDirectionalAnimationName(animationPrefix);
        if (animationName == null)
            return false;

        AnimatedSprite.Play(animationName, customSpeed: customSpeed);
        return true;
    }

    public void PlayIdleIfAvailable()
    {
        SetAnimationSafe(GetIdleAnimationName());
    }

    protected void SetSpriteTint(Color color)
    {
        if (AnimatedSprite != null)
            AnimatedSprite.Modulate = color;
    }

    protected void ResetSpriteTint()
    {
        SetSpriteTint(Colors.White);
    }

    private bool HasAnimation(string animationName)
    {
        return AnimatedSprite?.SpriteFrames != null &&
               AnimatedSprite.SpriteFrames.HasAnimation(animationName) &&
               AnimatedSprite.SpriteFrames.GetFrameCount(animationName) > 0;
    }
}
