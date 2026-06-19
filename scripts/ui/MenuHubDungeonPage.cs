using Godot;

using System;
using System.Globalization;

// Dungeon HUB page: the launch surface for dungeon runs. With no active run it presents the
// run configuration (ordinary-room count, starting level, an editable 64-bit seed) and a Start
// button gated by entrance authorization or the Dungeon Anywhere setting. During a run it shows
// read-only status (seed, room progress, level) and a Resume button.
//
// Ownership stays narrow: this page only validates/presents inputs and status. Starting goes
// through a bound request callback (page -> MenuHub/Main -> World); run state is read from the
// Dungeon node and refreshed from an explicit signal rather than polled per frame.
[GlobalClass]
public partial class MenuHubDungeonPage : Control
{
    private const string ReadyStatusText = "Ready to start.";
    private const string EntranceRequiredStatusText =
        "Interact with the dungeon entrance to start, or enable Dungeon Anywhere on the Debug page.";

    [Export]
    public NodePath ConfigViewPath { get; set; } = new("Margin/VBox/ConfigView");

    [Export]
    public NodePath RoomsSpinBoxPath { get; set; } = new("Margin/VBox/ConfigView/RoomsRow/RoomsSpinBox");

    [Export]
    public NodePath StartingLevelSpinBoxPath { get; set; } = new("Margin/VBox/ConfigView/StartingLevelRow/StartingLevelSpinBox");

    [Export]
    public NodePath SeedLineEditPath { get; set; } = new("Margin/VBox/ConfigView/SeedRow/SeedLineEdit");

    [Export]
    public NodePath RandomizeSeedButtonPath { get; set; } = new("Margin/VBox/ConfigView/SeedButtonsRow/RandomizeSeedButton");

    [Export]
    public NodePath StartButtonPath { get; set; } = new("Margin/VBox/ConfigView/StartButton");

    [Export]
    public NodePath StatusLabelPath { get; set; } = new("Margin/VBox/ConfigView/StatusLabel");

    [Export]
    public NodePath ActiveViewPath { get; set; } = new("Margin/VBox/ActiveView");

    [Export]
    public NodePath ActiveSeedLabelPath { get; set; } = new("Margin/VBox/ActiveView/ActiveSeedLabel");

    [Export]
    public NodePath ProgressLabelPath { get; set; } = new("Margin/VBox/ActiveView/ProgressLabel");

    [Export]
    public NodePath ActiveLevelLabelPath { get; set; } = new("Margin/VBox/ActiveView/ActiveLevelLabel");

    [Export]
    public NodePath ResumeButtonPath { get; set; } = new("Margin/VBox/ActiveView/ResumeButton");

    private Control _configView;
    private SpinBox _roomsSpinBox;
    private SpinBox _startingLevelSpinBox;
    private LineEdit _seedLineEdit;
    private Button _randomizeSeedButton;
    private Button _startButton;
    private Label _statusLabel;
    private Control _activeView;
    private Label _activeSeedLabel;
    private Label _progressLabel;
    private Label _activeLevelLabel;
    private Button _resumeButton;

    private readonly RandomNumberGenerator _seedRng = new();
    private World _world;
    private Dungeon _boundDungeon;
    private Action _resume;
    // Returns null/empty on success, or an actionable error to display while the HUB stays open.
    private Func<ulong, int, int, string> _startDungeon;
    private bool _entranceAuthorized;
    private bool _rulesDefaultsInitialized;
    private bool _dungeonAnywhereSubscribed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _configView = GetNodeOrNull<Control>(ConfigViewPath);
        _roomsSpinBox = GetNodeOrNull<SpinBox>(RoomsSpinBoxPath);
        _startingLevelSpinBox = GetNodeOrNull<SpinBox>(StartingLevelSpinBoxPath);
        _seedLineEdit = GetNodeOrNull<LineEdit>(SeedLineEditPath);
        _randomizeSeedButton = GetNodeOrNull<Button>(RandomizeSeedButtonPath);
        _startButton = GetNodeOrNull<Button>(StartButtonPath);
        _statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
        _activeView = GetNodeOrNull<Control>(ActiveViewPath);
        _activeSeedLabel = GetNodeOrNull<Label>(ActiveSeedLabelPath);
        _progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
        _activeLevelLabel = GetNodeOrNull<Label>(ActiveLevelLabelPath);
        _resumeButton = GetNodeOrNull<Button>(ResumeButtonPath);

        ConfigureRoomsSpinBox();
        ConfigureStartingLevelSpinBox();

        if (_seedLineEdit != null)
        {
            _seedRng.Randomize();
            _seedLineEdit.Text = NextRandomSeed().ToString(CultureInfo.InvariantCulture);
            _seedLineEdit.TextChanged += OnSeedTextChanged;
        }

        if (_randomizeSeedButton != null)
            _randomizeSeedButton.Pressed += OnRandomizeSeedPressed;

        if (_startButton != null)
            _startButton.Pressed += OnStartPressed;

        if (_resumeButton != null)
            _resumeButton.Pressed += OnResumePressed;

        GameSettings.DungeonAnywhereChanged += OnDungeonAnywhereChanged;
        _dungeonAnywhereSubscribed = true;

        Refresh();
    }

    public override void _ExitTree()
    {
        if (_seedLineEdit != null)
            _seedLineEdit.TextChanged -= OnSeedTextChanged;

        if (_randomizeSeedButton != null)
            _randomizeSeedButton.Pressed -= OnRandomizeSeedPressed;

        if (_startButton != null)
            _startButton.Pressed -= OnStartPressed;

        if (_resumeButton != null)
            _resumeButton.Pressed -= OnResumePressed;

        if (_dungeonAnywhereSubscribed)
        {
            GameSettings.DungeonAnywhereChanged -= OnDungeonAnywhereChanged;
            _dungeonAnywhereSubscribed = false;
        }

        DisconnectDungeonSignal();
    }

    public void Bind(World world, Action resume, Func<ulong, int, int, string> startDungeon)
    {
        DisconnectDungeonSignal();

        _world = world;
        _resume = resume;
        _startDungeon = startDungeon;

        ConnectDungeonSignal();
        Refresh();
    }

    // Pushed by MenuHub: entrance authorization is granted by the entrance interaction, survives
    // page navigation within the open HUB session, and is cleared on close or consumed on start.
    public void SetEntranceAuthorized(bool authorized)
    {
        _entranceAuthorized = authorized;
        Refresh();
    }

    public void OnPageEntered()
    {
        Refresh();
    }

    private void Refresh()
    {
        EnsureRulesDefaults();

        var active = _world != null && GodotObject.IsInstanceValid(_world) && _world.HasActiveDungeonRun;

        if (_configView != null)
            _configView.Visible = !active;

        if (_activeView != null)
            _activeView.Visible = active;

        if (active)
            RefreshActiveView();
        else
            RefreshConfigView();
    }

    private void RefreshConfigView()
    {
        var seedValid = TryParseSeed(out _, out var seedError);
        var hasAccess = HasStartAccess();
        var canStart = seedValid && hasAccess;

        if (_startButton != null)
            _startButton.Disabled = !canStart;

        string status;
        if (!seedValid)
            status = seedError;
        else if (!hasAccess)
            status = EntranceRequiredStatusText;
        else
            status = ReadyStatusText;

        SetStatus(status, isError: !canStart);
    }

    private void RefreshActiveView()
    {
        var dungeon = ResolveDungeon();
        if (dungeon == null || !dungeon.HasActiveRun)
            return;

        if (_activeSeedLabel != null)
            _activeSeedLabel.Text = $"Seed: {dungeon.RunSeed.ToString(CultureInfo.InvariantCulture)}";

        var node = dungeon.ActiveNode;
        var total = dungeon.ActivePlan?.Length ?? 0;
        var roomNumber = node != null ? node.Index + 1 : 0;

        if (_progressLabel != null)
            _progressLabel.Text = $"Room {roomNumber} / {total}";

        if (_activeLevelLabel != null)
            _activeLevelLabel.Text = $"Level: {(node?.Level ?? 0)}";
    }

    private void OnStartPressed()
    {
        if (!TryParseSeed(out var seed, out var seedError))
        {
            SetStatus(seedError, isError: true);
            return;
        }

        if (!HasStartAccess())
        {
            SetStatus(EntranceRequiredStatusText, isError: true);
            return;
        }

        if (_startDungeon == null)
        {
            SetStatus("Dungeon launch is unavailable.", isError: true);
            return;
        }

        var roomCount = ReadSpinBoxInt(_roomsSpinBox, 0);
        var startingLevel = ReadSpinBoxInt(_startingLevelSpinBox, 1);

        var error = _startDungeon.Invoke(seed, roomCount, startingLevel);
        if (!string.IsNullOrEmpty(error))
        {
            // Failed launch: keep the HUB open and surface the actionable error. Authorization
            // and the return origin are untouched because nothing started.
            SetStatus(error, isError: true);
            return;
        }

        // Success: Main consumes authorization and closes the HUB. Refresh so a later reopen
        // (e.g. Esc during the run) shows the active-run view.
        Refresh();
    }

    private void OnResumePressed()
    {
        _resume?.Invoke();
    }

    private void OnRandomizeSeedPressed()
    {
        if (_seedLineEdit != null)
            _seedLineEdit.Text = NextRandomSeed().ToString(CultureInfo.InvariantCulture);

        RefreshConfigView();
    }

    private void OnSeedTextChanged(string _)
    {
        RefreshConfigView();
    }

    private void OnDungeonAnywhereChanged(bool _)
    {
        // Start availability must update immediately when the setting toggles on the Debug page.
        Refresh();
    }

    private void OnDungeonRunStateChanged()
    {
        Refresh();
    }

    private bool HasStartAccess()
    {
        return _entranceAuthorized || GameSettings.DungeonAnywhere;
    }

    private bool TryParseSeed(out ulong seed, out string error)
    {
        seed = 0;
        error = null;

        var text = _seedLineEdit?.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            error = "Enter a seed between 0 and 18446744073709551615.";
            return false;
        }

        if (!ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
        {
            error = "Seed must be a whole number from 0 to 18446744073709551615.";
            return false;
        }

        return true;
    }

    private void EnsureRulesDefaults()
    {
        if (_rulesDefaultsInitialized)
            return;

        var rules = ResolveDungeon()?.GenerationRules;
        if (rules == null)
            return;

        if (_roomsSpinBox != null)
            _roomsSpinBox.Value = rules.OrdinaryRoomCount;

        if (_startingLevelSpinBox != null)
            _startingLevelSpinBox.Value = rules.StartingRoomLevel;

        _rulesDefaultsInitialized = true;
    }

    private void ConfigureRoomsSpinBox()
    {
        if (_roomsSpinBox == null)
            return;

        _roomsSpinBox.MinValue = 0;
        _roomsSpinBox.MaxValue = 100;
        _roomsSpinBox.Step = 1;
        _roomsSpinBox.Rounded = true;
    }

    private void ConfigureStartingLevelSpinBox()
    {
        if (_startingLevelSpinBox == null)
            return;

        _startingLevelSpinBox.MinValue = 1;
        _startingLevelSpinBox.MaxValue = 100;
        _startingLevelSpinBox.Step = 1;
        _startingLevelSpinBox.Rounded = true;
    }

    private void SetStatus(string text, bool isError)
    {
        if (_statusLabel == null)
            return;

        _statusLabel.Text = text ?? string.Empty;
        _statusLabel.Modulate = isError ? new Color(1.0f, 0.6f, 0.6f) : Colors.White;
    }

    private ulong NextRandomSeed()
    {
        var high = (ulong)_seedRng.Randi();
        var low = (ulong)_seedRng.Randi();
        return (high << 32) | low;
    }

    private Dungeon ResolveDungeon()
    {
        return _world != null && GodotObject.IsInstanceValid(_world) ? _world.Dungeon : null;
    }

    private void ConnectDungeonSignal()
    {
        var dungeon = ResolveDungeon();
        if (dungeon == null)
            return;

        _boundDungeon = dungeon;
        _boundDungeon.RunStateChanged += OnDungeonRunStateChanged;
    }

    private void DisconnectDungeonSignal()
    {
        if (_boundDungeon == null)
            return;

        if (GodotObject.IsInstanceValid(_boundDungeon))
            _boundDungeon.RunStateChanged -= OnDungeonRunStateChanged;

        _boundDungeon = null;
    }

    private static int ReadSpinBoxInt(SpinBox spinBox, int fallback)
    {
        return spinBox != null ? (int)Math.Round(spinBox.Value) : fallback;
    }
}
