using Godot;

using System;

[GlobalClass]
public partial class Skeleton : EnemyBase, IEnemyTarget
{
    [Export]
    public float Speed { get; set; } = 52.0f;

    [Export]
    public float AttackRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.1f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cross-punch";

    [Export]
    public int Health { get; set; } = 24;

    private RandomNumberGenerator _randomNumberGenerator = new();
    private float _attackCooldownTimer;
    private bool _attacking;
    private int _currentHealth;
    private bool _isDead;

    public override void _Ready()
    {
        _currentHealth = Math.Max(1, Health);
        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            "Skeleton");
        AnimatedSprite.SpriteFrames = BuildSpriteFrames();
        AnimatedSprite.Animation = "walk_south";
        AnimatedSprite.Play();
        AnimatedSprite.AnimationFinished += OnAnimationFinished;

        _randomNumberGenerator.Randomize();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        if (!ValidateCurrentTarget())
        {
            AcquireTarget();

            if (!ValidateCurrentTarget())
            {
                Velocity = Vector2.Zero;
                AnimatedSprite.Stop();
                return;
            }
        }

        if (Player == null || !IsInstanceValid(Player) || !Player.IsInsideTree())
        {
            Velocity = Vector2.Zero;
            AnimatedSprite.Stop();
            return;
        }

        if (_attackCooldownTimer > 0.0f)
            _attackCooldownTimer -= (float)delta;

        if (_attacking)
        {
            Velocity = Vector2.Zero;
            return;
        }

        var toPlayer = Player.GlobalPosition - GlobalPosition;
        if (toPlayer.Length() <= AttackRange && _attackCooldownTimer <= 0.0f)
        {
            StartAttack();
            return;
        }

        if (toPlayer == Vector2.Zero)
        {
            Velocity = Vector2.Zero;
            AnimatedSprite.Stop();
            return;
        }

        LastDirection = DirectionHelper.GetDirectionName(toPlayer);
        var walkAnimation = $"walk_{LastDirection}";
        if (AnimatedSprite.Animation != walkAnimation)
            AnimatedSprite.Play(walkAnimation);

        Velocity = toPlayer.Normalized() * Speed;
        MoveAndSlide();
    }

    public void ApplyDamage(int amount)
    {
        if (_isDead)
            return;

        var damage = Math.Max(1, amount);
        _currentHealth = Math.Max(0, _currentHealth - damage);
        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        GD.Print($"Skeleton health: {_currentHealth}/{Health} (took {damage})");
        if (_currentHealth <= 0)
        {
            _isDead = true;
            StartDeath();
        }
    }

    private void StartAttack()
    {
        if (Player == null || !Player.IsInsideTree())
        {
            ClearPlayer();
            return;
        }

        _attacking = true;
        _attackCooldownTimer = AttackCooldown;

        var attackAnimation = $"{AttackAnimation}_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimation) == 0)
        {
            _attacking = false;
            Player.ApplyDamage(_randomNumberGenerator.RandiRange(1, 5));
            return;
        }

        if (Player != null && Player.GlobalPosition != Vector2.Zero)
            LastDirection = DirectionHelper.GetDirectionName(Player.GlobalPosition - GlobalPosition);

        AnimatedSprite.Play(attackAnimation);

        var damage = _randomNumberGenerator.RandiRange(1, 5);
        Player.ApplyDamage(damage);
    }

    private void OnAnimationFinished()
    {
        var animationName = AnimatedSprite.Animation.ToString();

        if (animationName.StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
        {
            _attacking = false;
            return;
        }

        TryFinalizeDeathAnimation();
    }

    private SpriteFrames BuildSpriteFrames()
    {
        var spriteFrames = new SpriteFrames();
        foreach (var direction in DirectionHelper.GetCardinalDirections())
        {
            AddWalkAndAttackAndDeathFrames(spriteFrames, direction);
        }

        return spriteFrames;
    }

    private void AddWalkAndAttackAndDeathFrames(SpriteFrames spriteFrames, string direction)
    {
        AddSpriteAnimation(spriteFrames, $"walk_{direction}", "walk", direction, true);
        AddSpriteAnimation(spriteFrames, $"{AttackAnimation}_{direction}", "cross-punch", direction, false);
        AddSpriteAnimation(spriteFrames, $"{DeathAnimation}_{direction}", DeathAnimation.ToString(), direction, false);
    }

    private void AddSpriteAnimation(SpriteFrames spriteFrames, string animationName, string assetFolder, string direction, bool loops)
    {
        if (!RuntimeSpriteLoader.HasFrame("assets/skeleton/animations", assetFolder, direction, 0))
            return;

        RuntimeSpriteLoader.AddAnimationFrames(
            spriteFrames,
            animationName,
            "assets/skeleton/animations",
            assetFolder,
            direction,
            loops,
            "Skeleton",
            false);
    }

    private void StartDeath()
    {
        _attacking = false;
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;

        TryPlayDeathAnimation();
    }

}
