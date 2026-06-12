using Godot;

public abstract partial class AnimatedCharacter : CharacterBody2D
{
    public OmniSprite OmniSprite { get; private set; }
    public string LastDirection { get; private set; } = "south";

    protected void SetOmniSprite(OmniSprite omniSprite)
    {
        OmniSprite = omniSprite;
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
        if (OmniSprite?.SpriteFrames == null || string.IsNullOrEmpty(animationPrefix))
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

        if (!OmniSprite.IsAnimationPlaying || OmniSprite.CurrentAnimation != animationName)
            OmniSprite.TryPlay(animationName);
    }

    public bool TryPlayDirectionalAnimation(string animationPrefix, float customSpeed = 1.0f)
    {
        if (OmniSprite == null || string.IsNullOrEmpty(animationPrefix))
            return false;

        // Forward unresolvable names so OmniSprite can register the missing request
        // (placeholder visual) and retry its lazy resource lookup.
        var animationName = ResolveDirectionalAnimationName(animationPrefix)
            ?? GetDirectionalAnimationName(animationPrefix);
        return OmniSprite.TryPlay(animationName, customSpeed);
    }

    public void PlayIdleIfAvailable()
    {
        SetAnimationSafe(GetIdleAnimationName());
    }

    private bool HasAnimation(string animationName)
    {
        return OmniSprite?.HasAnimation(animationName) ?? false;
    }
}
