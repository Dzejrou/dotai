using Godot;

[GlobalClass]
public partial class PauseMenu : Control
{
    [Signal]
    public delegate void ResumeRequestedEventHandler();

    [Signal]
    public delegate void DebugRequestedEventHandler();

    [Export]
    public NodePath ResumeButtonPath { get; set; } = new NodePath("Center/Panel/VBox/ResumeButton");

    [Export]
    public NodePath DebugButtonPath { get; set; } = new NodePath("Center/Panel/VBox/DebugButton");

    private Button _resumeButton;
    private Button _debugButton;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _resumeButton = GetNodeOrNull<Button>(ResumeButtonPath);
        _debugButton = GetNodeOrNull<Button>(DebugButtonPath);

        if (_resumeButton != null)
            _resumeButton.Pressed += OnResumePressed;

        if (_debugButton != null)
            _debugButton.Pressed += OnDebugPressed;
    }

    public override void _ExitTree()
    {
        if (_resumeButton != null)
            _resumeButton.Pressed -= OnResumePressed;

        if (_debugButton != null)
            _debugButton.Pressed -= OnDebugPressed;
    }

    private void OnResumePressed()
    {
        EmitSignal(SignalName.ResumeRequested);
    }

    private void OnDebugPressed()
    {
        EmitSignal(SignalName.DebugRequested);
    }
}
