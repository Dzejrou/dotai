using Godot;

using System;

[GlobalClass]
public partial class Chest : WorldObject, ILockable
{
    private const string DefaultAnimationName = "default";

    [Export]
    public bool IsLocked
    {
        get => _isLocked;
        set => SetLocked(value);
    }

    [Export]
    public SpriteFrames OpenAnimationFrames { get; set; }

    [Export]
    public SpriteFrames UnlockOpenAnimationFrames { get; set; }

    [Export]
    public LootTable LootTable { get; set; }

    [Export(PropertyHint.Range, "0,128,1")]
    public float DropSpreadDistanceMin { get; set; } = 12.0f;

    [Export(PropertyHint.Range, "0,128,1")]
    public float DropSpreadDistanceMax { get; set; } = 28.0f;

    [Export]
    public NodePath AnimatedSpritePath { get; set; } = new("AnimatedSprite2D");

    public bool HasDroppedLoot { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsAnimatingOpen { get; private set; }

    private readonly RandomNumberGenerator _lootRandom = CreateLootRandom();
    private AnimatedSprite2D _animatedSprite;
    private bool _isLocked = true;
    private bool _animationFinishedConnected;

    public override void _EnterTree()
    {
        base._EnterTree();
        EnsureAnimationSignalConnected();
    }

    public override void _Ready()
    {
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
        EnsureAnimationSignalConnected();

        InitializeWorldObject(collisionShape: GetNodeOrNull<CollisionShape2D>("CollisionShape2D"));
        ApplyVisualState();
    }

    public override void _ExitTree()
    {
        DisconnectAnimationSignal();
    }

    public override bool CanInteract(Node interactor)
    {
        return base.CanInteract(interactor) && (!IsOpen || !HasDroppedLoot);
    }

    public bool TryUnlock(Node interactor)
    {
        if (!IsLocked)
            return true;

        if (interactor is Player player && !TryConsumeChestKey(player))
            return false;

        UnlockExternal();
        StartOpenAnimation(UnlockOpenAnimationFrames);
        IsOpen = true;
        return true;
    }

    public void UnlockExternal()
    {
        if (!IsLocked)
            return;

        SetLocked(false);
    }

    public bool TryOpen()
    {
        if (IsLocked)
            return false;

        if (IsOpen)
            return true;

        StartOpenAnimation(OpenAnimationFrames);
        IsOpen = true;
        return true;
    }

    public bool TryDropLoot()
    {
        if (HasDroppedLoot)
            return true;

        if (IsLocked || !IsOpen)
            return false;

        SpawnLootDrops();
        HasDroppedLoot = true;
        return true;
    }

    private bool TryConsumeChestKey(Player player)
    {
        if (player == null || !GodotObject.IsInstanceValid(player))
            return false;

        var inventory = player.InventoryController;
        return inventory != null && inventory.TryConsumeKeyKind(InventoryKeyKind.ChestKey, 1);
    }

    private void ApplyVisualState()
    {
        if (_animatedSprite == null)
            return;

        var frames = ResolveIdleFrames();
        _animatedSprite.SpriteFrames = frames;

        var animationName = ResolveAnimationName(frames);
        if (animationName == default)
            return;

        _animatedSprite.Animation = animationName;
        _animatedSprite.Stop();

        if (IsOpen)
        {
            _animatedSprite.Frame = GetFinalFrame(frames, animationName);
            return;
        }

        _animatedSprite.Frame = 0;
    }

    private void StartOpenAnimation(SpriteFrames frames)
    {
        if (_animatedSprite == null)
            return;

        var animationName = ResolveAnimationName(frames);
        if (animationName == default)
            return;

        _animatedSprite.SpriteFrames = frames;
        _animatedSprite.Animation = animationName;

        IsAnimatingOpen = true;
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
        var frames = _animatedSprite?.SpriteFrames;
        var animationName = ResolveAnimationName(frames);
        if (_animatedSprite == null || animationName == default)
            return;

        IsAnimatingOpen = false;
        _animatedSprite.Stop();
        _animatedSprite.Frame = GetFinalFrame(frames, animationName);
    }

    private void SetLocked(bool isLocked)
    {
        _isLocked = isLocked;

        if (!IsOpen && !IsAnimatingOpen)
            ApplyVisualState();
    }

    private SpriteFrames ResolveIdleFrames()
    {
        return IsLocked ? UnlockOpenAnimationFrames : OpenAnimationFrames;
    }

    private StringName ResolveAnimationName(SpriteFrames frames)
    {
        if (frames == null)
            return default;

        var defaultAnimation = new StringName(DefaultAnimationName);
        if (frames.HasAnimation(defaultAnimation) &&
            frames.GetFrameCount(defaultAnimation) > 0)
        {
            return defaultAnimation;
        }

        foreach (StringName animationName in frames.GetAnimationNames())
        {
            if (frames.GetFrameCount(animationName) > 0)
                return animationName;
        }

        return default;
    }

    private int GetFinalFrame(SpriteFrames frames, StringName animationName)
    {
        if (frames == null || animationName == default)
            return 0;

        return Math.Max(0, frames.GetFrameCount(animationName) - 1);
    }

    private static RandomNumberGenerator CreateLootRandom()
    {
        var random = new RandomNumberGenerator();
        random.Randomize();
        return random;
    }

    private void EnsureAnimationSignalConnected()
    {
        if (_animationFinishedConnected || _animatedSprite == null)
            return;

        _animatedSprite.AnimationFinished += OnAnimatedSpriteAnimationFinished;
        _animationFinishedConnected = true;
    }

    private void DisconnectAnimationSignal()
    {
        if (!_animationFinishedConnected || _animatedSprite == null)
            return;

        _animatedSprite.AnimationFinished -= OnAnimatedSpriteAnimationFinished;
        _animationFinishedConnected = false;
    }
}
