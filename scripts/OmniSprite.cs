using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class OmniSprite : Node2D
{
    [Signal]
    public delegate void AnimationFinishedEventHandler();

    private const string AnimatedSpriteNodeName = "AnimatedSprite2D";
    private const string StaticSpriteNodeName = "Sprite2D";
    private const string MissingSpriteNodeName = "__MissingSprite2D";
    private const int MissingTextureSize = 16;

    private static Texture2D _missingTexture;

    private readonly Dictionary<StringName, Color> _statusTints = new();
    private AnimatedSprite2D _animatedSprite;
    private Sprite2D _staticSprite;
    private Sprite2D _missingSprite;
    private Texture2D _configuredStaticTexture;
    private Color _baseModulate = Colors.White;

    public AnimatedSprite2D AnimatedSprite
    {
        get
        {
            ResolveChildVisuals();
            return _animatedSprite;
        }
    }

    public Sprite2D StaticSprite
    {
        get
        {
            ResolveChildVisuals();
            return _staticSprite;
        }
    }

    public SpriteFrames SpriteFrames => AnimatedSprite?.SpriteFrames;
    public StringName CurrentAnimation => AnimatedSprite?.Animation ?? default;
    public bool IsAnimationPlaying => AnimatedSprite?.IsPlaying() ?? false;

    public override void _Ready()
    {
        ResolveChildVisuals();
        ConnectAnimatedSpriteSignals();
        RefreshVisualState();
    }

    public override void _ExitTree()
    {
        DisconnectAnimatedSpriteSignals();
    }

    public bool HasAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
            return false;

        var spriteFrames = SpriteFrames;
        if (spriteFrames == null || !spriteFrames.HasAnimation(animationName))
            return false;

        var frameCount = spriteFrames.GetFrameCount(animationName);
        return frameCount > 0 && spriteFrames.GetFrameTexture(animationName, 0) != null;
    }

    public bool TryPlay(string animationName, float customSpeed = 1.0f)
    {
        if (!HasAnimation(animationName))
        {
            RefreshVisualState();
            return false;
        }

        AnimatedSprite?.Play(animationName, customSpeed: customSpeed);
        RefreshVisualState();
        return true;
    }

    public void StopAnimation()
    {
        AnimatedSprite?.Stop();
        RefreshVisualState();
    }

    public void SetAnimatedSpriteFrames(SpriteFrames spriteFrames, string animationName = null)
    {
        var animatedSprite = AnimatedSprite;
        if (animatedSprite == null)
            return;

        animatedSprite.SpriteFrames = spriteFrames;
        if (!string.IsNullOrEmpty(animationName))
            animatedSprite.Animation = animationName;

        RefreshVisualState();
    }

    public void SetStaticTexture(Texture2D texture)
    {
        _configuredStaticTexture = texture;

        if (texture != null)
            GetOrCreateStaticSprite().Texture = texture;
        else if (_staticSprite != null)
            _staticSprite.Texture = null;

        RefreshVisualState();
    }

    public Texture2D GetStaticTexture()
    {
        ResolveChildVisuals();
        return _configuredStaticTexture ?? _staticSprite?.Texture;
    }

    public void SetBaseModulate(Color color)
    {
        _baseModulate = color;
        RefreshVisualState();
    }

    public void ResetBaseModulate()
    {
        SetBaseModulate(Colors.White);
    }

    public void SetStatusTint(StringName statusKey, Color color)
    {
        if (statusKey == default)
            return;

        _statusTints[statusKey] = color;
        RefreshVisualState();
    }

    public void ClearStatusTint(StringName statusKey)
    {
        if (statusKey == default)
            return;

        if (_statusTints.Remove(statusKey))
            RefreshVisualState();
    }

    public void ReflectStatusEffect(StatusEffect effect, bool active)
    {
        effect?.ApplyVisualEffect(this, active);
        RefreshVisualState();
    }

    private void ResolveChildVisuals()
    {
        _animatedSprite ??= GetNodeOrNull<AnimatedSprite2D>(AnimatedSpriteNodeName);
        _staticSprite ??= GetNodeOrNull<Sprite2D>(StaticSpriteNodeName);
        _missingSprite ??= GetNodeOrNull<Sprite2D>(MissingSpriteNodeName);

        if (_staticSprite != null && _configuredStaticTexture == null)
            _configuredStaticTexture = _staticSprite.Texture;
    }

    private void ConnectAnimatedSpriteSignals()
    {
        var animatedSprite = AnimatedSprite;
        if (animatedSprite == null)
            return;

        var callable = new Callable(this, nameof(OnAnimatedSpriteAnimationFinished));
        if (!animatedSprite.IsConnected(AnimatedSprite2D.SignalName.AnimationFinished, callable))
            animatedSprite.Connect(AnimatedSprite2D.SignalName.AnimationFinished, callable);
    }

    private void DisconnectAnimatedSpriteSignals()
    {
        var animatedSprite = _animatedSprite;
        if (animatedSprite == null || !GodotObject.IsInstanceValid(animatedSprite))
            return;

        var callable = new Callable(this, nameof(OnAnimatedSpriteAnimationFinished));
        if (animatedSprite.IsConnected(AnimatedSprite2D.SignalName.AnimationFinished, callable))
            animatedSprite.Disconnect(AnimatedSprite2D.SignalName.AnimationFinished, callable);
    }

    private void OnAnimatedSpriteAnimationFinished()
    {
        EmitSignal(SignalName.AnimationFinished);
    }

    private void RefreshVisualState()
    {
        ResolveChildVisuals();

        var activeModulate = ComposeModulate();
        var shouldUseAnimatedSprite = HasActiveAnimatedVisual();
        var staticTexture = GetStaticTexture();
        var shouldUseStaticSprite = !shouldUseAnimatedSprite && staticTexture != null;

        if (_animatedSprite != null)
        {
            _animatedSprite.Visible = shouldUseAnimatedSprite;
            _animatedSprite.Modulate = activeModulate;
        }

        if (_staticSprite != null)
        {
            _staticSprite.Texture = staticTexture;
            _staticSprite.Visible = shouldUseStaticSprite;
            _staticSprite.Modulate = activeModulate;
        }

        var shouldUseMissingSprite = !shouldUseAnimatedSprite && !shouldUseStaticSprite;
        if (shouldUseMissingSprite)
        {
            var missingSprite = GetOrCreateMissingSprite();
            missingSprite.Texture = GetMissingTexture();
            missingSprite.Visible = true;
            missingSprite.Modulate = activeModulate;
        }
        else if (_missingSprite != null)
        {
            _missingSprite.Visible = false;
            _missingSprite.Modulate = activeModulate;
        }
    }

    private bool HasActiveAnimatedVisual()
    {
        var animatedSprite = AnimatedSprite;
        if (animatedSprite?.SpriteFrames == null)
            return false;

        var animationName = animatedSprite.Animation;
        return !animationName.IsEmpty && HasAnimation(animationName);
    }

    private Color ComposeModulate()
    {
        var modulate = _baseModulate;
        foreach (var tint in _statusTints.Values)
            modulate *= tint;

        return modulate;
    }

    private Sprite2D GetOrCreateStaticSprite()
    {
        ResolveChildVisuals();
        if (_staticSprite != null)
            return _staticSprite;

        _staticSprite = new Sprite2D
        {
            Name = StaticSpriteNodeName,
            Centered = true,
        };
        AddChild(_staticSprite);
        return _staticSprite;
    }

    private Sprite2D GetOrCreateMissingSprite()
    {
        ResolveChildVisuals();
        if (_missingSprite != null)
            return _missingSprite;

        _missingSprite = new Sprite2D
        {
            Name = MissingSpriteNodeName,
            Centered = true,
            ZIndex = -1,
        };
        AddChild(_missingSprite);
        return _missingSprite;
    }

    private static Texture2D GetMissingTexture()
    {
        _missingTexture ??= CreateMissingTexture();
        return _missingTexture;
    }

    private static Texture2D CreateMissingTexture()
    {
        var image = Image.CreateEmpty(MissingTextureSize, MissingTextureSize, false, Image.Format.Rgba8);

        for (var y = 0; y < MissingTextureSize; y++)
        {
            for (var x = 0; x < MissingTextureSize; x++)
            {
                var isBorder = x == 0 || y == 0 || x == MissingTextureSize - 1 || y == MissingTextureSize - 1;
                var color = isBorder ? Colors.Black : Colors.Magenta;
                image.SetPixel(x, y, color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
