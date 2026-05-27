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

    [Signal]
    public delegate void LoadRequestedEventHandler();

    [Export]
    public NodePath MainViewPath { get; set; } = new NodePath("Center/Panel/Views/MainView");

    [Export]
    public NodePath SettingsViewPath { get; set; } = new NodePath("Center/Panel/Views/SettingsView");

    [Export]
    public NodePath ResumeButtonPath { get; set; } = new NodePath("Center/Panel/Views/MainView/ResumeButton");

    [Export]
    public NodePath SaveButtonPath { get; set; } = new NodePath("Center/Panel/Views/MainView/SaveButton");

    [Export]
    public NodePath LoadButtonPath { get; set; } = new NodePath("Center/Panel/Views/MainView/LoadButton");

    [Export]
    public NodePath SettingsButtonPath { get; set; } = new NodePath("Center/Panel/Views/MainView/SettingsButton");

    [Export]
    public NodePath DebugButtonPath { get; set; } = new NodePath("Center/Panel/Views/MainView/DebugButton");

    [Export]
    public NodePath BackButtonPath { get; set; } = new NodePath("Center/Panel/Views/SettingsView/BackButton");

    [Export]
    public NodePath ShowActorNamesTogglePath { get; set; } = new NodePath("Center/Panel/Views/SettingsView/ShowActorNamesToggle");

    [Export]
    public NodePath ShowFloatingTextTogglePath { get; set; } = new NodePath("Center/Panel/Views/SettingsView/ShowFloatingTextToggle");

    [Export]
    public NodePath ShowCombatLogDebugTogglePath { get; set; } = new NodePath("Center/Panel/Views/SettingsView/ShowCombatLogDebugToggle");

    private readonly GameConfigStore _gameConfigStore = new();
    private Control _mainView;
    private Control _settingsView;
    private Button _resumeButton;
    private Button _saveButton;
    private Button _loadButton;
    private Button _settingsButton;
    private Button _debugButton;
    private Button _backButton;
    private BaseButton _showActorNamesToggle;
    private BaseButton _showFloatingTextToggle;
    private BaseButton _showCombatLogDebugToggle;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _gameConfigStore.LoadGameSettings();

        _mainView = GetNodeOrNull<Control>(MainViewPath);
        _settingsView = GetNodeOrNull<Control>(SettingsViewPath);
        _resumeButton = GetNodeOrNull<Button>(ResumeButtonPath);
        _saveButton = GetNodeOrNull<Button>(SaveButtonPath);
        _loadButton = GetNodeOrNull<Button>(LoadButtonPath);
        _settingsButton = GetNodeOrNull<Button>(SettingsButtonPath);
        _debugButton = GetNodeOrNull<Button>(DebugButtonPath);
        _backButton = GetNodeOrNull<Button>(BackButtonPath);
        _showActorNamesToggle = GetNodeOrNull<BaseButton>(ShowActorNamesTogglePath);
        _showFloatingTextToggle = GetNodeOrNull<BaseButton>(ShowFloatingTextTogglePath);
        _showCombatLogDebugToggle = GetNodeOrNull<BaseButton>(ShowCombatLogDebugTogglePath);

        if (_resumeButton != null)
            _resumeButton.Pressed += OnResumePressed;

        if (_saveButton != null)
            _saveButton.Pressed += OnSavePressed;

        if (_loadButton != null)
            _loadButton.Pressed += OnLoadPressed;

        if (_settingsButton != null)
            _settingsButton.Pressed += OnSettingsPressed;

        if (_debugButton != null)
            _debugButton.Pressed += OnDebugPressed;

        if (_backButton != null)
            _backButton.Pressed += OnBackPressed;

        if (_showActorNamesToggle != null)
        {
            _showActorNamesToggle.ButtonPressed = GameSettings.ShowActorNames;
            _showActorNamesToggle.Toggled += OnShowActorNamesToggled;
        }

        if (_showFloatingTextToggle != null)
        {
            _showFloatingTextToggle.ButtonPressed = GameSettings.ShowFloatingText;
            _showFloatingTextToggle.Toggled += OnShowFloatingTextToggled;
        }

        if (_showCombatLogDebugToggle != null)
        {
            _showCombatLogDebugToggle.ButtonPressed = GameSettings.ShowCombatLogDebugMessages;
            _showCombatLogDebugToggle.Toggled += OnShowCombatLogDebugToggled;
        }

        ShowMainView();
    }

    public override void _ExitTree()
    {
        if (_resumeButton != null)
            _resumeButton.Pressed -= OnResumePressed;

        if (_saveButton != null)
            _saveButton.Pressed -= OnSavePressed;

        if (_loadButton != null)
            _loadButton.Pressed -= OnLoadPressed;

        if (_settingsButton != null)
            _settingsButton.Pressed -= OnSettingsPressed;

        if (_debugButton != null)
            _debugButton.Pressed -= OnDebugPressed;

        if (_backButton != null)
            _backButton.Pressed -= OnBackPressed;

        if (_showActorNamesToggle != null)
            _showActorNamesToggle.Toggled -= OnShowActorNamesToggled;

        if (_showFloatingTextToggle != null)
            _showFloatingTextToggle.Toggled -= OnShowFloatingTextToggled;

        if (_showCombatLogDebugToggle != null)
            _showCombatLogDebugToggle.Toggled -= OnShowCombatLogDebugToggled;
    }

    private void OnResumePressed()
    {
        EmitSignal(SignalName.ResumeRequested);
    }

    private void OnSavePressed()
    {
        EmitSignal(SignalName.SaveRequested);
    }

    private void OnLoadPressed()
    {
        EmitSignal(SignalName.LoadRequested);
    }

    private void OnDebugPressed()
    {
        EmitSignal(SignalName.DebugRequested);
    }

    private void OnSettingsPressed()
    {
        ShowSettingsView();
    }

    private void OnBackPressed()
    {
        ShowMainView();
    }

    private void OnShowActorNamesToggled(bool pressed)
    {
        GameSettings.SetShowActorNames(pressed);
        PersistSettings();
    }

    private void OnShowFloatingTextToggled(bool pressed)
    {
        GameSettings.SetShowFloatingText(pressed);
        PersistSettings();
    }

    private void OnShowCombatLogDebugToggled(bool pressed)
    {
        GameSettings.SetShowCombatLogDebugMessages(pressed);
        PersistSettings();
    }

    private void PersistSettings()
    {
        if (!_gameConfigStore.TrySaveGameSettings(out var message))
            GD.PushWarning(message);
    }

    private void ShowMainView()
    {
        if (_mainView != null)
            _mainView.Visible = true;

        if (_settingsView != null)
            _settingsView.Visible = false;
    }

    private void ShowSettingsView()
    {
        if (_mainView != null)
            _mainView.Visible = false;

        if (_settingsView != null)
            _settingsView.Visible = true;
    }
}
