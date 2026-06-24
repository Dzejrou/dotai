using Godot;

using System;
using System.Collections.Generic;
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
    // Raised when the nested History view opens (true) or closes (false). The HUB uses it to hide
    // its top navigation row and lock page navigation while History is shown.
    [Signal]
    public delegate void NestedViewChangedEventHandler(bool open);

    // Outcome row/detail colors. Failed runs are shown in red so they read distinctly from a green
    // Completed run and an amber Gave Up run in history and the end-of-run summary.
    private static readonly Color CompletedOutcomeColor = new(0.40f, 0.85f, 0.45f);
    private static readonly Color GaveUpOutcomeColor = new(0.95f, 0.65f, 0.25f);
    private static readonly Color FailedOutcomeColor = new(0.92f, 0.30f, 0.30f);

    // Shown for a record's score fields when it has no score data (a legacy run finalized before
    // scoring existed), so old entries read clearly instead of pretending their score was 0.
    private const string LegacyScoreFallback = "—";

    private const string ReadyStatusText = "Ready to start.";
    private const string EntranceRequiredStatusText =
        "Interact with the dungeon entrance to start, or enable Dungeon Anywhere on the Debug page.";

    // Persistent DP balance label, a VBox sibling above the swapped views so it stays visible in the
    // run-configuration, active-run and history views alike.
    [Export]
    public NodePath DpLabelPath { get; set; } = new("Margin/VBox/DpLabel");

    [Export]
    public NodePath ConfigViewPath { get; set; } = new("Margin/VBox/ConfigView");

    [Export]
    public NodePath RoomsSpinBoxPath { get; set; } = new("Margin/VBox/ConfigView/RoomsRow/RoomsSpinBox");

    // Container the difficulty rows (header, the five option rows, and the live summary) are built
    // into programmatically from the data-driven difficulty rules.
    [Export]
    public NodePath DifficultyContainerPath { get; set; } = new("Margin/VBox/ConfigView/DifficultyContainer");

    [Export]
    public NodePath SeedLineEditPath { get; set; } = new("Margin/VBox/ConfigView/SeedRow/SeedLineEdit");

    [Export]
    public NodePath RandomizeSeedButtonPath { get; set; } = new("Margin/VBox/ConfigView/SeedButtonsRow/RandomizeSeedButton");

    [Export]
    public NodePath HardcoreTogglePath { get; set; } = new("Margin/VBox/ConfigView/HardcoreToggle");

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
    public NodePath StatsLabelPath { get; set; } = new("Margin/VBox/ActiveView/StatsLabel");

    [Export]
    public NodePath ResumeButtonPath { get; set; } = new("Margin/VBox/ActiveView/ResumeButton");

    [Export]
    public NodePath GiveUpButtonPath { get; set; } = new("Margin/VBox/ActiveView/GiveUpButton");

    [Export]
    public NodePath ActiveStatusLabelPath { get; set; } = new("Margin/VBox/ActiveView/ActiveStatusLabel");

    [Export]
    public NodePath ShopButtonPath { get; set; } = new("Margin/VBox/ShopButton");

    [Export]
    public NodePath HistoryButtonPath { get; set; } = new("Margin/VBox/HistoryButton");

    [Export]
    public NodePath HistoryViewPath { get; set; } = new("Margin/VBox/HistoryView");

    [Export]
    public NodePath HistoryBackButtonPath { get; set; } = new("Margin/VBox/HistoryView/LeftColumn/BackButton");

    [Export]
    public NodePath HistoryListPath { get; set; } = new("Margin/VBox/HistoryView/LeftColumn/RunList");

    [Export]
    public NodePath HistoryEmptyLabelPath { get; set; } = new("Margin/VBox/HistoryView/LeftColumn/EmptyLabel");

    [Export]
    public NodePath HistoryOutcomeLabelPath { get; set; } = new("Margin/VBox/HistoryView/RightColumn/OutcomeLabel");

    [Export]
    public NodePath HistoryDetailsLabelPath { get; set; } = new("Margin/VBox/HistoryView/RightColumn/DetailsLabel");

    [Export]
    public NodePath SummaryViewPath { get; set; } = new("Margin/VBox/SummaryView");

    [Export]
    public NodePath SummaryTitleLabelPath { get; set; } = new("Margin/VBox/SummaryView/TitleLabel");

    [Export]
    public NodePath SummaryOutcomeLabelPath { get; set; } = new("Margin/VBox/SummaryView/OutcomeLabel");

    [Export]
    public NodePath SummaryDetailsLabelPath { get; set; } = new("Margin/VBox/SummaryView/DetailsLabel");

    [Export]
    public NodePath SummaryCloseButtonPath { get; set; } = new("Margin/VBox/SummaryView/CloseButton");

    [Export]
    public NodePath DeathViewPath { get; set; } = new("Margin/VBox/DeathView");

    [Export]
    public NodePath DeathStatusLabelPath { get; set; } = new("Margin/VBox/DeathView/StatusLabel");

    [Export]
    public NodePath DeathContinueButtonPath { get; set; } = new("Margin/VBox/DeathView/ContinueButton");

    [Export]
    public NodePath DeathGiveUpButtonPath { get; set; } = new("Margin/VBox/DeathView/GiveUpButton");

    private Label _dpLabel;
    private Control _configView;
    private SpinBox _roomsSpinBox;
    private Container _difficultyContainer;
    private Label _difficultySummaryLabel;
    private LineEdit _seedLineEdit;
    private Button _randomizeSeedButton;
    private CheckBox _hardcoreToggle;
    private Button _startButton;
    private Label _statusLabel;
    private Control _activeView;
    private Label _activeSeedLabel;
    private Label _progressLabel;
    private Label _activeLevelLabel;
    private Label _statsLabel;
    private Button _resumeButton;
    private Button _giveUpButton;
    private Label _activeStatusLabel;
    private Button _historyButton;
    private Control _historyView;
    private Button _historyBackButton;
    private ItemList _historyList;
    private Label _historyEmptyLabel;
    private Label _historyOutcomeLabel;
    private Label _historyDetailsLabel;
    private Control _summaryView;
    private Label _summaryTitleLabel;
    private Label _summaryOutcomeLabel;
    private Label _summaryDetailsLabel;
    private Button _summaryCloseButton;
    private Control _deathView;
    private Label _deathStatusLabel;
    private Button _deathContinueButton;
    private Button _deathGiveUpButton;
    private Button _shopButton;

    // Shared commerce surface hosted as a nested Dungeon Shop subview, plus its own stock built from
    // the centrally editable definition resource. Created once in _Ready and reused; the surface
    // stays hidden until Shop is opened. Buy uses Dungeon Points; Sell/Buyback stay Gold-based.
    private const string CommerceScenePath = "res://scenes/ui/merchant_window.tscn";
    private const string DungeonShopDefinitionPath = "res://resources/merchants/dungeon_shop.tres";
    private MerchantWindow _shopCommerce;
    private MerchantStock _shopStock;

    private bool _historyOpen;
    private bool _shopOpen;
    private bool _summaryOpen;
    private bool _deathOpen;
    private DungeonRunRecord _selectedRecord;
    private DungeonRunRecord _summaryRecord;

    private readonly RandomNumberGenerator _seedRng = new();
    private World _world;
    private Dungeon _boundDungeon;
    private Action _resume;
    // Returns null/empty on success, or an actionable error to display while the HUB stays open.
    private Func<ulong, int, DungeonDifficultySelection, string> _startDungeon;
    // Returns null/empty on success, or an actionable error to display while the HUB stays open.
    private Func<string> _giveUp;
    // Death/retry view callbacks (softcore death). Each returns null/empty on success, or an
    // actionable error shown in the death view while it stays open.
    private Func<string> _continueAfterDeath;
    private Func<string> _giveUpAfterDeath;
    private bool _entranceAuthorized;
    private bool _rulesDefaultsInitialized;
    private bool _difficultyRowsBuilt;
    private bool _dungeonAnywhereSubscribed;

    // Difficulty rows the HUB drives, in display order. Each owns its option table, mutually exclusive
    // buttons, selection label and current selection.
    private DungeonDifficultyRules _difficultyRules;
    private readonly List<DifficultyRow> _difficultyRows = new();

    // Reward-adjustment colors: positive green, negative red, zero neutral.
    private static readonly Color PositiveRewardColor = new(0.40f, 0.85f, 0.45f);
    private static readonly Color NegativeRewardColor = new(0.92f, 0.45f, 0.45f);
    private static readonly Color NeutralRewardColor = Colors.White;

    // Identifies how a difficulty row formats its option values and what gameplay field it drives.
    private enum DifficultyRowKind
    {
        StartingLevel,
        LevelIncrease,
        HealthPower,
        Resistance,
        Damage,
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _dpLabel = GetNodeOrNull<Label>(DpLabelPath);
        _configView = GetNodeOrNull<Control>(ConfigViewPath);
        _roomsSpinBox = GetNodeOrNull<SpinBox>(RoomsSpinBoxPath);
        _difficultyContainer = GetNodeOrNull<Container>(DifficultyContainerPath);
        _seedLineEdit = GetNodeOrNull<LineEdit>(SeedLineEditPath);
        _randomizeSeedButton = GetNodeOrNull<Button>(RandomizeSeedButtonPath);
        _hardcoreToggle = GetNodeOrNull<CheckBox>(HardcoreTogglePath);
        _startButton = GetNodeOrNull<Button>(StartButtonPath);
        _statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
        _activeView = GetNodeOrNull<Control>(ActiveViewPath);
        _activeSeedLabel = GetNodeOrNull<Label>(ActiveSeedLabelPath);
        _progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
        _activeLevelLabel = GetNodeOrNull<Label>(ActiveLevelLabelPath);
        _statsLabel = GetNodeOrNull<Label>(StatsLabelPath);
        _resumeButton = GetNodeOrNull<Button>(ResumeButtonPath);
        _giveUpButton = GetNodeOrNull<Button>(GiveUpButtonPath);
        _activeStatusLabel = GetNodeOrNull<Label>(ActiveStatusLabelPath);
        _shopButton = GetNodeOrNull<Button>(ShopButtonPath);
        _historyButton = GetNodeOrNull<Button>(HistoryButtonPath);
        _historyView = GetNodeOrNull<Control>(HistoryViewPath);
        _historyBackButton = GetNodeOrNull<Button>(HistoryBackButtonPath);
        _historyList = GetNodeOrNull<ItemList>(HistoryListPath);
        _historyEmptyLabel = GetNodeOrNull<Label>(HistoryEmptyLabelPath);
        _historyOutcomeLabel = GetNodeOrNull<Label>(HistoryOutcomeLabelPath);
        _historyDetailsLabel = GetNodeOrNull<Label>(HistoryDetailsLabelPath);
        _summaryView = GetNodeOrNull<Control>(SummaryViewPath);
        _summaryTitleLabel = GetNodeOrNull<Label>(SummaryTitleLabelPath);
        _summaryOutcomeLabel = GetNodeOrNull<Label>(SummaryOutcomeLabelPath);
        _summaryDetailsLabel = GetNodeOrNull<Label>(SummaryDetailsLabelPath);
        _summaryCloseButton = GetNodeOrNull<Button>(SummaryCloseButtonPath);
        _deathView = GetNodeOrNull<Control>(DeathViewPath);
        _deathStatusLabel = GetNodeOrNull<Label>(DeathStatusLabelPath);
        _deathContinueButton = GetNodeOrNull<Button>(DeathContinueButtonPath);
        _deathGiveUpButton = GetNodeOrNull<Button>(DeathGiveUpButtonPath);

        ConfigureRoomsSpinBox();

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

        if (_giveUpButton != null)
            _giveUpButton.Pressed += OnGiveUpPressed;

        if (_shopButton != null)
            _shopButton.Pressed += OnShopPressed;

        if (_historyButton != null)
            _historyButton.Pressed += OnHistoryPressed;

        if (_historyBackButton != null)
            _historyBackButton.Pressed += OnHistoryBackPressed;

        if (_historyList != null)
            _historyList.ItemSelected += OnHistoryItemSelected;

        if (_summaryCloseButton != null)
            _summaryCloseButton.Pressed += OnSummaryClosePressed;

        if (_deathContinueButton != null)
            _deathContinueButton.Pressed += OnDeathContinuePressed;

        if (_deathGiveUpButton != null)
            _deathGiveUpButton.Pressed += OnDeathGiveUpPressed;

        GameSettings.DungeonAnywhereChanged += OnDungeonAnywhereChanged;
        _dungeonAnywhereSubscribed = true;

        CreateShop();

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

        if (_giveUpButton != null)
            _giveUpButton.Pressed -= OnGiveUpPressed;

        if (_shopButton != null)
            _shopButton.Pressed -= OnShopPressed;

        if (_shopCommerce != null && GodotObject.IsInstanceValid(_shopCommerce) &&
            _shopCommerce.IsConnected(MerchantWindow.SignalName.CloseRequested, new Callable(this, nameof(OnShopCloseRequested))))
            _shopCommerce.Disconnect(MerchantWindow.SignalName.CloseRequested, new Callable(this, nameof(OnShopCloseRequested)));

        if (_historyButton != null)
            _historyButton.Pressed -= OnHistoryPressed;

        if (_historyBackButton != null)
            _historyBackButton.Pressed -= OnHistoryBackPressed;

        if (_historyList != null)
            _historyList.ItemSelected -= OnHistoryItemSelected;

        if (_summaryCloseButton != null)
            _summaryCloseButton.Pressed -= OnSummaryClosePressed;

        if (_deathContinueButton != null)
            _deathContinueButton.Pressed -= OnDeathContinuePressed;

        if (_deathGiveUpButton != null)
            _deathGiveUpButton.Pressed -= OnDeathGiveUpPressed;

        if (_dungeonAnywhereSubscribed)
        {
            GameSettings.DungeonAnywhereChanged -= OnDungeonAnywhereChanged;
            _dungeonAnywhereSubscribed = false;
        }

        DisconnectDungeonSignal();
    }

    public void Bind(
        World world,
        Action resume,
        Func<ulong, int, DungeonDifficultySelection, string> startDungeon,
        Func<string> giveUp,
        Func<string> continueAfterDeath,
        Func<string> giveUpAfterDeath)
    {
        DisconnectDungeonSignal();

        _world = world;
        _resume = resume;
        _startDungeon = startDungeon;
        _giveUp = giveUp;
        _continueAfterDeath = continueAfterDeath;
        _giveUpAfterDeath = giveUpAfterDeath;

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
        UpdateDpLabel();

        var active = _world != null && GodotObject.IsInstanceValid(_world) && _world.HasActiveDungeonRun;

        // History, the Dungeon Shop, the end-of-run summary and the softcore death/retry view are
        // nested subviews: while any is open the normal page content and its entry buttons are hidden
        // (the Shop surface is an opaque overlay child).
        var nestedOpen = _historyOpen || _shopOpen || _summaryOpen || _deathOpen;

        if (_configView != null)
            _configView.Visible = !nestedOpen && !active;

        if (_activeView != null)
            _activeView.Visible = !nestedOpen && active;

        if (_historyButton != null)
            _historyButton.Visible = !nestedOpen;

        if (_shopButton != null)
            _shopButton.Visible = !nestedOpen;

        if (_historyView != null)
            _historyView.Visible = _historyOpen;

        if (_summaryView != null)
            _summaryView.Visible = _summaryOpen;

        if (_deathView != null)
            _deathView.Visible = _deathOpen;

        // The Shop surface manages its own content; nothing else to refresh while it owns the page.
        if (_shopOpen)
            return;

        // The death/retry view owns the page until the player chooses Continue or Give Up; nothing
        // else to refresh, and a routine refresh (e.g. the DP label) must never swap it away.
        if (_deathOpen)
            return;

        // The summary shows a single immutable finalized record; populate it and stop so a routine
        // refresh (e.g. the DP label above) never swaps back to the config/active view.
        if (_summaryOpen)
        {
            RefreshSummary();
            return;
        }

        if (_historyOpen)
        {
            RefreshHistory();
            return;
        }

        if (active)
            RefreshActiveView();
        else
            RefreshConfigView();
    }

    // Updates the persistent DP balance label from the dungeon's current Points. Shows zero when the
    // dungeon runtime is unresolved so the label is never blank.
    private void UpdateDpLabel()
    {
        if (_dpLabel == null)
            return;

        var points = ResolveDungeon()?.Points ?? 0;
        _dpLabel.Text = $"DP: {points.ToString(CultureInfo.InvariantCulture)}";
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
        SetActiveStatus(string.Empty, isError: false);

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

        if (_statsLabel != null)
        {
            var stats = dungeon.ActiveStats;
            _statsLabel.Text = stats != null
                ? $"Score: {stats.BaseScore}\n" +
                  $"Rooms Cleared: {stats.RoomsCleared}\n" +
                  $"Enemies Killed: {stats.EnemiesKilled}\n" +
                  $"Deaths: {stats.PlayerDeaths}\n" +
                  $"Bosses Defeated: {stats.BossesDefeated}"
                : string.Empty;
        }
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
        var difficulty = BuildDifficultySelection();

        var error = _startDungeon.Invoke(seed, roomCount, difficulty);
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

    private void OnGiveUpPressed()
    {
        if (_giveUp == null)
        {
            SetActiveStatus("Give Up is unavailable.", isError: true);
            return;
        }

        var error = _giveUp.Invoke();
        if (!string.IsNullOrEmpty(error))
        {
            // Failed abandon: keep the HUB open and surface the error. The active run and return
            // origin are preserved by World, so the player is not stranded.
            SetActiveStatus(error, isError: true);
            return;
        }

        // Success: Main closes the HUB after the return transition. Refresh so a later reopen
        // shows the no-run configuration view.
        Refresh();
    }

    // Dungeon Shop (nested commerce subview) ------------------------------------------------------

    public bool IsShopOpen => _shopOpen;

    // Builds the Dungeon Shop's stock from the centrally editable definition resource and an instance
    // of the shared commerce surface, both hosted as children of this page. Created once and reused;
    // the surface stays hidden until Shop is opened. Stack-only stock needs no World/gear rules, so
    // hosting it under the HUB (outside the World tree) is fine.
    private void CreateShop()
    {
        var definition = GD.Load<MerchantDefinition>(DungeonShopDefinitionPath);
        if (definition == null)
        {
            GD.PushWarning($"{nameof(MenuHubDungeonPage)}: failed to load Dungeon Shop definition at '{DungeonShopDefinitionPath}'.");
            return;
        }

        _shopStock = new MerchantStock
        {
            Name = "DungeonShopStock",
            Definition = definition,
        };
        AddChild(_shopStock);

        var commerceScene = ResourceLoader.Load<PackedScene>(CommerceScenePath);
        if (commerceScene?.Instantiate<MerchantWindow>() is not MerchantWindow commerce)
        {
            GD.PushWarning($"{nameof(MenuHubDungeonPage)}: failed to instantiate commerce surface at '{CommerceScenePath}'.");
            return;
        }

        _shopCommerce = commerce;
        AddChild(_shopCommerce);
        _shopCommerce.Connect(MerchantWindow.SignalName.CloseRequested, new Callable(this, nameof(OnShopCloseRequested)));
    }

    private void OnShopPressed()
    {
        if (_shopOpen || _shopCommerce == null || _shopStock == null)
            return;

        var inventory = _world != null && GodotObject.IsInstanceValid(_world)
            ? _world.ResolveInventoryController()
            : null;
        var dungeon = ResolveDungeon();
        if (inventory == null || dungeon == null)
        {
            GD.PushWarning($"{nameof(MenuHubDungeonPage)}: cannot open Dungeon Shop; inventory or dungeon runtime is unavailable.");
            return;
        }

        // Buy spends Dungeon Points; Sell/Buyback stay Gold-based inside the commerce surface.
        _shopOpen = true;
        _shopCommerce.Open(inventory, _shopStock, new DungeonPointsWallet(dungeon));

        // Lock HUB navigation and hide the nav row exactly like History does.
        EmitSignal(SignalName.NestedViewChanged, true);
        Refresh();
    }

    private void OnShopCloseRequested()
    {
        CloseShop();
    }

    // Closes the nested Dungeon Shop subview and returns to the Dungeon page, mirroring CloseHistory.
    // The MenuHub stays open/paused; only this nested view is dismissed. Also called when the HUB
    // closes so reopening Dungeon shows its normal configuration/active view.
    public void CloseShop()
    {
        if (!_shopOpen)
            return;

        _shopOpen = false;
        _shopCommerce?.CloseWindow();
        EmitSignal(SignalName.NestedViewChanged, false);
        Refresh();
    }

    // Run summary (nested end-of-run view) --------------------------------------------------------

    public bool IsSummaryOpen => _summaryOpen;

    // Opens the end-of-run summary for a freshly completed run as a nested subview, mirroring History/
    // Shop: the normal page content and entry buttons hide while it owns the page. The record is the
    // immutable finalized one (also the newest history entry), so the summary shows exactly the data
    // that will persist on save. Main calls this right after a completion opens the HUB.
    public void ShowRunSummary(DungeonRunRecord record)
    {
        if (record == null)
            return;

        _summaryRecord = record;
        _summaryOpen = true;

        // Lock HUB navigation and hide the nav row exactly like History/Shop do.
        EmitSignal(SignalName.NestedViewChanged, true);
        Refresh();
    }

    // Closes the nested summary view (via its Close button, Esc, or when the HUB closes) and returns
    // to the normal Dungeon page. The run is already finalized and cleared, so that is the no-run
    // configuration view, satisfying the "back to outside-run state" requirement.
    public void CloseSummary()
    {
        if (!_summaryOpen)
            return;

        _summaryOpen = false;
        _summaryRecord = null;
        EmitSignal(SignalName.NestedViewChanged, false);
        Refresh();
    }

    private void OnSummaryClosePressed()
    {
        CloseSummary();
    }

    private void RefreshSummary()
    {
        if (_summaryRecord == null)
            return;

        // The summary is shared by completed and failed runs, so the title and outcome color reflect
        // the actual outcome rather than always reading "Run Complete".
        if (_summaryTitleLabel != null)
            _summaryTitleLabel.Text = _summaryRecord.Outcome == DungeonRunOutcome.Failed
                ? "Run Failed"
                : "Run Complete";

        if (_summaryOutcomeLabel != null)
        {
            _summaryOutcomeLabel.Text = $"Outcome: {OutcomeText(_summaryRecord.Outcome)}";
            _summaryOutcomeLabel.Modulate = OutcomeColor(_summaryRecord.Outcome);
        }

        if (_summaryDetailsLabel != null)
            _summaryDetailsLabel.Text = FormatRecordDetails(_summaryRecord);
    }

    // Softcore death/retry (nested view) ----------------------------------------------------------

    public bool IsDeathOpen => _deathOpen;

    // Opens the softcore death/retry view as a nested subview, mirroring Summary/History/Shop: the
    // normal page content and entry buttons hide while it owns the page, and HUB navigation locks.
    // Main calls this when a softcore dungeon death occurs; the run is still active and the player is
    // downed until Continue or Give Up resolves it.
    public void ShowDungeonDeathRetry()
    {
        _deathOpen = true;
        SetDeathStatus(string.Empty, isError: false);

        // Lock HUB navigation and hide the nav row exactly like Summary/History/Shop do.
        EmitSignal(SignalName.NestedViewChanged, true);
        Refresh();
    }

    // Closes the death/retry view and returns to the normal Dungeon page. Used by the Give Up button
    // (the run is finalized, so this lands on the outside-run configuration view) and when the HUB
    // closes after a successful Continue. A failed Continue/Give Up keeps the view open instead.
    public void CloseDeath()
    {
        if (!_deathOpen)
            return;

        _deathOpen = false;
        EmitSignal(SignalName.NestedViewChanged, false);
        Refresh();
    }

    private void OnDeathContinuePressed()
    {
        if (_continueAfterDeath == null)
        {
            SetDeathStatus("Continue is unavailable.", isError: true);
            return;
        }

        var error = _continueAfterDeath.Invoke();
        if (!string.IsNullOrEmpty(error))
        {
            // Failed retry: keep the death view open and surface the error. World left the run active
            // and the player downed, so the player is not stranded.
            SetDeathStatus(error, isError: true);
            return;
        }

        // Success: World rebuilt the room and revived the player; Main closes the HUB and unpauses,
        // which also resets this view. Nothing else to do here.
    }

    private void OnDeathGiveUpPressed()
    {
        if (_giveUpAfterDeath == null)
        {
            SetDeathStatus("Give Up is unavailable.", isError: true);
            return;
        }

        var error = _giveUpAfterDeath.Invoke();
        if (!string.IsNullOrEmpty(error))
        {
            SetDeathStatus(error, isError: true);
            return;
        }

        // Success: the run is finalized as GaveUp and the player revived outside. Step back to the
        // normal outside-run Dungeon view (now the configuration view), keeping the HUB open.
        CloseDeath();
    }

    private void SetDeathStatus(string text, bool isError)
    {
        if (_deathStatusLabel == null)
            return;

        _deathStatusLabel.Text = text ?? string.Empty;
        _deathStatusLabel.Modulate = isError ? new Color(1.0f, 0.6f, 0.6f) : Colors.White;
    }

    // History (nested secondary view) -------------------------------------------------------------

    public bool IsHistoryOpen => _historyOpen;

    // Page-level escape hook used by the HUB/Main: while a nested view (Shop or History) is open,
    // Esc steps back to the ordinary Dungeon view instead of closing the HUB. Returns true when it
    // consumed the event.
    public bool TryHandleEscape()
    {
        // The softcore death view is a hard modal: Esc must never resolve it, because closing back to
        // gameplay would strand a dead player in an unresolved death state. Consume the event without
        // closing so the player can only leave it through Continue or Give Up.
        if (_deathOpen)
            return true;

        if (_summaryOpen)
        {
            CloseSummary();
            return true;
        }

        if (_shopOpen)
        {
            CloseShop();
            return true;
        }

        if (!_historyOpen)
            return false;

        CloseHistory();
        return true;
    }

    // Closes the nested History view, e.g. via Back, Esc, or when the HUB closes through an
    // external lifecycle event so reopening Dungeon shows its normal configuration/active view.
    public void CloseHistory()
    {
        if (!_historyOpen)
            return;

        _historyOpen = false;
        _selectedRecord = null;
        EmitSignal(SignalName.NestedViewChanged, false);
        Refresh();
    }

    private void OnHistoryPressed()
    {
        if (_historyOpen)
            return;

        // Start each open fresh so the newest record is auto-selected.
        _historyOpen = true;
        _selectedRecord = null;
        EmitSignal(SignalName.NestedViewChanged, true);
        Refresh();
    }

    private void OnHistoryBackPressed()
    {
        CloseHistory();
    }

    private void OnHistoryItemSelected(long index)
    {
        var history = ResolveDungeon()?.History;
        if (history == null || index < 0 || index >= history.Count)
            return;

        _selectedRecord = history[(int)index];
        ShowRecordDetails(_selectedRecord);
    }

    private void RefreshHistory()
    {
        if (_historyList == null)
            return;

        var history = ResolveDungeon()?.History;

        if (history == null || history.Count == 0)
        {
            // No stale selection or details once history is empty.
            _historyList.Clear();
            _historyList.Visible = false;
            _selectedRecord = null;
            if (_historyEmptyLabel != null)
                _historyEmptyLabel.Visible = true;
            ShowRecordDetails(null);
            return;
        }

        if (_historyEmptyLabel != null)
            _historyEmptyLabel.Visible = false;
        _historyList.Visible = true;

        // Rebuild the newest-first list from the authoritative history.
        _historyList.Clear();
        for (var i = 0; i < history.Count; i++)
        {
            var record = history[i];
            _historyList.AddItem(FormatHistoryRow(record));
            _historyList.SetItemCustomFgColor(i, OutcomeColor(record.Outcome));
        }

        // Preserve the current selection if its record still exists; otherwise select the newest.
        var selectedIndex = _selectedRecord != null ? IndexOfRecord(history, _selectedRecord) : -1;
        if (selectedIndex < 0)
            selectedIndex = 0;

        _selectedRecord = history[selectedIndex];
        _historyList.Select(selectedIndex);
        _historyList.EnsureCurrentIsVisible();
        ShowRecordDetails(_selectedRecord);
    }

    private void ShowRecordDetails(DungeonRunRecord record)
    {
        if (record == null)
        {
            if (_historyOutcomeLabel != null)
            {
                _historyOutcomeLabel.Text = string.Empty;
                _historyOutcomeLabel.Modulate = Colors.White;
            }

            if (_historyDetailsLabel != null)
                _historyDetailsLabel.Text = string.Empty;

            return;
        }

        if (_historyOutcomeLabel != null)
        {
            _historyOutcomeLabel.Text = $"Outcome: {OutcomeText(record.Outcome)}";
            _historyOutcomeLabel.Modulate = OutcomeColor(record.Outcome);
        }

        if (_historyDetailsLabel != null)
            _historyDetailsLabel.Text = FormatRecordDetails(record);
    }

    // Multi-line run detail text shared by the History details panel and the end-of-run summary, so
    // both always present the same fields formatted identically from one finalized record. Covers the
    // run identity, the difficulty breakdown and aggregate multiplier, the base/final score, the DP
    // awarded, and the run statistics. Legacy-null fields render through the same dash fallbacks.
    private static string FormatRecordDetails(DungeonRunRecord record)
    {
        return
            $"Finished: {FormatFinishedAt(record.FinishedAt)}\n" +
            $"Seed: {record.Seed.ToString(CultureInfo.InvariantCulture)}\n" +
            $"Starting Room Level: {record.StartingRoomLevel}\n" +
            $"Level Increase: {FormatLevelIncrease(record.LevelIncreasePerRoom)}\n" +
            $"Health / Power: {FormatBonusPercent(record.HealthPowerBonus)}\n" +
            $"Resistance: {FormatBonusPercent(record.ResistanceBonus)}\n" +
            $"Damage: {FormatBonusPercent(record.DamageBonus)}\n" +
            $"Planned Run Length: {record.PlannedRunLength}\n" +
            $"Base Score: {FormatScore(record.BaseScore)}\n" +
            $"Difficulty Multiplier: {FormatMultiplier(record.DifficultyMultiplier)}\n" +
            $"Final Score: {FormatScore(record.FinalScore)}\n" +
            $"DP Earned: {FormatPointsEarned(record.PointsEarned)}\n" +
            $"Rooms Cleared: {record.RoomsCleared}\n" +
            $"Enemies Killed: {record.EnemiesKilled}\n" +
            $"Player Deaths: {record.PlayerDeaths}\n" +
            $"Bosses Defeated: {record.BossesDefeated}\n" +
            $"Furthest Room Reached: {record.FurthestRoomIndex}\n" +
            $"Furthest Room Level: {record.FurthestRoomLevel}";
    }

    private static string FormatHistoryRow(DungeonRunRecord record)
    {
        return $"{OutcomeText(record.Outcome)}  ·  {FormatFinishedAt(record.FinishedAt)}  ·  {record.RoomsCleared}/{record.PlannedRunLength}";
    }

    // Concise player-facing local date/time for a finalized run. Legacy records saved before
    // timestamps existed have none and show a fallback instead.
    private static string FormatFinishedAt(DateTimeOffset? finishedAt)
    {
        if (finishedAt == null)
            return "Unknown date";

        return finishedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    // Score / multiplier text for a record's details. Legacy records saved before scoring existed
    // carry no score and show the fallback dash rather than a misleading 0; a real zero score shows
    // as "0".
    private static string FormatScore(int? score)
    {
        return score?.ToString(CultureInfo.InvariantCulture) ?? LegacyScoreFallback;
    }

    // DP earned for a record's details. Legacy records saved before Points existed carry none and
    // show the fallback dash rather than a misleading 0; a real zero award shows as "0".
    private static string FormatPointsEarned(int? pointsEarned)
    {
        return pointsEarned?.ToString(CultureInfo.InvariantCulture) ?? LegacyScoreFallback;
    }

    private static string FormatMultiplier(float? multiplier)
    {
        return multiplier?.ToString("0.00", CultureInfo.InvariantCulture) ?? LegacyScoreFallback;
    }

    // History difficulty fields. Legacy records saved before difficulty existed carry none and show
    // the fallback dash rather than a fabricated value; a real 0% bonus shows as "0%".
    private static string FormatLevelIncrease(int? levelIncrease)
    {
        return levelIncrease.HasValue
            ? $"+{levelIncrease.Value.ToString(CultureInfo.InvariantCulture)}"
            : LegacyScoreFallback;
    }

    private static string FormatBonusPercent(float? bonus)
    {
        return bonus.HasValue ? FormatSignedPercent(bonus.Value) : LegacyScoreFallback;
    }

    private static string OutcomeText(DungeonRunOutcome outcome)
    {
        return outcome switch
        {
            DungeonRunOutcome.Completed => "Completed",
            DungeonRunOutcome.GaveUp => "Gave Up",
            DungeonRunOutcome.Failed => "Failed",
            _ => outcome.ToString(),
        };
    }

    private static Color OutcomeColor(DungeonRunOutcome outcome)
    {
        return outcome switch
        {
            DungeonRunOutcome.Completed => CompletedOutcomeColor,
            DungeonRunOutcome.GaveUp => GaveUpOutcomeColor,
            DungeonRunOutcome.Failed => FailedOutcomeColor,
            _ => Colors.White,
        };
    }

    private static int IndexOfRecord(IReadOnlyList<DungeonRunRecord> history, DungeonRunRecord record)
    {
        for (var i = 0; i < history.Count; i++)
        {
            if (ReferenceEquals(history[i], record))
                return i;
        }

        return -1;
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

    // Drives the immediate DP label update after a completion award or a save load, without a full
    // page refresh.
    private void OnDungeonPointsChanged(int totalPoints)
    {
        UpdateDpLabel();
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
        var dungeon = ResolveDungeon();
        if (dungeon == null)
            return;

        if (!_rulesDefaultsInitialized && dungeon.GenerationRules is { } rules)
        {
            if (_roomsSpinBox != null)
                _roomsSpinBox.Value = rules.OrdinaryRoomCount;

            _rulesDefaultsInitialized = true;
        }

        if (!_difficultyRowsBuilt)
        {
            BuildDifficultyRows(dungeon.DifficultyRules ?? DungeonDifficultyRules.CreateDefault());
            _difficultyRowsBuilt = true;
        }
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

    // Difficulty rows -----------------------------------------------------------------------------

    private const float ModifierColumnWidth = 140.0f;
    private const float SelectionColumnWidth = 150.0f;

    // Builds the header, the five mutually exclusive option rows, and the live summary from the rules
    // tables. Selections default to the first option of every row; the summary reflects them at once.
    private void BuildDifficultyRows(DungeonDifficultyRules rules)
    {
        _difficultyRules = rules;
        _difficultyRows.Clear();

        if (_difficultyContainer == null)
            return;

        foreach (var child in _difficultyContainer.GetChildren())
            child.QueueFree();

        _difficultyContainer.AddChild(BuildHeaderRow());

        AddDifficultyRow(DifficultyRowKind.StartingLevel, "Starting level", rules.StartingLevelOptions, DungeonDifficultyRules.DefaultStartingLevelIndex);
        AddDifficultyRow(DifficultyRowKind.LevelIncrease, "Level increase", rules.LevelIncreaseOptions, DungeonDifficultyRules.DefaultLevelIncreaseIndex);
        AddDifficultyRow(DifficultyRowKind.HealthPower, "Health / Power", rules.EnemyStatOptions, DungeonDifficultyRules.DefaultEnemyStatIndex);
        AddDifficultyRow(DifficultyRowKind.Resistance, "Resistance", rules.EnemyStatOptions, DungeonDifficultyRules.DefaultEnemyStatIndex);
        AddDifficultyRow(DifficultyRowKind.Damage, "Damage", rules.EnemyStatOptions, DungeonDifficultyRules.DefaultEnemyStatIndex);

        _difficultySummaryLabel = new Label
        {
            CustomMinimumSize = new Vector2(480.0f, 0.0f),
        };
        _difficultyContainer.AddChild(_difficultySummaryLabel);

        RefreshDifficultySummary();
    }

    private HBoxContainer BuildHeaderRow()
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        header.AddChild(BuildColumnLabel("Modifier", ModifierColumnWidth));
        header.AddChild(BuildColumnLabel("Selection (Reward bonus)", SelectionColumnWidth));
        header.AddChild(BuildColumnLabel("Available values", 0.0f));
        return header;
    }

    private void AddDifficultyRow(
        DifficultyRowKind kind,
        string label,
        Godot.Collections.Array<DungeonDifficultyOption> options,
        int defaultIndex)
    {
        var rowBox = new HBoxContainer();
        rowBox.AddThemeConstantOverride("separation", 8);
        rowBox.AddChild(BuildColumnLabel(label, ModifierColumnWidth));

        var selectionLabel = BuildColumnLabel(string.Empty, SelectionColumnWidth);
        rowBox.AddChild(selectionLabel);

        var buttonsBox = new HBoxContainer();
        buttonsBox.AddThemeConstantOverride("separation", 4);

        var row = new DifficultyRow(kind, options, selectionLabel);
        var group = new ButtonGroup();
        var optionCount = options?.Count ?? 0;
        var initialIndex = optionCount > 0 ? Math.Clamp(defaultIndex, 0, optionCount - 1) : -1;

        for (var i = 0; i < optionCount; i++)
        {
            var option = options[i];
            var button = new Button
            {
                ToggleMode = true,
                ButtonGroup = group,
                Text = FormatOptionValue(kind, option?.Value ?? 0.0f),
            };

            var optionIndex = i;
            button.Toggled += pressed => OnDifficultyOptionToggled(row, optionIndex, pressed);
            buttonsBox.AddChild(button);
            row.Buttons.Add(button);
        }

        rowBox.AddChild(buttonsBox);
        _difficultyContainer.AddChild(rowBox);
        _difficultyRows.Add(row);

        row.SelectedIndex = initialIndex;
        if (initialIndex >= 0)
            row.Buttons[initialIndex].ButtonPressed = true;

        UpdateRowSelectionLabel(row);
    }

    private void OnDifficultyOptionToggled(DifficultyRow row, int optionIndex, bool pressed)
    {
        // A mutually exclusive group reports the newly pressed button; ignore the matching release of
        // the previously selected one.
        if (!pressed)
            return;

        row.SelectedIndex = optionIndex;
        UpdateRowSelectionLabel(row);
        RefreshDifficultySummary();
    }

    private void UpdateRowSelectionLabel(DifficultyRow row)
    {
        var option = row.SelectedOption;
        if (option == null)
        {
            row.SelectionLabel.Text = "-";
            row.SelectionLabel.Modulate = NeutralRewardColor;
            return;
        }

        row.SelectionLabel.Text = $"{FormatOptionValue(row.Kind, option.Value)} ({FormatSignedPercent(option.RewardAdjustment)})";
        row.SelectionLabel.Modulate = RewardColor(option.RewardAdjustment);
    }

    private void RefreshDifficultySummary()
    {
        if (_difficultySummaryLabel == null)
            return;

        var selection = BuildDifficultySelection();
        if (selection == null)
        {
            _difficultySummaryLabel.Text = string.Empty;
            return;
        }

        var total = selection.TotalRewardAdjustment;
        _difficultySummaryLabel.Text =
            $"Reward bonus: {FormatSignedPercent(total)} ({selection.DifficultyMultiplier.ToString("0.00", CultureInfo.InvariantCulture)}x)";
        _difficultySummaryLabel.Modulate = RewardColor(total);
    }

    // Resolves the current selections into an immutable snapshot. Falls back to the rules defaults
    // when the rows have not been built yet (e.g. an early Start before the page is shown).
    private DungeonDifficultySelection BuildDifficultySelection()
    {
        var rules = _difficultyRules ?? ResolveDungeon()?.DifficultyRules ?? DungeonDifficultyRules.CreateDefault();
        var hardcore = _hardcoreToggle?.ButtonPressed ?? false;

        if (_difficultyRows.Count == 0)
            return DungeonDifficultySelection.CreateDefault(rules, hardcore: hardcore);

        return DungeonDifficultySelection.FromIndices(
            rules,
            SelectedIndex(DifficultyRowKind.StartingLevel),
            SelectedIndex(DifficultyRowKind.LevelIncrease),
            SelectedIndex(DifficultyRowKind.HealthPower),
            SelectedIndex(DifficultyRowKind.Resistance),
            SelectedIndex(DifficultyRowKind.Damage),
            hardcore: hardcore);
    }

    private int SelectedIndex(DifficultyRowKind kind)
    {
        foreach (var row in _difficultyRows)
        {
            if (row.Kind == kind)
                return Math.Max(0, row.SelectedIndex);
        }

        return 0;
    }

    private static Label BuildColumnLabel(string text, float minWidth)
    {
        return new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(minWidth, 0.0f),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static Color RewardColor(float rewardAdjustment)
    {
        if (rewardAdjustment > 0.0f)
            return PositiveRewardColor;

        return rewardAdjustment < 0.0f ? NegativeRewardColor : NeutralRewardColor;
    }

    // Formats an option's gameplay value for its row: an absolute starting level ("50"), a per-room
    // increase ("+2"), or a signed actor-bonus percent ("+40%").
    private static string FormatOptionValue(DifficultyRowKind kind, float value)
    {
        return kind switch
        {
            DifficultyRowKind.StartingLevel => Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture),
            DifficultyRowKind.LevelIncrease => $"+{Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)}",
            _ => FormatSignedPercent(value),
        };
    }

    // Formats an additive fraction as a signed percent: "+25%", "-75%", or "0%" for zero.
    private static string FormatSignedPercent(float fraction)
    {
        var percent = Mathf.RoundToInt(fraction * 100.0f);
        if (percent > 0)
            return $"+{percent.ToString(CultureInfo.InvariantCulture)}%";

        return percent < 0
            ? $"{percent.ToString(CultureInfo.InvariantCulture)}%"
            : "0%";
    }

    // One difficulty row's live UI state: its kind, option table, mutually exclusive buttons, the
    // selection label, and the currently selected option index.
    private sealed class DifficultyRow
    {
        public DifficultyRow(DifficultyRowKind kind, Godot.Collections.Array<DungeonDifficultyOption> options, Label selectionLabel)
        {
            Kind = kind;
            Options = options;
            SelectionLabel = selectionLabel;
        }

        public DifficultyRowKind Kind { get; }
        public Godot.Collections.Array<DungeonDifficultyOption> Options { get; }
        public Label SelectionLabel { get; }
        public List<Button> Buttons { get; } = new();
        public int SelectedIndex { get; set; } = -1;

        public DungeonDifficultyOption SelectedOption =>
            Options != null && SelectedIndex >= 0 && SelectedIndex < Options.Count ? Options[SelectedIndex] : null;
    }

    private void SetStatus(string text, bool isError)
    {
        if (_statusLabel == null)
            return;

        _statusLabel.Text = text ?? string.Empty;
        _statusLabel.Modulate = isError ? new Color(1.0f, 0.6f, 0.6f) : Colors.White;
    }

    private void SetActiveStatus(string text, bool isError)
    {
        if (_activeStatusLabel == null)
            return;

        _activeStatusLabel.Text = text ?? string.Empty;
        _activeStatusLabel.Modulate = isError ? new Color(1.0f, 0.6f, 0.6f) : Colors.White;
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
        _boundDungeon.PointsChanged += OnDungeonPointsChanged;
    }

    private void DisconnectDungeonSignal()
    {
        if (_boundDungeon == null)
            return;

        if (GodotObject.IsInstanceValid(_boundDungeon))
        {
            _boundDungeon.RunStateChanged -= OnDungeonRunStateChanged;
            _boundDungeon.PointsChanged -= OnDungeonPointsChanged;
        }

        _boundDungeon = null;
    }

    private static int ReadSpinBoxInt(SpinBox spinBox, int fallback)
    {
        return spinBox != null ? (int)Math.Round(spinBox.Value) : fallback;
    }
}
