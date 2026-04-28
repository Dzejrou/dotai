using Godot;

using System;

[GlobalClass]
public partial class CountdownHUD : Control
{
    [Export]
    public NodePath TitleLabelPath { get; set; } = new("TopCenter/Panel/Margin/VBox/TitleLabel");

    [Export]
    public NodePath CountdownLabelPath { get; set; } = new("TopCenter/Panel/Margin/VBox/CountdownLabel");

    private Label _titleLabel;
    private Label _countdownLabel;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
        _countdownLabel = GetNodeOrNull<Label>(CountdownLabelPath);
        HideCountdown();
    }

    public void ShowCountdown(string title, float timeRemainingSeconds)
    {
        if (_titleLabel != null)
            _titleLabel.Text = string.IsNullOrWhiteSpace(title) ? "Countdown" : title;

        if (_countdownLabel != null)
            _countdownLabel.Text = FormatCountdown(timeRemainingSeconds);

        Visible = true;
    }

    public void HideCountdown()
    {
        Visible = false;
    }

    private static string FormatCountdown(float timeRemainingSeconds)
    {
        var remainingSeconds = Math.Max(0, Mathf.CeilToInt(Math.Max(0.0f, timeRemainingSeconds)));
        return $"{remainingSeconds / 60:00}:{remainingSeconds % 60:00}";
    }
}
