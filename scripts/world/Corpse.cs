using Godot;

using System;

[GlobalClass]
public partial class Corpse : Node2D
{
    private AnimatedSprite2D _animatedSprite;

    public override void _Ready()
    {
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_animatedSprite != null)
            _animatedSprite.AnimationFinished += OnAnimationFinished;
    }

    public override void _ExitTree()
    {
        if (_animatedSprite != null)
            _animatedSprite.AnimationFinished -= OnAnimationFinished;
    }

    public void Initialize(
        SpriteFrames spriteFrames,
        StringName deathAnimationBase,
        string direction,
        Vector2 worldPosition,
        Vector2 spriteOffset,
        Vector2 spriteScale,
        bool flipH,
        bool flipV,
        int zIndex)
    {
        _animatedSprite ??= GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_animatedSprite == null || spriteFrames == null)
            return;

        GlobalPosition = worldPosition;
        ZIndex = zIndex;
        _animatedSprite.Position = spriteOffset;
        _animatedSprite.Scale = spriteScale;
        _animatedSprite.FlipH = flipH;
        _animatedSprite.FlipV = flipV;
        _animatedSprite.SpriteFrames = spriteFrames;

        var animationName = ResolveAnimationName(spriteFrames, deathAnimationBase, direction);
        if (animationName == default || spriteFrames.GetFrameCount(animationName) <= 0)
        {
            QueueFree();
            return;
        }

        _animatedSprite.Play(animationName);
    }

    private void OnAnimationFinished()
    {
        if (_animatedSprite?.SpriteFrames == null)
            return;

        var animationName = _animatedSprite.Animation;
        var finalFrame = Math.Max(0, _animatedSprite.SpriteFrames.GetFrameCount(animationName) - 1);
        _animatedSprite.Stop();
        _animatedSprite.SetFrame(finalFrame);
    }

    private static StringName ResolveAnimationName(SpriteFrames spriteFrames, StringName deathAnimationBase, string direction)
    {
        var directedAnimation = new StringName($"{deathAnimationBase}_{direction}");
        if (spriteFrames.HasAnimation(directedAnimation) && spriteFrames.GetFrameCount(directedAnimation) > 0)
            return directedAnimation;

        foreach (StringName animationName in spriteFrames.GetAnimationNames())
        {
            if (animationName.ToString().StartsWith(deathAnimationBase.ToString(), StringComparison.Ordinal) &&
                spriteFrames.GetFrameCount(animationName) > 0)
            {
                return animationName;
            }
        }

        foreach (StringName animationName in spriteFrames.GetAnimationNames())
        {
            if (spriteFrames.GetFrameCount(animationName) > 0)
                return animationName;
        }

        return default;
    }
}
