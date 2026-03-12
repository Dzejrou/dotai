using Godot;

using System;

[GlobalClass]
public partial class Ogre : EnemyBase, IAttackable, ITargetable
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

    public override void _Ready()
    {
        _currentHealth = Math.Max(1, MaxHealth);
        InitializeEnemy(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            "Ogre");
        SetMovementSpeed(Speed);

        PlayIdleIfAvailable();

        AnimatedSprite.AnimationFinished += OnAnimationFinished;

        _randomNumberGenerator.Randomize();
        _healthRegenTimer = Math.Max(HealthRegenerationInterval, 0.0f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        HandleHealthRegeneration((float)delta);
        base._PhysicsProcess(delta);
    }

    protected override bool CanAttackNow(Vector2 toTarget, double delta)
    {
        if (_attackCooldownTimer > 0.0f)
            _attackCooldownTimer -= (float)delta;

        return _attackCooldownTimer <= 0.0f && toTarget.Length() <= AttackRange;
    }

    protected override bool ShouldStayEngaged(Vector2 toTarget, double delta)
    {
        return toTarget.Length() <= AttackRange;
    }

    protected override void StartAttack()
    {
        if (_attackCooldownTimer > 0.0f || _isDead ||
            CurrentTarget is not IAttackable attackable ||
            CurrentTarget is not ITargetable targetable ||
            !targetable.CanBeTargeted)
            return;

        _attackCooldownTimer = AttackCooldown;
        SetCombatState(CombatUnitState.Attacking);

        var attackAnimation = $"{AttackAnimation}_{LastDirection}";
        if (AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(attackAnimation) &&
            AnimatedSprite.SpriteFrames.GetFrameCount(attackAnimation) > 0)
        {
            AnimatedSprite.Play(attackAnimation);
        }
        else
        {
            SetCombatState(CombatUnitState.PursuingTarget);
        }

        var maxDamage = Math.Max(MinAttackDamage, MaxAttackDamage);
        var damage = _randomNumberGenerator.RandiRange(Math.Min(MinAttackDamage, maxDamage), maxDamage);
        attackable.ApplyDamage(new DamageInfo(damage, this));
    }

    public bool CanBeTargeted => !_isDead;

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (_isDead)
            return;

        if (!TryReactToDamageSource(damageInfo))
            return;

        var damage = Math.Max(1, damageInfo.Amount);
        _currentHealth = Math.Max(0, _currentHealth - damage);
        GD.Print($"Ogre health: {_currentHealth}/{MaxHealth} (took {damage})");

        if (_currentHealth <= 0)
        {
            _isDead = true;
            StartDeath();
        }
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
            FinishAttackState();
            return;
        }

        TryFinalizeDeathAnimation();
    }

    private void StartDeath()
    {
        MarkDead();
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
