using Godot;

using System;

[GlobalClass]
public partial class CastBar : Control
{
    private const float PushbackDisplayDurationSeconds = 0.6f;

    [Export]
    public NodePath SpellLabelPath { get; set; } = new("BottomLeft/Panel/Margin/VBox/TopRow/SpellLabel");

    [Export]
    public NodePath PushbackLabelPath { get; set; } = new("BottomLeft/Panel/Margin/VBox/TopRow/PushbackLabel");

    [Export]
    public NodePath TimeLabelPath { get; set; } = new("BottomLeft/Panel/Margin/VBox/TopRow/TimeLabel");

    [Export]
    public NodePath ProgressBarPath { get; set; } = new("BottomLeft/Panel/Margin/VBox/ProgressBar");

    private Label _spellLabel;
    private Label _pushbackLabel;
    private Label _timeLabel;
    private ProgressBar _progressBar;
    private float _durationSeconds;
    private float _pushbackDisplayRemaining;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _spellLabel = GetNodeOrNull<Label>(SpellLabelPath);
        _pushbackLabel = GetNodeOrNull<Label>(PushbackLabelPath);
        _timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
        _progressBar = GetNodeOrNull<ProgressBar>(ProgressBarPath);
        HideCast();
    }

    public override void _Process(double delta)
    {
        if (_pushbackDisplayRemaining <= 0.0f)
            return;

        _pushbackDisplayRemaining = Math.Max(0.0f, _pushbackDisplayRemaining - Math.Max(0.0f, (float)delta));
        if (_pushbackDisplayRemaining == 0.0f && _pushbackLabel != null)
            _pushbackLabel.Visible = false;
    }

    public void ShowCast(string label, float durationSeconds)
    {
        _durationSeconds = Math.Max(0.0f, durationSeconds);

        if (_spellLabel != null)
            _spellLabel.Text = string.IsNullOrWhiteSpace(label) ? "Casting" : label;

        if (_progressBar != null)
        {
            _progressBar.MinValue = 0.0;
            _progressBar.MaxValue = Math.Max(0.001f, _durationSeconds);
            _progressBar.Value = 0.0;
        }

        UpdateCast(0.0f);
        Visible = true;
    }

    public void ShowPushback(float seconds)
    {
        if (_pushbackLabel == null)
            return;

        var pushbackSeconds = Math.Max(0.0f, seconds);
        if (pushbackSeconds <= 0.0f)
        {
            _pushbackLabel.Visible = false;
            _pushbackDisplayRemaining = 0.0f;
            return;
        }

        _pushbackLabel.Text = $"-{pushbackSeconds:0.0}s";
        _pushbackLabel.Visible = true;
        _pushbackDisplayRemaining = PushbackDisplayDurationSeconds;
    }

    public void UpdateCast(float elapsedSeconds)
    {
        var clampedElapsed = Mathf.Clamp(elapsedSeconds, 0.0f, Math.Max(0.0f, _durationSeconds));
        var remainingSeconds = Math.Max(0.0f, _durationSeconds - clampedElapsed);

        if (_progressBar != null)
            _progressBar.Value = clampedElapsed;

        if (_timeLabel != null)
            _timeLabel.Text = $"{remainingSeconds:0.0}s";
    }

    public void HideCast()
    {
        Visible = false;
        _durationSeconds = 0.0f;
        _pushbackDisplayRemaining = 0.0f;

        if (_progressBar != null)
            _progressBar.Value = 0.0;

        if (_pushbackLabel != null)
            _pushbackLabel.Visible = false;

        if (_timeLabel != null)
            _timeLabel.Text = "0.0s";
    }
}
