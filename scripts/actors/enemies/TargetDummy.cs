using Godot;

using System;

[GlobalClass]
public partial class TargetDummy : CharacterBody2D, IAttackable, ITargetable, IFactionMember
{
    private static readonly Vector2 HealthLabelOffset = new Vector2(-24.0f, -36.0f);
    private static readonly Vector2 HealthLabelSize = new Vector2(48.0f, 16.0f);
    private static readonly Color InactiveModulate = new Color(0.55f, 0.55f, 0.55f, 0.45f);

    [Export]
    public int MaxHealth { get; set; } = 99;

    [Export]
    public float RespawnDelaySeconds { get; set; } = 30.0f;

    public bool CanBeTargeted => !_isDead;
    public Faction Faction => Factions.Neutral;

    private AnimatedSprite2D _animatedSprite;
    private CollisionShape2D _collisionShape;
    private Timer _respawnTimer;
    private Label _healthLabel;
    private Vector2 _spawnPosition;
    private int _currentHealth;
    private bool _isDead;

    private int ResolvedMaxHealth => Math.Max(1, MaxHealth);

    public override void _Ready()
    {
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _respawnTimer = GetNodeOrNull<Timer>("RespawnTimer");
        _spawnPosition = GlobalPosition;
        _currentHealth = ResolvedMaxHealth;
        _isDead = false;

        AddToGroup(CombatGroups.Enemies);
        EnsureHealthLabel();
        UpdateHealthLabel();
        ApplyActiveVisualState();

        if (_respawnTimer != null)
        {
            _respawnTimer.OneShot = true;
            _respawnTimer.WaitTime = Math.Max(0.01f, RespawnDelaySeconds);
            _respawnTimer.Timeout += OnRespawnTimerTimeout;
        }
    }

    public override void _ExitTree()
    {
        if (_respawnTimer != null)
            _respawnTimer.Timeout -= OnRespawnTimerTimeout;
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (_isDead)
            return;

        var damage = Math.Max(1, damageInfo.Amount);
        _currentHealth = Math.Max(0, _currentHealth - damage);
        UpdateHealthLabel();
        FloatingNumberHelper.ShowFloatingNumber(this, damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));

        if (_currentHealth <= 0)
            StartDeath();
    }

    private void StartDeath()
    {
        if (_isDead)
            return;

        _isDead = true;
        _currentHealth = 0;
        Velocity = Vector2.Zero;
        UpdateHealthLabel();
        SetCollisionEnabled(false);
        ApplyInactiveVisualState();

        if (_respawnTimer != null)
        {
            _respawnTimer.Stop();
            _respawnTimer.WaitTime = Math.Max(0.01f, RespawnDelaySeconds);
            _respawnTimer.Start();
        }
    }

    private void OnRespawnTimerTimeout()
    {
        Respawn();
    }

    private void Respawn()
    {
        GlobalPosition = _spawnPosition;
        _currentHealth = ResolvedMaxHealth;
        _isDead = false;
        Velocity = Vector2.Zero;
        UpdateHealthLabel();
        SetCollisionEnabled(true);
        ApplyActiveVisualState();
    }

    private void EnsureHealthLabel()
    {
        if (_healthLabel != null)
            return;

        _healthLabel = new Label
        {
            Name = "HealthLabel",
            Position = HealthLabelOffset,
            Size = HealthLabelSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 10
        };
        _healthLabel.AddThemeFontSizeOverride("font_size", 12);
        _healthLabel.AddThemeColorOverride("font_color", Colors.White);
        _healthLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _healthLabel.AddThemeConstantOverride("outline_size", 2);
        AddChild(_healthLabel);
    }

    private void UpdateHealthLabel()
    {
        if (_healthLabel == null)
            return;

        _healthLabel.Text = $"{_currentHealth}/{ResolvedMaxHealth}";
        _healthLabel.AddThemeColorOverride("font_color", FactionColors.Resolve(Faction));
    }

    private void SetCollisionEnabled(bool enabled)
    {
        if (_collisionShape == null)
            return;

        _collisionShape.SetDeferred("disabled", !enabled);
    }

    private void ApplyActiveVisualState()
    {
        if (_animatedSprite == null)
            return;

        _animatedSprite.Modulate = Colors.White;
        if (_animatedSprite.SpriteFrames != null && _animatedSprite.SpriteFrames.HasAnimation("breathing-idle_south"))
            _animatedSprite.Play("breathing-idle_south");
    }

    private void ApplyInactiveVisualState()
    {
        if (_animatedSprite == null)
            return;

        _animatedSprite.Stop();
        _animatedSprite.Animation = "breathing-idle_south";
        _animatedSprite.SetFrame(0);
        _animatedSprite.Modulate = InactiveModulate;
    }
}
