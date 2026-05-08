using Godot;

using System;

[GlobalClass]
public partial class CastBar : Control
{
    private const float PushbackDisplayDurationSeconds = 0.6f;
    private const float CanceledFadeDurationSeconds = 2.0f;
    private static readonly Color CanceledLabelColor = new(1.0f, 0.32f, 0.32f, 1.0f);

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
    private float _fadeRemainingSeconds;
    private bool _isChanneling;

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
        var deltaSeconds = Math.Max(0.0f, (float)delta);

        if (_pushbackDisplayRemaining > 0.0f)
            _pushbackDisplayRemaining = Math.Max(0.0f, _pushbackDisplayRemaining - deltaSeconds);

        if (_pushbackDisplayRemaining == 0.0f && _pushbackLabel != null)
            _pushbackLabel.Visible = false;

        if (_fadeRemainingSeconds <= 0.0f)
            return;

        _fadeRemainingSeconds = Math.Max(0.0f, _fadeRemainingSeconds - deltaSeconds);
        var alpha = _fadeRemainingSeconds / CanceledFadeDurationSeconds;
        Modulate = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp(alpha, 0.0f, 1.0f));
        if (_fadeRemainingSeconds == 0.0f)
            HideCast();
    }

    public void ShowCast(string label, float durationSeconds, bool isChanneling = false)
    {
        ResetDisplayState();
        _durationSeconds = Math.Max(0.0f, durationSeconds);
        _isChanneling = isChanneling;

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

    public void ShowCanceled(string label = "CANCELED")
    {
        ResetDisplayState();
        Visible = true;
        _fadeRemainingSeconds = CanceledFadeDurationSeconds;

        if (_spellLabel != null)
        {
            _spellLabel.Text = string.IsNullOrWhiteSpace(label) ? "CANCELED" : label;
            _spellLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _spellLabel.AddThemeColorOverride("font_color", CanceledLabelColor);
        }

        if (_timeLabel != null)
            _timeLabel.Visible = false;

        if (_pushbackLabel != null)
            _pushbackLabel.Visible = false;

        _pushbackDisplayRemaining = 0.0f;
        Modulate = Colors.White;
    }

    public void ShowPushback(float seconds)
    {
        if (_pushbackLabel == null || _fadeRemainingSeconds > 0.0f)
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
        var progressValue = _isChanneling ? remainingSeconds : clampedElapsed;

        if (_progressBar != null)
            _progressBar.Value = progressValue;

        if (_timeLabel != null)
            _timeLabel.Text = $"{remainingSeconds:0.0}s";
    }

    public void HideCast()
    {
        Visible = false;
        ResetDisplayState();
        _durationSeconds = 0.0f;
        _isChanneling = false;

        if (_progressBar != null)
            _progressBar.Value = 0.0;

        if (_timeLabel != null)
            _timeLabel.Text = "0.0s";
    }

    private void ResetDisplayState()
    {
        _fadeRemainingSeconds = 0.0f;
        _pushbackDisplayRemaining = 0.0f;
        Modulate = Colors.White;

        if (_spellLabel != null)
        {
            _spellLabel.HorizontalAlignment = HorizontalAlignment.Left;
            _spellLabel.RemoveThemeColorOverride("font_color");
        }

        if (_pushbackLabel != null)
            _pushbackLabel.Visible = false;

        if (_timeLabel != null)
            _timeLabel.Visible = true;
    }
}
