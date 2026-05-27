using Godot;

using System;

[GlobalClass]
public partial class ActorHUD : Node2D
{
    public enum HudPlacementMode
    {
        WorldFollow = 0,
        ScreenAnchored = 1,
    }

    private const float DefaultHealthBarWidth = 60.0f;
    private const float DefaultHealthBarHeight = 14.0f;
    private const float DefaultManaBarWidth = 60.0f;
    private const float DefaultManaBarHeight = 10.0f;
    private const float DefaultXpBarWidth = 60.0f;
    private const float DefaultXpBarHeight = 8.0f;

    private static readonly Color DefaultHealthFillColor = new Color(0.45f, 0.95f, 0.45f, 1.0f);
    private static readonly Color DefaultHealthBackgroundColor = new Color(0.16f, 0.36f, 0.16f, 0.85f);
    private static readonly Color PoisonedHealthFillColor = new Color(0.42f, 0.92f, 0.42f, 1.0f);
    private static readonly Color PoisonedHealthBackgroundColor = new Color(0.12f, 0.28f, 0.12f, 0.85f);

    [Export]
    public bool ShowName { get; set; } = true;

    [Export]
    public bool ShowMana { get; set; } = true;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public float VerticalOffset { get; set; } = -40.0f;

    [Export]
    public float HealthBarWidth { get; set; } = DefaultHealthBarWidth;

    [Export]
    public float HealthBarHeight { get; set; } = DefaultHealthBarHeight;

    [Export]
    public float ManaBarWidth { get; set; } = DefaultManaBarWidth;

    [Export]
    public float ManaBarHeight { get; set; } = DefaultManaBarHeight;

    [Export]
    public HudPlacementMode PlacementMode { get; set; } = HudPlacementMode.WorldFollow;

    [Export]
    public Vector2 ScreenAnchor { get; set; } = Vector2.Zero;

    [Export]
    public Vector2 ScreenOffset { get; set; } = Vector2.Zero;

    [Export]
    public int ScreenLayer { get; set; } = 100;

    [Export]
    public bool UseFactionHealthColors { get; set; } = true;

    [Export]
    public Color HealthTextColor { get; set; } = Colors.Black;

    [Export]
    public Color ManaTextColor { get; set; } = Colors.Black;

    [Export]
    public Color HealthFillColor { get; set; } = DefaultHealthFillColor;

    [Export]
    public Color HealthBackgroundColor { get; set; } = DefaultHealthBackgroundColor;

    [Export]
    public Color ManaFillColor { get; set; } = new Color(0.35f, 0.65f, 1.0f, 1.0f);

    [Export]
    public Color ManaBackgroundColor { get; set; } = new Color(0.16f, 0.2f, 0.3f, 0.85f);

    [Export]
    public float XpBarWidth { get; set; } = DefaultXpBarWidth;

    [Export]
    public float XpBarHeight { get; set; } = DefaultXpBarHeight;

    [Export]
    public Color XpFillColor { get; set; } = new Color(0.65f, 0.25f, 0.85f, 1.0f);

    [Export]
    public Color XpBackgroundColor { get; set; } = new Color(0.22f, 0.1f, 0.3f, 0.85f);

    private Node2D _contentRoot;
    private CanvasLayer _screenLayer;
    private Control _unitFrame;
    private Control _healthBar;
    private Label _nameLabel;
    private ColorRect _healthBackground;
    private ColorRect _healthFill;
    private Label _healthLabel;
    private Control _manaBar;
    private ColorRect _manaBackground;
    private ColorRect _manaFill;
    private Label _manaLabel;
    private Control _xpBar;
    private ColorRect _xpBackground;
    private ColorRect _xpFill;
    private Node2D _targetBracket;
    private Line2D _leftBracket;
    private Line2D _rightBracket;
    private Node2D _owner;
    private Player _playerOwner;
    private ManaState _manaState;
    private Faction _faction;
    private int _currentHealth;
    private int _maxHealth = 1;
    private int _currentXp;
    private int _requiredXp = 1;
    private int _playerLevel = 1;
    private bool _isPoisoned;

    public override void _Ready()
    {
        _contentRoot = GetNodeOrNull<Node2D>("ContentRoot");
        _screenLayer = GetNodeOrNull<CanvasLayer>("ScreenLayer");
        _unitFrame = GetNodeOrNull<Control>("ContentRoot/UnitFrame");
        _healthBar = GetNodeOrNull<Control>("ContentRoot/UnitFrame/HealthBar");
        _nameLabel = GetNodeOrNull<Label>("ContentRoot/UnitFrame/NameLabel");
        _healthBackground = GetNodeOrNull<ColorRect>("ContentRoot/UnitFrame/HealthBar/HealthBackground");
        _healthFill = GetNodeOrNull<ColorRect>("ContentRoot/UnitFrame/HealthBar/HealthFill");
        _healthLabel = GetNodeOrNull<Label>("ContentRoot/UnitFrame/HealthBar/HealthLabel");
        _manaBar = GetNodeOrNull<Control>("ContentRoot/UnitFrame/ManaBar");
        _manaBackground = GetNodeOrNull<ColorRect>("ContentRoot/UnitFrame/ManaBar/ManaBackground");
        _manaFill = GetNodeOrNull<ColorRect>("ContentRoot/UnitFrame/ManaBar/ManaFill");
        _manaLabel = GetNodeOrNull<Label>("ContentRoot/UnitFrame/ManaBar/ManaLabel");
        _xpBar = GetNodeOrNull<Control>("ContentRoot/UnitFrame/XpBar");
        _xpBackground = GetNodeOrNull<ColorRect>("ContentRoot/UnitFrame/XpBar/XpBackground");
        _xpFill = GetNodeOrNull<ColorRect>("ContentRoot/UnitFrame/XpBar/XpFill");
        _targetBracket = GetNodeOrNull<Node2D>("ContentRoot/TargetBracket");
        _leftBracket = GetNodeOrNull<Line2D>("ContentRoot/TargetBracket/LeftBracket");
        _rightBracket = GetNodeOrNull<Line2D>("ContentRoot/TargetBracket/RightBracket");
        GameSettings.ShowActorNamesChanged += OnShowActorNamesChanged;
        ApplyBarSizes();
        RefreshPlacement();
        RefreshName();
        RefreshTextColors();
        RefreshManaVisibility();
        RefreshManaColors();
        RefreshHealthBar();
        RefreshXpVisibility();
        RefreshXpBar();
        SetUnitFrameVisible(false);
        SetTargetBracketVisible(false);
    }

    public override void _ExitTree()
    {
        UnbindPlayerXp();
        GameSettings.ShowActorNamesChanged -= OnShowActorNamesChanged;
    }

    public void Bind(Node2D owner)
    {
        UnbindPlayerXp();
        _owner = owner;
        _playerOwner = owner as Player;
        _manaState = owner?.GetNodeOrNull<ManaState>("ManaState");
        _currentXp = Math.Max(0, _playerOwner?.CurrentExperience ?? 0);
        _requiredXp = Math.Max(1, _playerOwner?.GetRequiredExperienceForCurrentLevel() ?? 1);
        _playerLevel = _playerOwner?.Level ?? 1;
        BindPlayerXp();
        RefreshName();
        RefreshManaVisibility();
        RefreshManaBar();
        RefreshXpVisibility();
        RefreshXpBar();
    }

    public void SetHealth(int current, int max)
    {
        _currentHealth = Math.Max(0, current);
        _maxHealth = Math.Max(1, max);
        RefreshHealthBar();
    }

    public void SetFaction(Faction faction)
    {
        _faction = faction;
        RefreshHealthColors();
        RefreshBracketColor();
    }

    public void SetPoisoned(bool isPoisoned)
    {
        if (_isPoisoned == isPoisoned)
            return;

        _isPoisoned = isPoisoned;
        RefreshHealthColors();
    }

    public void SetTargetBracketVisible(bool visible)
    {
        if (_targetBracket != null)
            _targetBracket.Visible = visible;

        if (visible)
            RefreshBracketColor();
    }

    public void ShowFloatingText(string text, Color color, float riseDistance = 18.0f, float duration = 0.6f, int fontSize = 20)
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_owner))
            return;

        if (Mathf.IsEqualApprox(riseDistance, 18.0f) &&
            Mathf.IsEqualApprox(duration, 0.6f) &&
            fontSize == 20)
        {
            FloatingText.ShowCustom(text, _owner, color);
            return;
        }

        FloatingText.Show(text, _owner, color, riseDistance: riseDistance, duration: duration, fontSize: fontSize);
    }

    public void SetUnitFrameVisible(bool visible)
    {
        if (_unitFrame != null)
            _unitFrame.Visible = visible;

        RefreshName();
    }

    public override void _Process(double delta)
    {
        RefreshPlacement();
        RefreshManaBar();
    }

    private void RefreshPlacement()
    {
        if (_contentRoot == null || _screenLayer == null)
            return;

        var isScreenAnchored = PlacementMode == HudPlacementMode.ScreenAnchored;
        var desiredParent = isScreenAnchored ? (Node)_screenLayer : this;
        if (_contentRoot.GetParent() != desiredParent)
            _contentRoot.Reparent(desiredParent, false);

        _screenLayer.Visible = isScreenAnchored;
        _screenLayer.Layer = ScreenLayer;

        if (isScreenAnchored)
        {
            var viewportRect = GetViewportRect();
            _contentRoot.Position = viewportRect.Position + (viewportRect.Size * ScreenAnchor) + ScreenOffset;
            return;
        }

        _contentRoot.Position = new Vector2(0.0f, VerticalOffset);
    }

    private void ApplyBarSizes()
    {
        ApplyBarSize(_healthBar, _healthBackground, _healthLabel, HealthBarWidth, HealthBarHeight);
        ApplyBarSize(_manaBar, _manaBackground, _manaLabel, ManaBarWidth, ManaBarHeight);
        ApplyBarSize(_xpBar, _xpBackground, null, XpBarWidth, XpBarHeight);
    }

    private void RefreshName()
    {
        if (_nameLabel == null)
            return;

        var resolvedName = !string.IsNullOrWhiteSpace(DisplayName)
            ? DisplayName
            : _owner?.Name.ToString() ?? string.Empty;

        if (_owner is CombatCharacter combatCharacter && !string.IsNullOrWhiteSpace(resolvedName))
            resolvedName = $"[{combatCharacter.Level}] {resolvedName}";

        var shouldShowName = ShowName &&
                             GameSettings.ShowActorNames &&
                             (_unitFrame == null || _unitFrame.Visible) &&
                             !string.IsNullOrWhiteSpace(resolvedName);
        _nameLabel.Visible = shouldShowName;
        if (_nameLabel.Visible)
            _nameLabel.Text = resolvedName;
    }

    private void RefreshHealthBar()
    {
        if (_healthLabel == null)
            return;

        _healthLabel.Text = $"{_currentHealth}/{_maxHealth}";
        SetBarFill(_healthFill, _healthBackground, (float)_currentHealth / _maxHealth);
        RefreshTextColors();
        RefreshHealthColors();
    }

    private void RefreshManaVisibility()
    {
        if (_manaBar == null)
            return;

        _manaBar.Visible = ShowMana && _manaState != null;
    }

    private void RefreshManaBar()
    {
        if (_manaBar == null || _manaLabel == null)
            return;

        RefreshManaVisibility();
        if (!_manaBar.Visible)
            return;

        var currentMana = Math.Max(0, _manaState.Current);
        var maxMana = Math.Max(1, _manaState.Max);
        _manaLabel.Text = $"{currentMana}/{maxMana}";
        SetBarFill(_manaFill, _manaBackground, (float)currentMana / maxMana);
        RefreshTextColors();
        RefreshManaColors();
    }

    private void RefreshHealthColors()
    {
        if (_healthFill == null || _healthBackground == null)
            return;

        if (_isPoisoned)
        {
            _healthFill.Color = PoisonedHealthFillColor;
            _healthBackground.Color = PoisonedHealthBackgroundColor;
            return;
        }

        if (!ShouldUseFactionHealthColors())
        {
            _healthFill.Color = HealthFillColor;
            _healthBackground.Color = HealthBackgroundColor;
            return;
        }

        var factionColor = FactionColors.Resolve(_faction);
        _healthFill.Color = factionColor;
        var backgroundColor = factionColor.Darkened(0.65f);
        backgroundColor.A = 0.85f;
        _healthBackground.Color = backgroundColor;
    }

    private void RefreshManaColors()
    {
        if (_manaFill == null || _manaBackground == null)
            return;

        _manaFill.Color = ManaFillColor;
        _manaBackground.Color = ManaBackgroundColor;
    }

    private void RefreshBracketColor()
    {
        var bracketColor = FactionColors.Resolve(_faction);
        bracketColor.A = 0.8f;

        if (_leftBracket != null)
            _leftBracket.DefaultColor = bracketColor;

        if (_rightBracket != null)
            _rightBracket.DefaultColor = bracketColor;
    }

    private void RefreshTextColors()
    {
        if (_healthLabel != null)
            _healthLabel.AddThemeColorOverride("font_color", HealthTextColor);

        if (_manaLabel != null)
            _manaLabel.AddThemeColorOverride("font_color", ManaTextColor);
    }

    private void RefreshXpVisibility()
    {
        if (_xpBar == null)
            return;

        _xpBar.Visible = _playerOwner != null;
    }

    private void RefreshXpBar()
    {
        if (_xpFill == null || _xpBackground == null)
            return;

        var isMaxLevel = _playerOwner != null && _playerLevel >= Math.Max(1, _playerOwner.MaxLevel);
        var fraction = isMaxLevel ? 1.0f : (float)_currentXp / Math.Max(1, _requiredXp);
        SetBarFill(_xpFill, _xpBackground, fraction);

        if (_xpFill != null)
            _xpFill.Color = XpFillColor;

        if (_xpBackground != null)
            _xpBackground.Color = XpBackgroundColor;
    }

    private void BindPlayerXp()
    {
        if (_playerOwner == null)
            return;

        _playerOwner.Connect(Player.SignalName.ExperienceChanged, new Callable(this, nameof(OnPlayerExperienceChanged)));
    }

    private void UnbindPlayerXp()
    {
        if (_playerOwner == null || !GodotObject.IsInstanceValid(_playerOwner))
            return;

        var callable = new Callable(this, nameof(OnPlayerExperienceChanged));
        if (_playerOwner.IsConnected(Player.SignalName.ExperienceChanged, callable))
            _playerOwner.Disconnect(Player.SignalName.ExperienceChanged, callable);
    }

    private void OnPlayerExperienceChanged(int currentExperience, int requiredExperience, int level)
    {
        _currentXp = Math.Max(0, currentExperience);
        _requiredXp = Math.Max(1, requiredExperience);
        _playerLevel = level;
        RefreshXpBar();
    }

    private bool ShouldUseFactionHealthColors()
    {
        return UseFactionHealthColors &&
               ColorsMatch(HealthFillColor, DefaultHealthFillColor) &&
               ColorsMatch(HealthBackgroundColor, DefaultHealthBackgroundColor);
    }

    private static bool ColorsMatch(Color left, Color right)
    {
        return Mathf.IsEqualApprox(left.R, right.R) &&
               Mathf.IsEqualApprox(left.G, right.G) &&
               Mathf.IsEqualApprox(left.B, right.B) &&
               Mathf.IsEqualApprox(left.A, right.A);
    }

    private static void ApplyBarSize(Control container, ColorRect background, Label label, float width, float height)
    {
        var resolvedWidth = Math.Max(2.0f, width);
        var resolvedHeight = Math.Max(2.0f, height);
        var barSize = new Vector2(resolvedWidth, resolvedHeight);

        if (container != null)
        {
            container.CustomMinimumSize = barSize;
            container.Size = barSize;
        }

        if (background != null)
            background.Size = barSize;

        if (label != null)
            label.Size = barSize;
    }

    private void OnShowActorNamesChanged(bool _)
    {
        RefreshName();
    }

    private static void SetBarFill(ColorRect fill, ColorRect background, float fraction)
    {
        if (fill == null || background == null)
            return;

        var clampedFraction = Mathf.Clamp(fraction, 0.0f, 1.0f);
        var horizontalPadding = fill.Position.X;
        var verticalPadding = fill.Position.Y;
        var innerWidth = Math.Max(0.0f, background.Size.X - (horizontalPadding * 2.0f));
        var innerHeight = Math.Max(0.0f, background.Size.Y - (verticalPadding * 2.0f));
        fill.Size = new Vector2(innerWidth * clampedFraction, innerHeight);
    }
}
