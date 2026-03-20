using Godot;

using System;

[GlobalClass]
public partial class TargetDummy : CharacterBody2D, IAttackable, ITargetable, IFactionMember
{
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
    private ActorHUD _actorHud;
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
        _actorHud = GetNodeOrNull<ActorHUD>("ActorHUD");
        if (_actorHud == null)
            GD.PushError($"{GetPath()}: missing required ActorHUD child.");
        else
            _actorHud.Bind(this);

        // Compatibility only: keep the neutral dummy discoverable by existing target enumeration.
        AddToGroup(CombatGroups.Enemies);
        UpdateHud();
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
        damageInfo.RegisterHit(this, setReceiverTargetToSource: false);
        UpdateHud();
        _actorHud?.ShowFloatingText(damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));

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
        UpdateHud();
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
        UpdateHud();
        SetCollisionEnabled(true);
        ApplyActiveVisualState();
    }

    private void UpdateHud()
    {
        if (_actorHud == null)
            return;

        _actorHud.SetHealth(_currentHealth, ResolvedMaxHealth);
        _actorHud.SetFaction(Faction);
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
