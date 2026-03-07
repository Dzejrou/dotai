using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Player : CharacterBody2D
{
    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Export]
    public float Speed { get; set; } = 140.0f;

    [Export]
    public int MaxHealth { get; set; } = 20;

    [Export]
    public float AttackRange { get; set; } = 28.0f;

    [Export]
    public float AttackCooldown { get; set; } = 0.5f;

    [Export]
    public float AttackArcDegrees { get; set; } = 70.0f;

    [Export]
    public int MaxAttackDamage { get; set; } = 5;

    [Export]
    public int MinAttackDamage { get; set; } = 2;

    private int _health;
    private bool _isDead;
    private AnimatedSprite2D _animatedSprite;
    private string _lastDirection = "south";
    private readonly RandomNumberGenerator _random = new();
    private readonly HashSet<Node> _hitThisAttack = new();
    private float _attackCooldownTimer;
    private bool _isAttacking;

    public override void _Ready()
    {
        _health = Math.Max(1, MaxHealth);
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _animatedSprite.SpriteFrames = BuildSpriteFrames();
        _animatedSprite.Animation = "walk_south";
        _animatedSprite.Play();
        _animatedSprite.AnimationFinished += OnAnimationFinished;
    }

    public override void _PhysicsProcess(double delta)
    {
        var direction = Vector2.Zero;

        if (_isAttacking)
        {
            Velocity = Vector2.Zero;
            ApplySlashDamage();
            return;
        }

        if (_attackCooldownTimer > 0.0f)
            _attackCooldownTimer -= (float)delta;

        if (Input.IsKeyPressed(Key.E) && _attackCooldownTimer <= 0.0f)
        {
            if (direction == Vector2.Zero)
                UpdateDirectionFromNearestEnemy();
            else
                _lastDirection = GetDirectionName(direction);

            StartAttack();
            return;
        }

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
            direction.X -= 1.0f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
            direction.X += 1.0f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
            direction.Y -= 1.0f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
            direction.Y += 1.0f;

        if (direction == Vector2.Zero)
        {
            Velocity = Vector2.Zero;
            _animatedSprite.Stop();
            return;
        }

        direction = direction.Normalized();
        _lastDirection = GetDirectionName(direction);
        Velocity = direction * Speed;
        MoveAndSlide();

        var animationName = $"walk_{_lastDirection}";
        if (_animatedSprite.Animation != animationName)
            _animatedSprite.Play(animationName);
    }

    private void StartAttack()
    {
        if (_isAttacking || _attackCooldownTimer > 0.0f)
            return;

        _isAttacking = true;
        _attackCooldownTimer = AttackCooldown;
        _hitThisAttack.Clear();

        var attackAnimation = $"slash_{_lastDirection}";
        if (_animatedSprite.SpriteFrames == null || _animatedSprite.SpriteFrames.GetFrameCount(attackAnimation) == 0)
        {
            ApplySlashDamage();
            _isAttacking = false;
            return;
        }

        _animatedSprite.Play(attackAnimation);
        ApplySlashDamage();
    }

    private void OnAnimationFinished()
    {
        if (_animatedSprite.Animation.ToString().StartsWith("slash_", StringComparison.Ordinal))
            _isAttacking = false;
    }

    public void ApplyDamage(int amount)
    {
        if (_isDead)
            return;

        var damage = Math.Max(1, amount);
        _health = Math.Max(0, _health - damage);

        ShowFloatingDamageNumber(damage);
        GD.Print($"Player health: {_health}");

        if (_health <= 0)
        {
            _isDead = true;
            EmitSignal(SignalName.PlayerDied);
            QueueFree();
        }
    }

    private void ApplySlashDamage()
    {
        if (_isDead)
            return;

        var facingVector = GetDirectionVector(_lastDirection);
        var minimumDot = Mathf.Cos(Mathf.DegToRad(AttackArcDegrees / 2.0f));

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (_hitThisAttack.Contains(node) || node is not Skeleton enemy)
                continue;

            if (!IsInstanceValid(enemy) || !enemy.IsInsideTree())
                continue;

            var toEnemy = enemy.GlobalPosition - GlobalPosition;
            if (toEnemy.Length() > AttackRange)
                continue;

            if (toEnemy == Vector2.Zero)
            {
                ApplyDamageToEnemy(enemy);
                continue;
            }

            if (facingVector.Dot(toEnemy.Normalized()) < minimumDot)
                continue;

            ApplyDamageToEnemy(enemy);
        }
    }

    private void ApplyDamageToEnemy(Skeleton enemy)
    {
        if (enemy == null || !_hitThisAttack.Add(enemy))
            return;

        var maxDamage = Math.Max(MinAttackDamage, MaxAttackDamage);
        var damage = _random.RandiRange(Math.Min(MinAttackDamage, maxDamage), maxDamage);
        enemy.ApplyDamage(damage);
    }

    private void UpdateDirectionFromNearestEnemy()
    {
        var nearestEnemy = FindClosestEnemy();
        if (nearestEnemy == null)
            return;

        var toEnemy = nearestEnemy.GlobalPosition - GlobalPosition;
        if (toEnemy != Vector2.Zero)
            _lastDirection = GetDirectionName(toEnemy);
    }

    private Skeleton FindClosestEnemy()
    {
        Skeleton closest = null;
        var closestDistance = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Skeleton enemy || !IsInstanceValid(enemy) || !enemy.IsInsideTree())
                continue;

            var distance = (enemy.GlobalPosition - GlobalPosition).Length();
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = enemy;
        }

        return closest;
    }

    private static string GetDirectionName(Vector2 direction)
    {
        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
            return direction.X > 0.0f ? "east" : "west";

        return direction.Y > 0.0f ? "south" : "north";
    }

    private static Vector2 GetDirectionVector(string direction)
    {
        return direction switch
        {
            "east" => Vector2.Right,
            "west" => Vector2.Left,
            "north" => Vector2.Up,
            _ => Vector2.Down,
        };
    }

    private void ShowFloatingDamageNumber(int amount)
    {
        var popup = new Node2D
        {
            GlobalPosition = GlobalPosition + new Vector2(0, -16.0f)
        };

        var label = new Label
        {
            Text = amount.ToString(),
            Modulate = new Color(1.0f, 0.0f, 0.0f, 1.0f),
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
        tween.Finished += () => popup.QueueFree();
    }

    private SpriteFrames BuildSpriteFrames()
    {
        var spriteFrames = new SpriteFrames();
        var directions = new[] { "south", "east", "north", "west" };

        foreach (var direction in directions)
        {
            AddAnimationFrames(spriteFrames, $"walk_{direction}", "walk", direction, true);
            AddAnimationFrames(spriteFrames, $"slash_{direction}", "slash", direction, false);
        }

        return spriteFrames;
    }

    private void AddAnimationFrames(
        SpriteFrames spriteFrames,
        string animationName,
        string assetFolder,
        string direction,
        bool loops)
    {
        spriteFrames.AddAnimation(animationName);
        spriteFrames.SetAnimationLoop(animationName, loops);
        var frameLoaded = 0;

        var frame = 0;
        while (frame <= 999)
        {
            var path = $"res://assets/player/animations/{assetFolder}/{direction}/frame_{frame:000}.png";
            var absolutePath = ProjectSettings.GlobalizePath(path);
            if (!FileAccess.FileExists(absolutePath))
                break;

            var image = Image.LoadFromFile(absolutePath);
            if (image == null)
            {
                GD.PrintErr($"Player failed to load frame image at '{path}'.");
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
            GD.PrintErr($"Player animation '{animationName}' has no frames at assets/player/animations/{assetFolder}/{direction}/");
        }
    }
}
