using Godot;

using System;

[GlobalClass]
public partial class Ogre : EnemyBase, IAttackable
{
    [Export]
    public float Speed { get; set; } = 64.0f;

    [Export]
    public float AttackRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.2f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cross-punch";

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

    private RandomNumberGenerator _randomNumberGenerator = new();
    private float _attackCooldownTimer;
    private float _healthRegenTimer;
    private int _currentHealth;
    private bool _isDead;
    private bool _attacking;
    public override void _Ready()
    {
        _currentHealth = Math.Max(1, MaxHealth);
        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            "Ogre");
        AnimatedSprite.SpriteFrames = BuildSpriteFrames();

        if (AnimatedSprite.SpriteFrames.HasAnimation("walk_south"))
        {
            AnimatedSprite.Animation = "walk_south";
            AnimatedSprite.Play();
        }

        AnimatedSprite.AnimationFinished += OnAnimationFinished;

        _randomNumberGenerator.Randomize();
        _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        HandleHealthRegeneration((float)delta);

        if (!ValidateCurrentTarget())
        {
            AcquireTarget();

            if (!ValidateCurrentTarget())
            {
                ClearTarget();
                Velocity = Vector2.Zero;
                AnimatedSprite.Stop();
                return;
            }
        }

        if (CurrentTarget == null || !IsInstanceValid(CurrentTarget) || !CurrentTarget.IsInsideTree())
        {
            ClearTarget();
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

        var toTarget = CurrentTarget.GlobalPosition - GlobalPosition;
        if (toTarget.Length() <= AttackRange)
        {
            Velocity = Vector2.Zero;
            TryAttackPlayer();
            return;
        }

        if (toTarget == Vector2.Zero)
        {
            Velocity = Vector2.Zero;
            AnimatedSprite.Stop();
            return;
        }

        LastDirection = DirectionHelper.GetDirectionName(toTarget);
        var walkAnimation = $"walk_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null && AnimatedSprite.SpriteFrames.HasAnimation(walkAnimation))
            AnimatedSprite.Play(walkAnimation);

        Velocity = toTarget.Normalized() * Speed;
        MoveAndSlide();
    }

    public void ApplyDamage(int amount)
    {
        if (_isDead)
            return;

        var damage = Math.Max(1, amount);
        _currentHealth = Math.Max(0, _currentHealth - damage);
        GD.Print($"Ogre health: {_currentHealth}/{MaxHealth} (took {damage})");

        if (_currentHealth <= 0)
        {
            _isDead = true;
            StartDeath();
        }
    }

    private void TryAttackPlayer()
    {
        if (_attackCooldownTimer > 0.0f || _isDead || CurrentTarget is not IAttackable attackable)
            return;

        _attackCooldownTimer = AttackCooldown;
        _attacking = true;

        var attackAnimation = $"{AttackAnimation}_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(attackAnimation) &&
            AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimation) > 0)
        {
            AnimatedSprite.Play(attackAnimation);
        }
        else
        {
            _attacking = false;
        }

        var maxDamage = Math.Max(MinAttackDamage, MaxAttackDamage);
        var damage = _randomNumberGenerator.RandiRange(Math.Min(MinAttackDamage, maxDamage), maxDamage);
        attackable.ApplyDamage(damage);
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
        if (AnimatedSprite.Animation.ToString().StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
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
            RuntimeSpriteLoader.AddAnimationFrames(
                spriteFrames,
                $"walk_{direction}",
                "assets/ogre/animations",
                "walk",
                direction,
                true,
                "Ogre",
                true);
            RuntimeSpriteLoader.AddAnimationFrames(
                spriteFrames,
                $"{DeathAnimation}_{direction}",
                "assets/ogre/animations",
                "falling-back-death",
                direction,
                false,
                "Ogre",
                true);
            RuntimeSpriteLoader.AddAnimationFrames(
                spriteFrames,
                $"{AttackAnimation}_{direction}",
                "assets/ogre/animations",
                "cross-punch",
                direction,
                false,
                "Ogre",
                true);
        }

        return spriteFrames;
    }

    private void StartDeath()
    {
        Velocity = Vector2.Zero;
        _attackCooldownTimer = 0.0f;
        TryPlayDeathAnimation();
    }

    private void ShowFloatingHealingNumber(int amount)
    {
        if (amount <= 0)
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, $"+{amount}", new Color(0.0f, 1.0f, 0.0f, 1.0f));
    }

}
