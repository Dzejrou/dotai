using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Player : CharacterBody2D, IAttackable, ITargetable
{
    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void HealthChangedEventHandler(int health, int maxHealth);

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

    [Export]
    public float HealthRegenerationInterval { get; set; } = 5.0f;

    [Export]
    public int HealthRegenerationAmount { get; set; } = 1;

    [Export]
    public float HealthRegenerationDelayAfterDamage { get; set; } = 5.0f;

    [Export]
    public PackedScene FireballScene { get; set; }

    [Export]
    public float FireballSpeed { get; set; } = 280.0f;

    [Export]
    public int FireballDamage { get; set; } = 4;

    [Export]
    public float FireballLifetime { get; set; } = 2.5f;

    [Export]
    public float FireballMaxDistance { get; set; } = 320.0f;

    private int _health;
    private bool _isDead;
    private AnimatedSprite2D _animatedSprite;
    private string _lastDirection = "south";
    private readonly RandomNumberGenerator _random = new();
    private readonly HashSet<Node> _hitThisAttack = new();
    private float _attackCooldownTimer;
    private bool _isAttacking;
    private float _healthRegenTimer;
    private float _healthRegenDelayTimer;

    public int CurrentHealth => _health;
    public bool CanBeTargeted => !_isDead;

    public override void _Ready()
    {
        _health = Math.Max(1, MaxHealth);
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _animatedSprite.Animation = "walk_south";
        _animatedSprite.Play();
        _animatedSprite.AnimationFinished += OnAnimationFinished;
        AddToGroup(CombatGroups.Allies);

        EmitSignal(SignalName.HealthChanged, _health, MaxHealth);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        HandleHealthRegenerationDelay((float)delta);
        HandleHealthRegeneration((float)delta);
        if (Input.IsActionJustPressed("cast_spell"))
            CastFireball();

        var direction = Vector2.Zero;
        if (Input.IsActionPressed("move_left"))
            direction.X -= 1.0f;
        if (Input.IsActionPressed("move_right"))
            direction.X += 1.0f;
        if (Input.IsActionPressed("move_up"))
            direction.Y -= 1.0f;
        if (Input.IsActionPressed("move_down"))
            direction.Y += 1.0f;

        if (_isAttacking)
        {
            Velocity = Vector2.Zero;
            ApplySlashDamage();
            return;
        }

        if (_attackCooldownTimer > 0.0f)
            _attackCooldownTimer -= (float)delta;

        if (Input.IsActionPressed("attack") && _attackCooldownTimer <= 0.0f)
        {
            if (direction != Vector2.Zero)
                _lastDirection = DirectionHelper.GetDirectionName(direction);

            StartAttack();
            return;
        }

        if (direction == Vector2.Zero)
        {
            Velocity = Vector2.Zero;
            _animatedSprite.Stop();
            return;
        }

        direction = direction.Normalized();
        _lastDirection = DirectionHelper.GetDirectionName(direction);
        var isSprinting = Input.IsActionPressed("sprint");
        var moveSpeed = isSprinting ? Speed * 2.0f : Speed;
        Velocity = direction * moveSpeed;
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

    private void CastFireball()
    {
        if (_isDead || FireballScene == null)
            return;

        var nearestTarget = TargetingHelper.FindClosestTarget(this, CombatGroups.Enemies, node => node is IAttackable && node is ITargetable targetable && targetable.CanBeTargeted);
        var fireDirection = DirectionHelper.GetDirectionVector(_lastDirection);
        if (nearestTarget != null)
        {
            var toTarget = nearestTarget.GlobalPosition - GlobalPosition;
            if (toTarget != Vector2.Zero)
                fireDirection = toTarget.Normalized();
        }

        var fireball = FireballScene.Instantiate<Projectile>();
        if (fireball == null)
            return;

        var parent = GetParent();
        if (parent == null)
            return;

        fireball.GlobalPosition = GlobalPosition;
        parent.AddChild(fireball);
        fireball.Initialize(
            fireDirection,
            this,
            FireballDamage,
            FireballSpeed,
            FireballLifetime,
            FireballMaxDistance,
            CombatGroups.Enemies);
    }

    public void ApplyDamage(int amount)
    {
        if (_isDead)
            return;

        var damage = Math.Max(1, amount);
        _health = Math.Max(0, _health - damage);

        ShowFloatingDamageNumber(damage);
        EmitSignal(SignalName.HealthChanged, _health, MaxHealth);
        _healthRegenDelayTimer = Math.Max(HealthRegenerationDelayAfterDamage, 0.0f);
        GD.Print($"Player health: {_health}/{MaxHealth} (took {damage})");

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

        var facingVector = DirectionHelper.GetDirectionVector(_lastDirection);
        var minimumDot = Mathf.Cos(Mathf.DegToRad(AttackArcDegrees / 2.0f));

        foreach (var node in GetTree().GetNodesInGroup(CombatGroups.Enemies))
        {
            if (_hitThisAttack.Contains(node) || node is not IAttackable attackable || node is not ITargetable targetable || !targetable.CanBeTargeted)
                continue;

            if (!IsInstanceValid(node) || node is not Node2D enemyNode || !enemyNode.IsInsideTree())
                continue;

            var toEnemy = enemyNode.GlobalPosition - GlobalPosition;
            if (toEnemy.Length() > AttackRange)
                continue;

            if (toEnemy == Vector2.Zero)
            {
                ApplyDamageToEnemy(node, attackable);
                continue;
            }

            if (facingVector.Dot(toEnemy.Normalized()) < minimumDot)
                continue;

            ApplyDamageToEnemy(node, attackable);
        }
    }

    private void ApplyDamageToEnemy(Node node, IAttackable enemy)
    {
        if (enemy == null || !_hitThisAttack.Add(node))
            return;

        var maxDamage = Math.Max(MinAttackDamage, MaxAttackDamage);
        var damage = _random.RandiRange(Math.Min(MinAttackDamage, maxDamage), maxDamage);
        enemy.ApplyDamage(damage);
    }

    private void UpdateDirectionFromNearestEnemy()
    {
        var nearestEnemy = TargetingHelper.FindClosestTarget(this, CombatGroups.Enemies, node => node is IAttackable && node is ITargetable targetable && targetable.CanBeTargeted);
        if (nearestEnemy == null)
            return;

        var toEnemy = nearestEnemy.GlobalPosition - GlobalPosition;
        if (toEnemy != Vector2.Zero)
            _lastDirection = DirectionHelper.GetDirectionName(toEnemy);
    }

    private void HandleHealthRegeneration(float delta)
    {
        if (_health >= MaxHealth)
        {
            _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
            return;
        }

        _healthRegenTimer -= delta;
        if (_healthRegenTimer > 0.0f)
            return;

        if (_health < MaxHealth)
        {
            var missingHealth = MaxHealth - _health;
            var recovered = Math.Clamp(HealthRegenerationAmount, 1, missingHealth);
            ShowFloatingHealingNumber(recovered);
            _health += recovered;
            EmitSignal(SignalName.HealthChanged, _health, MaxHealth);
        }

        var interval = Math.Max(HealthRegenerationInterval, 0.0f);
        if (interval == 0.0f)
            _healthRegenTimer = 0.0f;
        else
            _healthRegenTimer = interval;
    }

    private void HandleHealthRegenerationDelay(float delta)
    {
        if (_healthRegenDelayTimer <= 0.0f)
            return;

        _healthRegenDelayTimer -= delta;
        _healthRegenDelayTimer = Math.Max(0.0f, _healthRegenDelayTimer);
        _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
    }

    private void ShowFloatingDamageNumber(int amount)
    {
        FloatingNumberHelper.ShowFloatingNumber(this, amount.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
    }

    private void ShowFloatingHealingNumber(int amount)
    {
        if (amount <= 0)
            return;

        FloatingNumberHelper.ShowFloatingNumber(this, $"+{amount}", new Color(0.0f, 1.0f, 0.0f, 1.0f));
    }

}
