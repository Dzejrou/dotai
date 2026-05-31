using Godot;

using System;

public enum MenuHubPage
{
    GameMenu,
    Inventory,
    Character,
    SpellBook,
}

[GlobalClass]
public partial class MenuHub : Control
{
    [Signal]
    public delegate void ResumeRequestedEventHandler();

    [Signal]
    public delegate void DebugRequestedEventHandler();

    [Signal]
    public delegate void SaveRequestedEventHandler();

    [Signal]
    public delegate void LoadRequestedEventHandler();

    private static readonly Vector2I[] WindowPresets =
    {
        new Vector2I(960, 540),
        new Vector2I(1280, 720),
        new Vector2I(1600, 900),
        new Vector2I(1920, 1080),
        new Vector2I(2560, 1440),
    };

    [Export]
    public NodePath GameMenuPageRootPath { get; set; } = new NodePath("Center");

    [Export]
    public NodePath InventoryPagePath { get; set; } = new NodePath("InventoryPage");

    [Export]
    public NodePath CharacterPagePath { get; set; } = new NodePath("CharacterPage");

    [Export]
    public NodePath SpellBookPagePath { get; set; } = new NodePath("SpellBookPage");

    [Export]
    public NodePath GameMenuPagePath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage");

    [Export]
    public NodePath MainViewPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/MainView");

    [Export]
    public NodePath SettingsViewPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView");

    [Export]
    public NodePath ResumeButtonPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/MainView/ResumeButton");

    [Export]
    public NodePath SaveButtonPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/MainView/SaveButton");

    [Export]
    public NodePath LoadButtonPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/MainView/LoadButton");

    [Export]
    public NodePath SettingsButtonPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/MainView/SettingsButton");

    [Export]
    public NodePath DebugButtonPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/MainView/DebugButton");

    [Export]
    public NodePath BackButtonPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/BackButton");

    [Export]
    public NodePath ShowActorNamesTogglePath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/ShowActorNamesToggle");

    [Export]
    public NodePath ShowFloatingTextTogglePath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/ShowFloatingTextToggle");

    [Export]
    public NodePath ShowCombatLogDebugTogglePath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/ShowCombatLogDebugToggle");

    [Export]
    public NodePath ShowCombatLogTogglePath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/ShowCombatLogToggle");

    [Export]
    public NodePath LockCombatLogPositionTogglePath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/LockCombatLogPositionToggle");

    [Export]
    public NodePath GodModeTogglePath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/GodModeToggle");

    [Export]
    public NodePath OneHitKillTogglePath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/OneHitKillToggle");

    [Export]
    public NodePath WindowSizeLabelPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/WindowSizeRow/WindowSizeLabel");

    [Export]
    public NodePath WindowSizeSmallerButtonPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/WindowSizeRow/SmallerButton");

    [Export]
    public NodePath WindowSizeLargerButtonPath { get; set; } = new NodePath("Center/Panel/Pages/GameMenuPage/SettingsView/WindowSizeRow/LargerButton");

    private readonly GameConfigStore _gameConfigStore = new();
    private Control _gameMenuPageRoot;
    private MenuHubInventoryPage _inventoryPage;
    private MenuHubCharacterPage _characterPage;
    private MenuHubSpellBookPage _spellBookPage;
    private Control _gameMenuPage;
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
    private BaseButton _showCombatLogToggle;
    private BaseButton _lockCombatLogPositionToggle;
    private BaseButton _godModeToggle;
    private BaseButton _oneHitKillToggle;
    private Label _windowSizeLabel;
    private Button _windowSizeSmallerButton;
    private Button _windowSizeLargerButton;
    private int _windowPresetIndex;

    public bool IsOpen => Visible;

    public MenuHubPage CurrentPage { get; private set; } = MenuHubPage.GameMenu;

    public MenuHubInventoryPage InventoryPage => _inventoryPage;

    public MenuHubCharacterPage CharacterPage => _characterPage;

    public MenuHubSpellBookPage SpellBookPage => _spellBookPage;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _gameConfigStore.LoadGameSettings();

        _gameMenuPageRoot = GetNodeOrNull<Control>(GameMenuPageRootPath);
        _inventoryPage = GetNodeOrNull<MenuHubInventoryPage>(InventoryPagePath);
        _characterPage = GetNodeOrNull<MenuHubCharacterPage>(CharacterPagePath);
        _spellBookPage = GetNodeOrNull<MenuHubSpellBookPage>(SpellBookPagePath);
        _gameMenuPage = GetNodeOrNull<Control>(GameMenuPagePath);
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
        _showCombatLogToggle = GetNodeOrNull<BaseButton>(ShowCombatLogTogglePath);
        _lockCombatLogPositionToggle = GetNodeOrNull<BaseButton>(LockCombatLogPositionTogglePath);
        _godModeToggle = GetNodeOrNull<BaseButton>(GodModeTogglePath);
        _oneHitKillToggle = GetNodeOrNull<BaseButton>(OneHitKillTogglePath);
        _windowSizeLabel = GetNodeOrNull<Label>(WindowSizeLabelPath);
        _windowSizeSmallerButton = GetNodeOrNull<Button>(WindowSizeSmallerButtonPath);
        _windowSizeLargerButton = GetNodeOrNull<Button>(WindowSizeLargerButtonPath);

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

        if (_showCombatLogToggle != null)
        {
            _showCombatLogToggle.ButtonPressed = GameSettings.ShowCombatLog;
            _showCombatLogToggle.Toggled += OnShowCombatLogToggled;
        }

        if (_lockCombatLogPositionToggle != null)
        {
            _lockCombatLogPositionToggle.ButtonPressed = GameSettings.LockCombatLogPosition;
            _lockCombatLogPositionToggle.Toggled += OnLockCombatLogPositionToggled;
        }

        if (_godModeToggle != null)
        {
            _godModeToggle.ButtonPressed = GameSettings.GodMode;
            _godModeToggle.Toggled += OnGodModeToggled;
        }

        if (_oneHitKillToggle != null)
        {
            _oneHitKillToggle.ButtonPressed = GameSettings.OneHitKill;
            _oneHitKillToggle.Toggled += OnOneHitKillToggled;
        }

        if (_windowSizeSmallerButton != null)
            _windowSizeSmallerButton.Pressed += OnWindowSizeSmallerPressed;

        if (_windowSizeLargerButton != null)
            _windowSizeLargerButton.Pressed += OnWindowSizeLargerPressed;

        InitializeWindowPreset();
        RefreshWindowSizeView();

        ShowPage(MenuHubPage.GameMenu);
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

        if (_showCombatLogToggle != null)
            _showCombatLogToggle.Toggled -= OnShowCombatLogToggled;

        if (_lockCombatLogPositionToggle != null)
            _lockCombatLogPositionToggle.Toggled -= OnLockCombatLogPositionToggled;

        if (_godModeToggle != null)
            _godModeToggle.Toggled -= OnGodModeToggled;

        if (_oneHitKillToggle != null)
            _oneHitKillToggle.Toggled -= OnOneHitKillToggled;

        if (_windowSizeSmallerButton != null)
            _windowSizeSmallerButton.Pressed -= OnWindowSizeSmallerPressed;

        if (_windowSizeLargerButton != null)
            _windowSizeLargerButton.Pressed -= OnWindowSizeLargerPressed;
    }

    public void Open(MenuHubPage page = MenuHubPage.GameMenu)
    {
        ShowPage(page);
        Visible = true;
    }

    public void Close()
    {
        Visible = false;
        _inventoryPage?.OnHubClosed();
    }

    public void SwitchTo(MenuHubPage page)
    {
        if (CurrentPage == page)
            return;

        ShowPage(page);
    }

    public void BindInventoryPage(Player player, InventoryController inventory, EquipmentController equipment)
    {
        if (_inventoryPage == null)
            return;

        _inventoryPage.BindPlayer(player);
        _inventoryPage.Bind(inventory, equipment);
    }

    public void BindCharacterPage(Player player, EquipmentController equipment)
    {
        _characterPage?.Bind(player, equipment);
    }

    public void BindSpellBookPage(Player player)
    {
        _spellBookPage?.Bind(player);
    }

    public void SetInventoryPageWorldDropHandlers(Action<int, int> inventoryDrop, Action<GearInstance> gearDrop)
    {
        if (_inventoryPage == null)
            return;

        _inventoryPage.InventoryDropToWorldRequested = inventoryDrop;
        _inventoryPage.GearDropToWorldRequested = gearDrop;
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

    private void OnShowCombatLogToggled(bool pressed)
    {
        GameSettings.SetShowCombatLog(pressed);
        PersistSettings();
    }

    private void OnLockCombatLogPositionToggled(bool pressed)
    {
        GameSettings.SetLockCombatLogPosition(pressed);
        PersistSettings();
    }

    private void OnGodModeToggled(bool pressed)
    {
        GameSettings.SetGodMode(pressed);
        PersistSettings();
    }

    private void OnOneHitKillToggled(bool pressed)
    {
        GameSettings.SetOneHitKill(pressed);
        PersistSettings();
    }

    private void OnWindowSizeSmallerPressed()
    {
        StepWindowSize(-1);
    }

    private void OnWindowSizeLargerPressed()
    {
        StepWindowSize(1);
    }

    private void StepWindowSize(int delta)
    {
        var newIndex = Mathf.Clamp(_windowPresetIndex + delta, 0, WindowPresets.Length - 1);
        if (newIndex == _windowPresetIndex)
        {
            RefreshWindowSizeView();
            return;
        }

        _windowPresetIndex = newIndex;
        var newSize = WindowPresets[_windowPresetIndex];
        DisplayServer.WindowSetSize(newSize);
        GD.Print($"Window size set to {newSize.X}x{newSize.Y}");
        RefreshWindowSizeView();
    }

    private void InitializeWindowPreset()
    {
        var currentSize = DisplayServer.WindowGetSize();
        var closestPresetIndex = 0;
        var closestDistanceSq = int.MaxValue;

        for (var i = 0; i < WindowPresets.Length; i++)
        {
            var preset = WindowPresets[i];
            var dx = currentSize.X - preset.X;
            var dy = currentSize.Y - preset.Y;
            var distanceSq = dx * dx + dy * dy;
            if (distanceSq < closestDistanceSq)
            {
                closestDistanceSq = distanceSq;
                closestPresetIndex = i;
            }
        }

        _windowPresetIndex = Mathf.Clamp(closestPresetIndex, 0, WindowPresets.Length - 1);
    }

    private void RefreshWindowSizeView()
    {
        if (_windowSizeLabel != null)
        {
            var size = WindowPresets[_windowPresetIndex];
            _windowSizeLabel.Text = $"Window size: {size.X}x{size.Y}";
        }

        if (_windowSizeSmallerButton != null)
            _windowSizeSmallerButton.Disabled = _windowPresetIndex == 0;

        if (_windowSizeLargerButton != null)
            _windowSizeLargerButton.Disabled = _windowPresetIndex == WindowPresets.Length - 1;
    }

    private void PersistSettings()
    {
        if (!_gameConfigStore.TrySaveGameSettings(out var message))
            GD.PushWarning(message);
    }

    private void ShowPage(MenuHubPage page)
    {
        CurrentPage = page;

        if (_gameMenuPageRoot != null)
            _gameMenuPageRoot.Visible = page == MenuHubPage.GameMenu;

        if (_inventoryPage != null)
            _inventoryPage.Visible = page == MenuHubPage.Inventory;

        if (_characterPage != null)
            _characterPage.Visible = page == MenuHubPage.Character;

        if (_spellBookPage != null)
            _spellBookPage.Visible = page == MenuHubPage.SpellBook;

        if (page == MenuHubPage.Inventory)
            _inventoryPage?.OnPageEntered();

        if (page == MenuHubPage.Character)
            _characterPage?.OnPageEntered();

        if (page == MenuHubPage.SpellBook)
            _spellBookPage?.OnPageEntered();
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
