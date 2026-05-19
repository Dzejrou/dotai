using Godot;

[GlobalClass]
public partial class PauseMenu : Control
{
    [Signal]
    public delegate void ResumeRequestedEventHandler();

    [Signal]
    public delegate void DebugRequestedEventHandler();

    [Signal]
    public delegate void SaveRequestedEventHandler();

    [Export]
    public NodePath ResumeButtonPath { get; set; } = new NodePath("Center/Panel/VBox/ResumeButton");

    [Export]
    public NodePath SaveButtonPath { get; set; } = new NodePath("Center/Panel/VBox/SaveButton");

    [Export]
    public NodePath DebugButtonPath { get; set; } = new NodePath("Center/Panel/VBox/DebugButton");

    [Export]
    public NodePath ShowNamesTogglePath { get; set; } = new NodePath("Center/Panel/VBox/Options/ShowNamesToggle");

    private Button _resumeButton;
    private Button _saveButton;
    private Button _debugButton;
    private BaseButton _showNamesToggle;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _resumeButton = GetNodeOrNull<Button>(ResumeButtonPath);
        _saveButton = GetNodeOrNull<Button>(SaveButtonPath);
        _debugButton = GetNodeOrNull<Button>(DebugButtonPath);
        _showNamesToggle = GetNodeOrNull<BaseButton>(ShowNamesTogglePath);

        if (_resumeButton != null)
            _resumeButton.Pressed += OnResumePressed;

        if (_saveButton != null)
            _saveButton.Pressed += OnSavePressed;

        if (_debugButton != null)
            _debugButton.Pressed += OnDebugPressed;

        if (_showNamesToggle != null)
        {
            _showNamesToggle.ButtonPressed = ActorHudSettings.ShowNames;
            _showNamesToggle.Toggled += OnShowNamesToggled;
        }
    }

    public override void _ExitTree()
    {
        if (_resumeButton != null)
            _resumeButton.Pressed -= OnResumePressed;

        if (_saveButton != null)
            _saveButton.Pressed -= OnSavePressed;

        if (_debugButton != null)
            _debugButton.Pressed -= OnDebugPressed;

        if (_showNamesToggle != null)
            _showNamesToggle.Toggled -= OnShowNamesToggled;
    }

    private void OnResumePressed()
    {
        EmitSignal(SignalName.ResumeRequested);
    }

    private void OnSavePressed()
    {
        EmitSignal(SignalName.SaveRequested);
    }

    private void OnDebugPressed()
    {
        EmitSignal(SignalName.DebugRequested);
    }

    private void OnShowNamesToggled(bool pressed)
    {
        ActorHudSettings.SetShowNames(pressed);
    }
}
