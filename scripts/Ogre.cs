using Godot;

using System;

[GlobalClass]
public partial class Ogre : CharacterBody2D, IEnemyTarget
{
    [Export]
    public float Speed { get; set; } = 64.0f;

    [Export]
    public float AttackRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.2f;

    [Export]
    public int MaxHealth { get; set; } = 40;

    [Export]
    public int MinAttackDamage { get; set; } = 1;

    [Export]
    public int MaxAttackDamage { get; set; } = 4;

    [Export]
    public float HealthRegenerationInterval { get; set; } = 5.0f;

    [Export]
    public int HealthRegenerationAmount { get; set; } = 1;

    [Export]
    public bool DisableCollisionOnDeath { get; set; } = true;

    [Export]
    public NodePath PlayerPath { get; set; } = new NodePath("../Player");

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    private AnimatedSprite2D _animatedSprite;
    private CollisionShape2D _collisionShape;
    private Player _player;
    private RandomNumberGenerator _randomNumberGenerator = new();
    private float _attackCooldownTimer;
    private float _healthRegenTimer;
    private int _currentHealth;
    private bool _isDead;
    private string _lastDirection = "south";

    public override void _Ready()
    {
        _currentHealth = Math.Max(1, MaxHealth);
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _animatedSprite.SpriteFrames = BuildSpriteFrames();

        if (_animatedSprite.SpriteFrames.HasAnimation("walk_south"))
        {
            _animatedSprite.Animation = "walk_south";
            _animatedSprite.Play();
        }

        _animatedSprite.AnimationFinished += OnAnimationFinished;
        AddToGroup("enemies");

        if (!PlayerPath.IsEmpty && HasNode(PlayerPath))
            _player = GetNode<Player>(PlayerPath);
        else
            _player = GetParent()?.GetNodeOrNull<Player>("Player");

        if (_player == null)
            GD.PrintErr("Ogre could not find Player node.");

        _randomNumberGenerator.Randomize();
        _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        HandleHealthRegeneration((float)delta);

        if (_player == null || !IsInstanceValid(_player) || !_player.IsInsideTree())
        {
            _player = null;
            Velocity = Vector2.Zero;
            _animatedSprite.Stop();
            return;
        }

        if (_attackCooldownTimer > 0.0f)
            _attackCooldownTimer -= (float)delta;

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        if (toPlayer.Length() <= AttackRange)
        {
            Velocity = Vector2.Zero;
            TryAttackPlayer();
            return;
        }

        if (toPlayer == Vector2.Zero)
        {
            Velocity = Vector2.Zero;
            _animatedSprite.Stop();
            return;
        }

        _lastDirection = GetDirectionName(toPlayer);
        var walkAnimation = $"walk_{_lastDirection}";
        if (_animatedSprite.SpriteFrames != null && _animatedSprite.SpriteFrames.HasAnimation(walkAnimation))
            _animatedSprite.Play(walkAnimation);

        Velocity = toPlayer.Normalized() * Speed;
        MoveAndSlide();
    }

    public void ApplyDamage(int amount)
    {
        if (_isDead)
            return;

        _currentHealth = Math.Max(0, _currentHealth - Math.Max(1, amount));

        if (_currentHealth <= 0)
        {
            _isDead = true;
            StartDeath();
        }
    }

    private void TryAttackPlayer()
    {
        if (_attackCooldownTimer > 0.0f || _player == null || _isDead)
            return;

        _attackCooldownTimer = AttackCooldown;

        var maxDamage = Math.Max(MinAttackDamage, MaxAttackDamage);
        var damage = _randomNumberGenerator.RandiRange(Math.Min(MinAttackDamage, maxDamage), maxDamage);
        _player.ApplyDamage(damage);
    }

    private void HandleHealthRegeneration(float delta)
    {
        _healthRegenTimer -= delta;
        if (_healthRegenTimer > 0.0f)
            return;

        if (_currentHealth >= MaxHealth)
        {
            _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
            return;
        }

        var missing = MaxHealth - _currentHealth;
        var healAmount = Math.Min(Math.Max(HealthRegenerationAmount, 1), missing);
        if (healAmount <= 0)
            return;

        _currentHealth += healAmount;
        ShowFloatingHealingNumber(healAmount);
        _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
    }

    private void OnAnimationFinished()
    {
        var animationName = _animatedSprite.Animation.ToString();

        if (!animationName.StartsWith(DeathAnimation.ToString(), StringComparison.Ordinal))
            return;

        var finalFrame = Math.Max(0, _animatedSprite.SpriteFrames.GetFrameCount(animationName) - 1);
        _animatedSprite.Stop();
        _animatedSprite.SetFrame(finalFrame);
        SetPhysicsProcess(false);
    }

    private static string GetDirectionName(Vector2 direction)
    {
        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
            return direction.X > 0.0f ? "east" : "west";

        return direction.Y > 0.0f ? "south" : "north";
    }

    private SpriteFrames BuildSpriteFrames()
    {
        var spriteFrames = new SpriteFrames();
        var directions = new[] { "south", "east", "north", "west" };

        foreach (var direction in directions)
        {
            AddAnimationFrames(spriteFrames, $"walk_{direction}", "walk", direction, true);
            AddAnimationFrames(spriteFrames, $"{DeathAnimation}_{direction}", "falling-back-death", direction, false);
        }

        return spriteFrames;
    }

    private void AddAnimationFrames(SpriteFrames spriteFrames, string animationName, string assetFolder, string direction, bool loops)
    {
        spriteFrames.AddAnimation(animationName);
        spriteFrames.SetAnimationLoop(animationName, loops);
        var frameLoaded = 0;

        var frame = 0;
        while (frame <= 999)
        {
            var path = $"res://assets/ogre/animations/{assetFolder}/{direction}/frame_{frame:000}.png";
            var absolutePath = ProjectSettings.GlobalizePath(path);
            if (!FileAccess.FileExists(absolutePath))
                break;

            var image = Image.LoadFromFile(absolutePath);
            if (image == null)
            {
                GD.PrintErr($"Ogre failed to load frame image at '{path}'.");
                frame++;
                continue;
            }

            var texture = ImageTexture.CreateFromImage(image);
            if (texture != null)
            {
                spriteFrames.AddFrame(animationName, texture);
                frameLoaded++;
            }

            frame++;
        }

        if (frameLoaded == 0)
        {
            GD.PrintErr($"Ogre animation '{animationName}' has no frames at assets/ogre/animations/{assetFolder}/{direction}/");
            spriteFrames.RemoveAnimation(animationName);
        }
    }

    private void StartDeath()
    {
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;

        if (DisableCollisionOnDeath && _collisionShape != null)
            _collisionShape.Disabled = true;

        var deathAnimation = $"{DeathAnimation}_{_lastDirection}";
        if (_animatedSprite.SpriteFrames != null &&
            _animatedSprite.SpriteFrames.HasAnimation(deathAnimation) &&
            _animatedSprite.SpriteFrames.GetFrameCount(deathAnimation) > 0)
        {
            _animatedSprite.Play(deathAnimation);
            return;
        }

        _animatedSprite.Stop();
        SetPhysicsProcess(false);
    }

    private void ShowFloatingHealingNumber(int amount)
    {
        if (amount <= 0)
            return;

        var popup = new Node2D
        {
            GlobalPosition = GlobalPosition + new Vector2(0, -16.0f)
        };

        var label = new Label
        {
            Text = $"+{amount}",
            Modulate = new Color(0.0f, 1.0f, 0.0f, 1.0f),
            ZIndex = 4
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        popup.AddChild(label);

        var parent = GetTree().CurrentScene ?? GetParent();
        if (parent == null)
            return;

        parent.AddChild(popup);

        var tween = GetTree().CreateTween();
        var targetY = popup.GlobalPosition + new Vector2(0.0f, -18.0f);
        tween.TweenProperty(popup, "global_position", targetY, 0.6f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.6f);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(popup))
                popup.QueueFree();
        };
    }

}
