using Godot;

using System;

[GlobalClass]
public partial class ActorHUD : Node2D
{
    private Label _healthLabel;
    private Node2D _owner;

    public override void _Ready()
    {
        _healthLabel = GetNodeOrNull<Label>("HealthLabel");
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
        if (_healthLabel == null)
            return;

        _healthLabel.AddThemeColorOverride("font_color", FactionColors.Resolve(faction));
    }

    public void ShowFloatingText(string text, Color color, float riseDistance = 18.0f, float duration = 0.6f, int fontSize = 20)
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_owner))
            return;

        FloatingNumberHelper.ShowFloatingNumber(_owner, text, color, riseDistance, duration, fontSize);
    }
}
