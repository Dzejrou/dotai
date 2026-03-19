using Godot;

using System;

[GlobalClass]
public partial class ActorHUD : Node2D
{
    private Label _healthLabel;
    private Node2D _targetBracket;
    private Line2D _leftBracket;
    private Line2D _rightBracket;
    private Node2D _owner;
    private Faction _faction;

    public override void _Ready()
    {
        _healthLabel = GetNodeOrNull<Label>("HealthLabel");
        _targetBracket = GetNodeOrNull<Node2D>("TargetBracket");
        _leftBracket = GetNodeOrNull<Line2D>("TargetBracket/LeftBracket");
        _rightBracket = GetNodeOrNull<Line2D>("TargetBracket/RightBracket");
        SetTargetBracketVisible(false);
    }

    public void Bind(Node2D owner)
    {
        _owner = owner;
    }

    public void SetHealth(int current, int max)
    {
        if (_healthLabel == null)
            return;

        _healthLabel.Text = $"{Math.Max(0, current)}/{Math.Max(1, max)}";
    }

    public void SetFaction(Faction faction)
    {
        _faction = faction;

        if (_healthLabel != null)
            _healthLabel.AddThemeColorOverride("font_color", FactionColors.Resolve(faction));

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

    private void RefreshBracketColor()
    {
        var bracketColor = FactionColors.Resolve(_faction);
        bracketColor.A = 0.8f;

        if (_leftBracket != null)
            _leftBracket.DefaultColor = bracketColor;

        if (_rightBracket != null)
            _rightBracket.DefaultColor = bracketColor;
    }
}
