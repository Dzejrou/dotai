using Godot;

using System;

[GlobalClass]
public partial class Chest : WorldObject
{
    private const string DefaultAnimationName = "default";

    [Export]
    public SpriteFrames OpenAnimationFrames { get; set; }

    [Export]
    public LootTable LootTable { get; set; }

    [Export(PropertyHint.Range, "0,128,1")]
    public float DropSpreadDistanceMin { get; set; } = 12.0f;

    [Export(PropertyHint.Range, "0,128,1")]
    public float DropSpreadDistanceMax { get; set; } = 28.0f;

    [Export]
    public NodePath AnimatedSpritePath { get; set; } = new("AnimatedSprite2D");

    public bool IsUnlocked { get; private set; }
    public bool HasDroppedLoot { get; private set; }
    public bool IsOpening { get; private set; }

    private readonly RandomNumberGenerator _lootRandom = CreateLootRandom();
    private AnimatedSprite2D _animatedSprite;

    public override void _Ready()
    {
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
        if (_animatedSprite != null)
            _animatedSprite.AnimationFinished += OnAnimatedSpriteAnimationFinished;

        InitializeWorldObject(collisionShape: GetNodeOrNull<CollisionShape2D>("CollisionShape2D"));
        ApplyVisualState();
    }

    public override void _ExitTree()
    {
        if (_animatedSprite != null)
            _animatedSprite.AnimationFinished -= OnAnimatedSpriteAnimationFinished;
    }

    public override bool CanInteract(Node interactor)
    {
        return base.CanInteract(interactor) && (!IsUnlocked || !HasDroppedLoot);
    }

    public bool TryUnlock(Node interactor)
    {
        if (IsUnlocked)
            return true;

        if (!CanUnlock(interactor))
            return false;

        IsUnlocked = true;
        StartOpeningAnimation();
        return true;
    }

    public bool TryDropLoot()
    {
        if (HasDroppedLoot)
            return true;

        if (!IsUnlocked)
            return false;

        SpawnLootDrops();
        HasDroppedLoot = true;
        return true;
    }

    private bool CanUnlock(Node interactor)
    {
        // TODO: Add key or other unlock requirements here.
        return interactor is Player;
    }

    private void ApplyVisualState()
    {
        if (_animatedSprite == null)
            return;

        _animatedSprite.SpriteFrames = OpenAnimationFrames;

        var animationName = ResolveAnimationName();
        if (animationName == default)
            return;

        _animatedSprite.Animation = animationName;
        _animatedSprite.Stop();

        if (IsUnlocked && !IsOpening)
        {
            _animatedSprite.Frame = GetFinalFrame(animationName);
            return;
        }

        _animatedSprite.Frame = 0;
    }

    private void StartOpeningAnimation()
    {
        if (_animatedSprite == null)
            return;

        var animationName = ResolveAnimationName();
        if (animationName == default)
            return;

        if (_animatedSprite.SpriteFrames != OpenAnimationFrames)
            _animatedSprite.SpriteFrames = OpenAnimationFrames;

        IsOpening = true;
        _animatedSprite.Play(animationName);
    }

    private void SpawnLootDrops()
    {
        if (LootTable == null)
            return;

        var dropParent = GetParent();
        if (dropParent == null)
            return;

        var rolledEntries = LootTable.Roll(_lootRandom);
        foreach (var entry in rolledEntries)
        {
            if (entry == null)
                continue;

            var drop = entry.CreateDropInstance();
            if (drop == null)
                continue;

            if (dropParent is Node2D node2DParent)
            {
                var spawnOrigin = node2DParent.ToLocal(GlobalPosition);
                var spawnTarget = node2DParent.ToLocal(GlobalPosition + ResolveDropSpawnOffset());
                drop.ConfigureSpawnMotion(spawnOrigin, spawnTarget);
            }

            dropParent.CallDeferred(Node.MethodName.AddChild, drop);
        }
    }

    private Vector2 ResolveDropSpawnOffset()
    {
        var angle = _lootRandom.RandfRange(0.0f, Mathf.Tau);
        var minDistance = Mathf.Max(0.0f, DropSpreadDistanceMin);
        var maxDistance = Mathf.Max(minDistance, DropSpreadDistanceMax);
        var distance = _lootRandom.RandfRange(minDistance, maxDistance);
        return Vector2.Right.Rotated(angle) * distance;
    }

    private void OnAnimatedSpriteAnimationFinished()
    {
        var animationName = ResolveAnimationName();
        if (_animatedSprite == null || animationName == default)
            return;

        IsOpening = false;
        _animatedSprite.Stop();
        _animatedSprite.Frame = GetFinalFrame(animationName);
    }

    private StringName ResolveAnimationName()
    {
        if (OpenAnimationFrames == null)
            return default;

        var defaultAnimation = new StringName(DefaultAnimationName);
        if (OpenAnimationFrames.HasAnimation(defaultAnimation) &&
            OpenAnimationFrames.GetFrameCount(defaultAnimation) > 0)
        {
            return defaultAnimation;
        }

        foreach (StringName animationName in OpenAnimationFrames.GetAnimationNames())
        {
            if (OpenAnimationFrames.GetFrameCount(animationName) > 0)
                return animationName;
        }

        return default;
    }

    private int GetFinalFrame(StringName animationName)
    {
        if (OpenAnimationFrames == null || animationName == default)
            return 0;

        return Math.Max(0, OpenAnimationFrames.GetFrameCount(animationName) - 1);
    }

    private static RandomNumberGenerator CreateLootRandom()
    {
        var random = new RandomNumberGenerator();
        random.Randomize();
        return random;
    }
}
