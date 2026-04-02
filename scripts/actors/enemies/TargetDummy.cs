using Godot;

using System;

[GlobalClass]
public partial class TargetDummy : WorldObject, IAttackable, ITargetable, IFactionMember
{
    private static readonly Color InactiveModulate = new Color(0.55f, 0.55f, 0.55f, 0.45f);
    private const string DefaultVisualDirection = "south";

    [Export]
    public int MaxHealth { get; set; } = 99;

    [Export]
    public float RespawnDelaySeconds { get; set; } = 30.0f;

    [Export(PropertyHint.Enum, "east,south-east,south,south-west,west,north-west,north,north-east")]
    public string VisualDirection { get; set; } = DefaultVisualDirection;

    [Export] public Texture2D EastTexture { get; set; }
    [Export] public Texture2D SouthEastTexture { get; set; }
    [Export] public Texture2D SouthTexture { get; set; }
    [Export] public Texture2D SouthWestTexture { get; set; }
    [Export] public Texture2D WestTexture { get; set; }
    [Export] public Texture2D NorthWestTexture { get; set; }
    [Export] public Texture2D NorthTexture { get; set; }
    [Export] public Texture2D NorthEastTexture { get; set; }

    public bool CanBeTargeted => !_isDead;
    public Faction Faction => _faction.Current;

    private Timer _respawnTimer;
    private ActorHUD _actorHud;
    private StatusEffectController _statusEffectController;
    private FactionState _faction;
    private Vector2 _spawnPosition;
    private int _currentHealth;
    private bool _isDead;

    private int ResolvedMaxHealth => Math.Max(1, MaxHealth);

    public override void _Ready()
    {
        InitializeWorldObject();
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
        EnsureStatusEffectController();

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

        UnbindStatusEffects();
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
        UpdateHud();
        ClearStatuses();
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
        UpdateHud();
        ClearStatuses();
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

    private void ApplyActiveVisualState()
    {
        if (VisualSprite == null)
            return;

        ApplyVisualState(ResolveVisualTexture(), Colors.White);
    }

    private void ApplyInactiveVisualState()
    {
        if (VisualSprite == null)
            return;

        ApplyVisualState(ResolveVisualTexture(), InactiveModulate);
    }

    private void ApplyVisualState(Texture2D texture, Color modulate)
    {
        if (VisualSprite == null)
            return;

        VisualSprite.Texture = texture;
        VisualSprite.Modulate = modulate;
    }

    private void EnsureStatusEffectController()
    {
        _statusEffectController = GetNodeOrNull<StatusEffectController>("StatusEffectController");
        if (_statusEffectController == null)
        {
            _statusEffectController = new StatusEffectController
            {
                Name = "StatusEffectController",
            };
            AddChild(_statusEffectController);
        }

        _statusEffectController.Connect(
            StatusEffectController.SignalName.StatusVisualStateChanged,
            new Callable(this, nameof(OnStatusVisualStateChanged)));

        OnStatusVisualStateChanged(PoisonedEffect.StatusKeyName, _statusEffectController.HasStatus(PoisonedEffect.StatusKeyName));
    }

    private void UnbindStatusEffects()
    {
        if (_statusEffectController == null)
            return;

        var callable = new Callable(this, nameof(OnStatusVisualStateChanged));
        if (_statusEffectController.IsConnected(StatusEffectController.SignalName.StatusVisualStateChanged, callable))
            _statusEffectController.Disconnect(StatusEffectController.SignalName.StatusVisualStateChanged, callable);
    }

    private void ClearStatuses()
    {
        _statusEffectController?.ClearAllEffects();
        _actorHud?.SetPoisoned(false);
    }

    private void OnStatusVisualStateChanged(StringName statusKey, bool active)
    {
        if (statusKey != PoisonedEffect.StatusKeyName)
            return;

        _actorHud?.SetPoisoned(active);
    }

    private Texture2D ResolveVisualTexture()
    {
        var requestedDirection = string.IsNullOrWhiteSpace(VisualDirection) ? DefaultVisualDirection : VisualDirection;
        var fallbackDirection = DirectionHelper.GetCardinalFallbackDirectionName(requestedDirection);

        return ResolveTextureForDirection(requestedDirection) ??
               ResolveTextureForDirection(fallbackDirection) ??
               SouthTexture ??
               EastTexture ??
               WestTexture ??
               NorthTexture;
    }

    private Texture2D ResolveTextureForDirection(string direction)
    {
        return direction switch
        {
            "east" => EastTexture,
            "south-east" => SouthEastTexture,
            "south" => SouthTexture,
            "south-west" => SouthWestTexture,
            "west" => WestTexture,
            "north-west" => NorthWestTexture,
            "north" => NorthTexture,
            "north-east" => NorthEastTexture,
            _ => SouthTexture,
        };
    }
}
