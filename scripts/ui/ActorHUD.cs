using Godot;

using System;

[GlobalClass]
public partial class ActorHUD : Node2D
{
    private static readonly Color DefaultHealthFillColor = new Color(0.45f, 0.95f, 0.45f, 1.0f);
    private static readonly Color DefaultHealthBackgroundColor = new Color(0.16f, 0.36f, 0.16f, 0.85f);

    [Export]
    public bool ShowName { get; set; } = true;

    [Export]
    public bool ShowMana { get; set; } = true;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public float VerticalOffset { get; set; } = -40.0f;

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

    private Control _unitFrame;
    private Label _nameLabel;
    private ColorRect _healthBackground;
    private ColorRect _healthFill;
    private Label _healthLabel;
    private Control _manaBar;
    private ColorRect _manaBackground;
    private ColorRect _manaFill;
    private Label _manaLabel;
    private Node2D _targetBracket;
    private Line2D _leftBracket;
    private Line2D _rightBracket;
    private Node2D _owner;
    private ManaState _manaState;
    private Faction _faction;
    private int _currentHealth;
    private int _maxHealth = 1;

    public override void _Ready()
    {
        _unitFrame = GetNodeOrNull<Control>("UnitFrame");
        _nameLabel = GetNodeOrNull<Label>("UnitFrame/NameLabel");
        _healthBackground = GetNodeOrNull<ColorRect>("UnitFrame/HealthBar/HealthBackground");
        _healthFill = GetNodeOrNull<ColorRect>("UnitFrame/HealthBar/HealthFill");
        _healthLabel = GetNodeOrNull<Label>("UnitFrame/HealthBar/HealthLabel");
        _manaBar = GetNodeOrNull<Control>("UnitFrame/ManaBar");
        _manaBackground = GetNodeOrNull<ColorRect>("UnitFrame/ManaBar/ManaBackground");
        _manaFill = GetNodeOrNull<ColorRect>("UnitFrame/ManaBar/ManaFill");
        _manaLabel = GetNodeOrNull<Label>("UnitFrame/ManaBar/ManaLabel");
        _targetBracket = GetNodeOrNull<Node2D>("TargetBracket");
        _leftBracket = GetNodeOrNull<Line2D>("TargetBracket/LeftBracket");
        _rightBracket = GetNodeOrNull<Line2D>("TargetBracket/RightBracket");
        ActorHudSettings.Changed += OnActorHudSettingsChanged;
        ApplyVerticalOffset();
        RefreshName();
        RefreshTextColors();
        RefreshManaVisibility();
        RefreshManaColors();
        RefreshHealthBar();
        SetUnitFrameVisible(false);
        SetTargetBracketVisible(false);
    }

    public override void _ExitTree()
    {
        ActorHudSettings.Changed -= OnActorHudSettingsChanged;
    }

    public void Bind(Node2D owner)
    {
        _owner = owner;
        _manaState = owner?.GetNodeOrNull<ManaState>("ManaState");
        RefreshName();
        RefreshManaVisibility();
        RefreshManaBar();
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

        FloatingNumberHelper.ShowFloatingNumber(_owner, text, color, riseDistance, duration, fontSize);
    }

    public void SetUnitFrameVisible(bool visible)
    {
        if (_unitFrame != null)
            _unitFrame.Visible = visible;

        RefreshName();
    }

    public override void _Process(double delta)
    {
        RefreshManaBar();
    }

    private void ApplyVerticalOffset()
    {
        Position = new Vector2(0.0f, VerticalOffset);
    }

    private void RefreshName()
    {
        if (_nameLabel == null)
            return;

        var resolvedName = !string.IsNullOrWhiteSpace(DisplayName)
            ? DisplayName
            : _owner?.Name.ToString() ?? string.Empty;

        var shouldShowName = ShowName &&
                             ActorHudSettings.ShowNames &&
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

    private void OnActorHudSettingsChanged(bool _)
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
