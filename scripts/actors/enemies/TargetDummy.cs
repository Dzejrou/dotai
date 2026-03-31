using Godot;

using System;

[GlobalClass]
public partial class TargetDummy : CharacterBody2D, IAttackable, ITargetable, IFactionMember
{
    private static readonly Color InactiveModulate = new Color(0.55f, 0.55f, 0.55f, 0.45f);
    private const string DefaultVisualDirection = "south";

    [Export]
    public int MaxHealth { get; set; } = 99;

    [Export]
    public float RespawnDelaySeconds { get; set; } = 30.0f;

    [Export(PropertyHint.Enum, "south,east,west,north")]
    public string VisualDirection { get; set; } = DefaultVisualDirection;

    public bool CanBeTargeted => !_isDead;
    public Faction Faction => _faction.Current;

    private AnimatedSprite2D _animatedSprite;
    private CollisionShape2D _collisionShape;
    private Timer _respawnTimer;
    private ActorHUD _actorHud;
    private FactionState _faction;
    private Vector2 _spawnPosition;
    private int _currentHealth;
    private bool _isDead;

    private int ResolvedMaxHealth => Math.Max(1, MaxHealth);

    public override void _Ready()
    {
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _respawnTimer = GetNodeOrNull<Timer>("RespawnTimer");
        _faction = GetNode<FactionState>("FactionState");
        _spawnPosition = GlobalPosition;
        _currentHealth = ResolvedMaxHealth;
        _isDead = false;
        _actorHud = GetNodeOrNull<ActorHUD>("ActorHUD");
        if (_actorHud == null)
            GD.PushError($"{GetPath()}: missing required ActorHUD child.");
        else
            _actorHud.Bind(this);

        AddToGroup(CombatGroups.Actors);
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

        ApplyVisualState(Colors.White);
    }

    private void ApplyInactiveVisualState()
    {
        if (_animatedSprite == null)
            return;

        ApplyVisualState(InactiveModulate);
    }

    private void ApplyVisualState(Color modulate)
    {
        if (_animatedSprite == null)
            return;

        _animatedSprite.Stop();
        _animatedSprite.Modulate = modulate;

        var animationName = ResolveVisualAnimationName();
        if (animationName.IsEmpty)
            return;

        _animatedSprite.Animation = animationName;
        _animatedSprite.SetFrameAndProgress(0, 0.0f);
    }

    private StringName ResolveVisualAnimationName()
    {
        if (_animatedSprite?.SpriteFrames == null)
            return new StringName();

        var spriteFrames = _animatedSprite.SpriteFrames;
        var requestedDirection = ResolveCardinalVisualDirection();
        if (spriteFrames.HasAnimation(requestedDirection))
            return requestedDirection;

        if (spriteFrames.HasAnimation(DefaultVisualDirection))
            return DefaultVisualDirection;

        var animationNames = spriteFrames.GetAnimationNames();
        return animationNames.Length > 0 ? animationNames[0] : new StringName();
    }

    private string ResolveCardinalVisualDirection()
    {
        var requestedDirection = string.IsNullOrWhiteSpace(VisualDirection) ? DefaultVisualDirection : VisualDirection;
        return DirectionHelper.GetCardinalFallbackDirectionName(requestedDirection);
    }
}
