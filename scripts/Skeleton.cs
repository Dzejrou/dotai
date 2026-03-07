using Godot;

using System;

[GlobalClass]
public partial class Skeleton : CharacterBody2D
{
    [Export]
    public float Speed { get; set; } = 52.0f;

    [Export]
    public float AttackRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.1f;

    [Export]
    public NodePath PlayerPath { get; set; } = new NodePath("../Player");

    [Export]
    public StringName AttackAnimation { get; set; } = "cross-punch";

    [Export]
    public StringName DeathAnimation { get; set; } = "falling-back-death";

    [Export]
    public int Health { get; set; } = 24;

    [Export]
    public bool DisableCollisionOnDeath { get; set; } = true;

    private AnimatedSprite2D _animatedSprite;
    private CollisionShape2D _collisionShape;
    private Player _player;
    private RandomNumberGenerator _randomNumberGenerator = new();
    private float _attackCooldownTimer;
    private bool _attacking;
    private string _lastDirection = "south";
    private int _currentHealth;
    private bool _isDead;

    public override void _Ready()
    {
        _currentHealth = Math.Max(1, Health);
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _animatedSprite.SpriteFrames = BuildSpriteFrames();
        _animatedSprite.Animation = "walk_south";
        _animatedSprite.Play();
        _animatedSprite.AnimationFinished += OnAnimationFinished;
        AddToGroup("enemies");

        if (!PlayerPath.IsEmpty && HasNode(PlayerPath))
            _player = GetNode<Player>(PlayerPath);
        else
            _player = GetParent()?.GetNodeOrNull<Player>("Player");

        if (_player == null)
            GD.PrintErr("Skeleton could not find Player node.");

        _randomNumberGenerator.Randomize();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        if (!IsInstanceValid(_player) || _player == null || !_player.IsInsideTree())
        {
            _player = null;
            Velocity = Vector2.Zero;
            _animatedSprite.Stop();
            return;
        }

        if (_attackCooldownTimer > 0.0f)
            _attackCooldownTimer -= (float)delta;

        if (_attacking)
        {
            Velocity = Vector2.Zero;
            return;
        }

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        if (toPlayer.Length() <= AttackRange && _attackCooldownTimer <= 0.0f)
        {
            StartAttack();
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
        if (_animatedSprite.Animation != walkAnimation)
            _animatedSprite.Play(walkAnimation);

        Velocity = toPlayer.Normalized() * Speed;
        MoveAndSlide();
    }

    public void ApplyDamage(int amount)
    {
        if (_isDead)
            return;

        _currentHealth = Math.Max(0, _currentHealth - Math.Max(1, amount));
        GD.Print($"Skeleton {Name} health: {_currentHealth}/{Health}");
        if (_currentHealth <= 0)
        {
            _isDead = true;
            StartDeath();
        }
    }

    private void StartAttack()
    {
        if (_player == null || !_player.IsInsideTree())
        {
            _player = null;
            return;
        }

        _attacking = true;
        _attackCooldownTimer = AttackCooldown;

        var attackAnimation = $"{AttackAnimation}_{_lastDirection}";
        if (_animatedSprite.SpriteFrames != null &&
            _animatedSprite.SpriteFrames.GetFrameCount(attackAnimation) == 0)
        {
            _attacking = false;
            _player.ApplyDamage(_randomNumberGenerator.RandiRange(1, 5));
            return;
        }

        if (_player.GlobalPosition != Vector2.Zero)
            _lastDirection = GetDirectionName(_player.GlobalPosition - GlobalPosition);

        _animatedSprite.Play(attackAnimation);

        var damage = _randomNumberGenerator.RandiRange(1, 5);
        _player.ApplyDamage(damage);
    }

    private void OnAnimationFinished()
    {
        var animationName = _animatedSprite.Animation.ToString();

        if (animationName.StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
        {
            _attacking = false;
            return;
        }

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
            AddAnimationFrames(spriteFrames, $"walk_{direction}", "walk", direction);
            AddAnimationFrames(spriteFrames, $"{AttackAnimation}_{direction}", "cross-punch", direction);
            AddAnimationFrames(spriteFrames, $"{DeathAnimation}_{direction}", DeathAnimation.ToString(), direction);
        }

        return spriteFrames;
    }

    private void AddAnimationFrames(SpriteFrames spriteFrames, string animationName, string assetFolder, string direction)
    {
        if (!FileExistsForFrame(assetFolder, direction, 0))
            return;

        spriteFrames.AddAnimation(animationName);
        spriteFrames.SetAnimationLoop(animationName, animationName.StartsWith("walk_", StringComparison.Ordinal));
        var frameLoaded = 0;

        var frame = 0;
        while (frame <= 999)
        {
            var resourcePath = $"res://assets/skeleton/animations/{assetFolder}/{direction}/frame_{frame:000}.png";
            var absolutePath = ProjectSettings.GlobalizePath(resourcePath);
            if (!FileAccess.FileExists(absolutePath))
                break;

            var image = Image.LoadFromFile(absolutePath);
            if (image == null)
            {
                GD.PrintErr($"Skeleton failed to load frame image at '{resourcePath}'.");
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
            GD.PrintErr(
                $"Skeleton animation '{animationName}' has no frames at assets/skeleton/animations/{assetFolder}/{direction}/");
        }
    }

    private void StartDeath()
    {
        _attacking = false;
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;

        if (DisableCollisionOnDeath && _collisionShape != null)
            _collisionShape.Disabled = true;

        var deathAnimation = $"{DeathAnimation}_{_lastDirection}";
        if (_animatedSprite.SpriteFrames != null &&
            _animatedSprite.SpriteFrames.GetFrameCount(deathAnimation) > 0)
        {
            _animatedSprite.Play(deathAnimation);
            return;
        }

        _animatedSprite.Stop();
        SetPhysicsProcess(false);
    }

    private bool FileExistsForFrame(string assetFolder, string direction, int frame)
    {
        var resourcePath = $"res://assets/skeleton/animations/{assetFolder}/{direction}/frame_{frame:000}.png";
        var absolutePath = ProjectSettings.GlobalizePath(resourcePath);
        return FileAccess.FileExists(absolutePath);
    }
}
