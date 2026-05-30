using Godot;

using System;

[GlobalClass]
public partial class Main : Node2D
{
    private static readonly Vector2I[] WindowPresets =
    {
        new Vector2I(960, 540),
        new Vector2I(1280, 720),
        new Vector2I(1600, 900),
        new Vector2I(1920, 1080),
        new Vector2I(2560, 1440),
    };

    [Export]
    public NodePath WorldPath { get; set; } = new NodePath("World");

    [Export]
    public NodePath GameOverPath { get; set; } = new NodePath("GameOver/Root");

    [Export]
    public NodePath PauseMenuPath { get; set; } = new NodePath("PauseMenu/Root");

    [Export]
    public NodePath DebugTrayPath { get; set; } = new NodePath("DebugTray/Root");

    private World _world;
    private Player _player;
    private InventoryController _inventoryController;
    private Control _gameOverRoot;
    private PauseMenu _pauseMenuRoot;
    private DebugTray _debugTrayRoot;
    private bool _gameOverActive;
    private bool _restartingFromGameOver;
    private bool _pauseMenuOpen;
    private CastBar _castBar;
    private PlayerSpellBar _spellBar;
    private PlayerSpellBindingWindow _spellBindingWindow;
    private InventoryWindow _inventoryWindow;
    private CharacterWindow _characterWindow;
    private PlayerDebugStatsWindow _playerDebugStatsWindow;
    private MerchantWindow _merchantWindow;
    private CombatLogPanel _combatLogPanel;
    private Sprite2D _interactionPrompt;
    private const string CastBarScenePath = "res://scenes/ui/cast_bar.tscn";
    private const string PlayerSpellBarScenePath = "res://scenes/ui/player_spell_bar.tscn";
    private const string PlayerSpellBindingWindowScenePath = "res://scenes/ui/player_spell_binding_window.tscn";
    private const string InventoryWindowScenePath = "res://scenes/ui/inventory_window.tscn";
    private const string CharacterWindowScenePath = "res://scenes/ui/character_window.tscn";
    private const string PlayerDebugStatsWindowScenePath = "res://scenes/ui/player_debug_stats_window.tscn";
    private const string MerchantWindowScenePath = "res://scenes/ui/merchant_window.tscn";
    private const string CountdownHudScenePath = "res://scenes/ui/countdown_hud.tscn";
    private const string CombatLogPanelScenePath = "res://scenes/ui/combat_log_panel.tscn";
    private const string InteractionPromptGlyphPath = "res://assets/glyphs/letter_g.png";
    private const string SpellBookActionName = "spell_book";
    private const string ToggleInventoryActionName = "toggle_inventory";
    private const string ToggleCharacterWindowActionName = "toggle_character_window";
    private int _windowPresetIndex;
    private CountdownHUD _countdownHud;
    private readonly SaveGameStore _saveGameStore = new();

    public CountdownHUD ResolveCountdownHud()
    {
        return _countdownHud != null && GodotObject.IsInstanceValid(_countdownHud)
            ? _countdownHud
            : null;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _world = GetNodeOrNull<World>(WorldPath);
        if (_world != null)
        {
            _world.Connect(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
            _world.Connect(World.SignalName.MerchantInteractionRequested, new Callable(this, nameof(OnMerchantInteractionRequested)));
        }

        _gameOverRoot = GetNodeOrNull<Control>(GameOverPath);
        _pauseMenuRoot = GetNodeOrNull<PauseMenu>(PauseMenuPath);
        _debugTrayRoot = GetNodeOrNull<DebugTray>(DebugTrayPath);
        CreateHud();

        if (_gameOverRoot != null)
        {
            _gameOverRoot.Visible = false;
            _gameOverRoot.ProcessMode = ProcessModeEnum.Always;
        }

        if (_pauseMenuRoot != null)
        {
            _pauseMenuRoot.Visible = false;
            _pauseMenuRoot.ProcessMode = ProcessModeEnum.Always;
            _pauseMenuRoot.Connect(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested)));
            _pauseMenuRoot.Connect(PauseMenu.SignalName.DebugRequested, new Callable(this, nameof(OnPauseMenuDebugRequested)));
            _pauseMenuRoot.Connect(PauseMenu.SignalName.SaveRequested, new Callable(this, nameof(OnPauseMenuSaveRequested)));
            _pauseMenuRoot.Connect(PauseMenu.SignalName.LoadRequested, new Callable(this, nameof(OnPauseMenuLoadRequested)));
        }

        if (_debugTrayRoot != null)
        {
            _debugTrayRoot.Visible = false;
            _debugTrayRoot.ProcessMode = ProcessModeEnum.Always;
            _debugTrayRoot.Connect(DebugTray.SignalName.PlayerStatsRequested, new Callable(this, nameof(OnDebugTrayPlayerStatsRequested)));
        }

        var playerPath = _world != null && !_world.PlayerPath.IsEmpty ? _world.PlayerPath : new NodePath("Player");
        var player = _world?.GetNodeOrNull<Player>(playerPath);
        _player = player;
        if (player != null)
        {
            player.Connect(Player.SignalName.InteractionAvailabilityChanged, new Callable(this, nameof(OnPlayerInteractionAvailabilityChanged)));
            player.BindCastBar(_castBar);
            _spellBar?.Bind(player);
            _spellBindingWindow?.Bind(player);
            _playerDebugStatsWindow?.Bind(player);
            UpdateInteractionPrompt(player.HasInteractionTarget);
        }
        else
        {
            _castBar?.HideCast();
            _spellBar?.Bind(null);
            _spellBindingWindow?.Bind(null);
            _playerDebugStatsWindow?.Bind(null);
            UpdateInteractionPrompt(false);
        }

        _inventoryController = _world != null && !_world.InventoryPath.IsEmpty
            ? _world.ResolveInventoryController()
            : null;
        var equipmentController = _player?.EquipmentControllerNode;
        _inventoryWindow?.BindPlayer(_player);
        _inventoryWindow?.Bind(_inventoryController, equipmentController);
        _characterWindow?.Bind(_inventoryController, equipmentController);
        _characterWindow?.BindStatsOwner(_player);

        InitializeWindowPreset();

        TryLoadFromSave();
    }

    public override void _ExitTree()
    {
        if (_world == null)
            return;

        if (GodotObject.IsInstanceValid(_world) &&
            _world.IsConnected(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied))))
            _world.Disconnect(World.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));

        if (GodotObject.IsInstanceValid(_world) &&
            _world.IsConnected(World.SignalName.MerchantInteractionRequested, new Callable(this, nameof(OnMerchantInteractionRequested))))
            _world.Disconnect(World.SignalName.MerchantInteractionRequested, new Callable(this, nameof(OnMerchantInteractionRequested)));

        if (GodotObject.IsInstanceValid(_player) &&
            _player.IsConnected(Player.SignalName.InteractionAvailabilityChanged, new Callable(this, nameof(OnPlayerInteractionAvailabilityChanged))))
            _player.Disconnect(Player.SignalName.InteractionAvailabilityChanged, new Callable(this, nameof(OnPlayerInteractionAvailabilityChanged)));

        if (GodotObject.IsInstanceValid(_pauseMenuRoot) &&
            _pauseMenuRoot.IsConnected(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested))))
            _pauseMenuRoot.Disconnect(PauseMenu.SignalName.ResumeRequested, new Callable(this, nameof(OnPauseMenuResumeRequested)));

        if (GodotObject.IsInstanceValid(_pauseMenuRoot) &&
            _pauseMenuRoot.IsConnected(PauseMenu.SignalName.DebugRequested, new Callable(this, nameof(OnPauseMenuDebugRequested))))
            _pauseMenuRoot.Disconnect(PauseMenu.SignalName.DebugRequested, new Callable(this, nameof(OnPauseMenuDebugRequested)));

        if (GodotObject.IsInstanceValid(_pauseMenuRoot) &&
            _pauseMenuRoot.IsConnected(PauseMenu.SignalName.SaveRequested, new Callable(this, nameof(OnPauseMenuSaveRequested))))
            _pauseMenuRoot.Disconnect(PauseMenu.SignalName.SaveRequested, new Callable(this, nameof(OnPauseMenuSaveRequested)));

        if (GodotObject.IsInstanceValid(_pauseMenuRoot) &&
            _pauseMenuRoot.IsConnected(PauseMenu.SignalName.LoadRequested, new Callable(this, nameof(OnPauseMenuLoadRequested))))
            _pauseMenuRoot.Disconnect(PauseMenu.SignalName.LoadRequested, new Callable(this, nameof(OnPauseMenuLoadRequested)));

        if (GodotObject.IsInstanceValid(_debugTrayRoot) &&
            _debugTrayRoot.IsConnected(DebugTray.SignalName.PlayerStatsRequested, new Callable(this, nameof(OnDebugTrayPlayerStatsRequested))))
            _debugTrayRoot.Disconnect(DebugTray.SignalName.PlayerStatsRequested, new Callable(this, nameof(OnDebugTrayPlayerStatsRequested)));
    }

    public override void _Input(InputEvent @event)
    {
        if (TryHandleWindowResizeInput(@event))
            return;

        if (TryHandleNavigationDebugInput(@event))
            return;

        if (_gameOverActive && !_restartingFromGameOver)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
                RestartFromGameOver();

            return;
        }

        if (TryHandleSpellBookInput(@event))
            return;

        if (TryHandleInventoryInput(@event))
            return;

        if (TryHandleCharacterWindowInput(@event))
            return;

        TryHandlePauseMenuInput(@event);
    }

    public override void _Process(double delta)
    {
        UpdateInteractionPromptPosition();
    }

    private void OnPlayerDied()
    {
        if (_gameOverActive)
            return;

        ClosePauseMenu();
        CloseDebugTray(false);
        UpdateInteractionPrompt(false);
        _gameOverActive = true;
        _spellBindingWindow?.CloseWindow();
        _inventoryWindow?.CloseWindow();
        _characterWindow?.CloseWindow();
        _playerDebugStatsWindow?.CloseWindow();
        _merchantWindow?.CloseWindow();
        GetTree().Paused = true;

        if (_gameOverRoot == null)
            return;

        _gameOverRoot.Visible = true;
    }

    private void OnPlayerInteractionAvailabilityChanged(bool available)
    {
        UpdateInteractionPrompt(available);
    }

    private void OnMerchantInteractionRequested(MerchantStock stock, Player player)
    {
        if (_merchantWindow == null || !GodotObject.IsInstanceValid(_merchantWindow))
            return;
        if (stock == null || !GodotObject.IsInstanceValid(stock))
            return;
        if (player == null || !GodotObject.IsInstanceValid(player))
            return;
        if (_inventoryController == null || !GodotObject.IsInstanceValid(_inventoryController))
            return;

        // Match the convention used by other window openers: close adjacent windows first.
        _inventoryWindow?.CloseWindow();
        _characterWindow?.CloseWindow();
        _spellBindingWindow?.CloseWindow();

        _merchantWindow.Open(_inventoryController, stock);
    }

    private void RestartFromGameOver()
    {
        _restartingFromGameOver = true;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void OnPauseMenuResumeRequested()
    {
        ClosePauseMenu();
    }

    private void OnPauseMenuDebugRequested()
    {
        ClosePauseMenu();
        OpenDebugTray();
    }

    private void OnPauseMenuSaveRequested()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            GD.Print("Save refused: no player available.");
            return;
        }

        if (_player.IsDead)
        {
            GD.Print("Save refused: player is dead.");
            return;
        }

        if (_player.InCombat)
        {
            GD.Print("Save refused: player is in combat.");
            return;
        }

        var equipmentController = _player.EquipmentControllerNode;
        if (_inventoryController == null || equipmentController == null)
        {
            GD.Print("Save refused: inventory or equipment controller unavailable.");
            return;
        }

        var data = new SaveGameData
        {
            Player = _player.CreateSaveSnapshot(),
            Inventory = _inventoryController.CreateSaveSnapshot(),
            Equipment = equipmentController.CreateSaveSnapshot(),
        };

        if (_saveGameStore.TrySave(data, out var message))
            GD.Print(message);
        else
            GD.PushWarning(message);
    }

    private void OnPauseMenuLoadRequested()
    {
        switch (TryApplySaveFromDisk())
        {
            case LoadAttemptResult.Applied:
                return;
            case LoadAttemptResult.NoSave:
                GD.Print($"Load skipped: no valid save at {SaveGameStore.SaveFilePath}.");
                return;
            case LoadAttemptResult.RuntimeUnavailable:
                GD.Print("Load refused: required runtime nodes are unavailable.");
                return;
        }
    }

    private void TryLoadFromSave()
    {
        TryApplySaveFromDisk();
    }

    private enum LoadAttemptResult
    {
        Applied,
        NoSave,
        RuntimeUnavailable,
    }

    private LoadAttemptResult TryApplySaveFromDisk()
    {
        if (!_saveGameStore.TryLoad(out var data) || data == null)
            return LoadAttemptResult.NoSave;

        if (_inventoryController == null ||
            _player == null ||
            !GodotObject.IsInstanceValid(_player))
        {
            return LoadAttemptResult.RuntimeUnavailable;
        }

        var equipmentController = _player.EquipmentControllerNode;
        if (equipmentController == null)
            return LoadAttemptResult.RuntimeUnavailable;

        // Apply order: inventory -> equipment -> player level/XP -> current HP/mana.
        // Equipment must land before HP/mana so the resolved Max values are correct
        // when we set Current.
        _inventoryController.LoadFromSnapshot(data.Inventory);
        equipmentController.LoadFromSnapshot(data.Equipment, _inventoryController.GearGenerationRules);

        if (data.Player != null)
        {
            _player.ApplyLoadedLevelAndExperience(data.Player.Level, data.Player.CurrentExperience);
            _player.ApplyLoadedHealthAndMana(data.Player.CurrentHealth, data.Player.CurrentMana);
        }

        GD.Print($"Loaded save from {SaveGameStore.SaveFilePath}.");
        return LoadAttemptResult.Applied;
    }

    private void OnDebugTrayPlayerStatsRequested()
    {
        _playerDebugStatsWindow?.ToggleWindow();
    }

    private void UpdateInteractionPrompt(bool available)
    {
        if (_interactionPrompt == null)
            return;

        _interactionPrompt.Visible = available && _player != null && _player.CurrentInteractionTarget != null;
        UpdateInteractionPromptPosition();
    }

    private void CreateHud()
    {
        var hudCanvas = new CanvasLayer
        {
            Name = "WorldHUD",
            Layer = 100
        };
        AddChild(hudCanvas);

        var castBarScene = ResourceLoader.Load<PackedScene>(CastBarScenePath);
        if (castBarScene?.Instantiate<CastBar>() is CastBar castBar)
        {
            _castBar = castBar;
            hudCanvas.AddChild(_castBar);
        }

        var spellBarScene = ResourceLoader.Load<PackedScene>(PlayerSpellBarScenePath);
        if (spellBarScene?.Instantiate<PlayerSpellBar>() is PlayerSpellBar spellBar)
        {
            _spellBar = spellBar;
            hudCanvas.AddChild(_spellBar);
        }

        var spellBindingWindowScene = ResourceLoader.Load<PackedScene>(PlayerSpellBindingWindowScenePath);
        if (spellBindingWindowScene?.Instantiate<PlayerSpellBindingWindow>() is PlayerSpellBindingWindow spellBindingWindow)
        {
            _spellBindingWindow = spellBindingWindow;
            hudCanvas.AddChild(_spellBindingWindow);
        }

        var inventoryWindowScene = ResourceLoader.Load<PackedScene>(InventoryWindowScenePath);
        if (inventoryWindowScene?.Instantiate<InventoryWindow>() is InventoryWindow inventoryWindow)
        {
            _inventoryWindow = inventoryWindow;
            _inventoryWindow.Connect(InventoryWindow.SignalName.ItemDroppedToWorld, new Callable(this, nameof(OnInventoryItemDroppedToWorld)));
            hudCanvas.AddChild(_inventoryWindow);
        }

        var characterWindowScene = ResourceLoader.Load<PackedScene>(CharacterWindowScenePath);
        if (characterWindowScene?.Instantiate<CharacterWindow>() is CharacterWindow characterWindow)
        {
            _characterWindow = characterWindow;
            hudCanvas.AddChild(_characterWindow);
        }

        var playerDebugStatsWindowScene = ResourceLoader.Load<PackedScene>(PlayerDebugStatsWindowScenePath);
        if (playerDebugStatsWindowScene?.Instantiate<PlayerDebugStatsWindow>() is PlayerDebugStatsWindow playerDebugStatsWindow)
        {
            _playerDebugStatsWindow = playerDebugStatsWindow;
            hudCanvas.AddChild(_playerDebugStatsWindow);
        }

        var merchantWindowScene = ResourceLoader.Load<PackedScene>(MerchantWindowScenePath);
        if (merchantWindowScene?.Instantiate<MerchantWindow>() is MerchantWindow merchantWindow)
        {
            _merchantWindow = merchantWindow;
            hudCanvas.AddChild(_merchantWindow);
        }

        var countdownHudScene = ResourceLoader.Load<PackedScene>(CountdownHudScenePath);
        if (countdownHudScene?.Instantiate<CountdownHUD>() is CountdownHUD countdownHud)
        {
            _countdownHud = countdownHud;
            hudCanvas.AddChild(_countdownHud);
        }

        var combatLogPanelScene = ResourceLoader.Load<PackedScene>(CombatLogPanelScenePath);
        if (combatLogPanelScene?.Instantiate<CombatLogPanel>() is CombatLogPanel combatLogPanel)
        {
            _combatLogPanel = combatLogPanel;
            hudCanvas.AddChild(_combatLogPanel);
        }

        var interactionPromptTexture = ResourceLoader.Load<Texture2D>(InteractionPromptGlyphPath);
        if (interactionPromptTexture == null)
        {
            GD.PushError($"Failed to load interaction prompt glyph at {InteractionPromptGlyphPath}.");
            return;
        }

        _interactionPrompt = new Sprite2D
        {
            Name = "InteractionPrompt",
            Visible = false,
            Centered = true,
            Texture = interactionPromptTexture,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 1000,
        };
        AddChild(_interactionPrompt);
    }

    private void UpdateInteractionPromptPosition()
    {
        if (_interactionPrompt == null || !_interactionPrompt.Visible)
            return;

        if (_player == null ||
            !_player.HasInteractionTarget ||
            _player.CurrentInteractionTarget == null ||
            !_player.TryGetInteractionPromptPosition(out var promptPosition))
        {
            _interactionPrompt.Visible = false;
            return;
        }

        _interactionPrompt.GlobalPosition = promptPosition;
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

    private bool TryHandleWindowResizeInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        var shouldIncrease = keyEvent.PhysicalKeycode == Key.Key9;
        var shouldDecrease = keyEvent.PhysicalKeycode == Key.Key0;

        if (!shouldIncrease && !shouldDecrease)
            return false;

        var newIndex = _windowPresetIndex;
        if (shouldIncrease)
            newIndex++;
        else
            newIndex--;

        newIndex = Mathf.Clamp(newIndex, 0, WindowPresets.Length - 1);
        if (newIndex == _windowPresetIndex)
            return true;

        _windowPresetIndex = newIndex;
        var newSize = WindowPresets[_windowPresetIndex];
        DisplayServer.WindowSetSize(newSize);
        GD.Print($"Window size set to {newSize.X}x{newSize.Y}");

        return true;
    }

    private bool TryHandlePauseMenuInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (keyEvent.PhysicalKeycode == Key.P)
        {
            _spellBindingWindow?.CloseWindow();
            if (_debugTrayRoot != null && _debugTrayRoot.TrayVisible)
                CloseDebugTray();
            else
            {
                ClosePauseMenu();
                OpenDebugTray();
            }

            return true;
        }

        if (keyEvent.PhysicalKeycode != Key.Escape)
            return false;

        if (_player != null &&
            GodotObject.IsInstanceValid(_player) &&
            _player.TryCancelSpellInputFromEscape())
        {
            return true;
        }

        if (_debugTrayRoot != null && _debugTrayRoot.TrayVisible)
        {
            if (_debugTrayRoot.HandleEscape())
                return true;

            CloseDebugTray();
            OpenPauseMenu();
            return true;
        }

        if (_pauseMenuOpen)
            ClosePauseMenu();
        else
            OpenPauseMenu();

        return true;
    }

    private bool TryHandleSpellBookInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (!InputMap.HasAction(SpellBookActionName) || !@event.IsActionPressed(SpellBookActionName))
            return false;

        if (_pauseMenuOpen || (_debugTrayRoot != null && _debugTrayRoot.TrayVisible))
            return true;

        _inventoryWindow?.CloseWindow();
        _characterWindow?.CloseWindow();
        _spellBindingWindow?.ToggleWindow();
        return true;
    }

    private bool TryHandleInventoryInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (!InputMap.HasAction(ToggleInventoryActionName) || !@event.IsActionPressed(ToggleInventoryActionName))
            return false;

        if (_pauseMenuOpen || (_debugTrayRoot != null && _debugTrayRoot.TrayVisible))
            return true;

        _spellBindingWindow?.CloseWindow();
        _inventoryWindow?.ToggleWindow();
        return true;
    }

    private bool TryHandleCharacterWindowInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (!InputMap.HasAction(ToggleCharacterWindowActionName) ||
            !@event.IsActionPressed(ToggleCharacterWindowActionName))
        {
            return false;
        }

        if (_pauseMenuOpen || (_debugTrayRoot != null && _debugTrayRoot.TrayVisible))
            return true;

        _spellBindingWindow?.CloseWindow();
        _characterWindow?.ToggleWindow();
        return true;
    }

    private bool TryHandleNavigationDebugInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (keyEvent.PhysicalKeycode != Key.Key8)
            return false;

        var enabled = NavigationDebugSettings.Toggle();
        GD.Print($"Navigation debug {(enabled ? "enabled" : "disabled")}");
        return true;
    }

    private void OpenPauseMenu()
    {
        CloseDebugTray();
        _spellBindingWindow?.CloseWindow();
        _inventoryWindow?.CloseWindow();
        _characterWindow?.CloseWindow();
        _merchantWindow?.CloseWindow();
        _pauseMenuOpen = true;
        if (_pauseMenuRoot != null)
            _pauseMenuRoot.Visible = true;

        GetTree().Paused = true;
    }

    private void ClosePauseMenu()
    {
        _pauseMenuOpen = false;
        if (_pauseMenuRoot != null)
            _pauseMenuRoot.Visible = false;

        if (!_gameOverActive)
            GetTree().Paused = false;
    }

    private void OpenDebugTray()
    {
        _inventoryWindow?.CloseWindow();
        _characterWindow?.CloseWindow();
        _merchantWindow?.CloseWindow();

        if (_debugTrayRoot != null)
            _debugTrayRoot.Open();

        if (!_gameOverActive)
            GetTree().Paused = false;
    }

    private void CloseDebugTray(bool cancelPlacement = true)
    {
        if (_debugTrayRoot != null)
            _debugTrayRoot.Close(cancelPlacement);
    }

    private void OnInventoryItemDroppedToWorld(int slotIndex, int amount)
    {
        if (_inventoryController == null || !GodotObject.IsInstanceValid(_inventoryController))
            return;

        // Preflight: verify slot still has an entry before doing anything destructive.
        if (!_inventoryController.TryGetEntry(slotIndex, out var entry) || entry?.Definition == null)
            return;

        var requestedAmount = Math.Max(1, amount);

        // Preflight: require an active room and living player to receive the drop.
        var room = _world?.ActiveRoom;
        if (room == null || !GodotObject.IsInstanceValid(room))
            return;

        if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
            return;

        // Preflight: load and instantiate the drop scene before touching inventory.
        var dropScene = ResourceLoader.Load<PackedScene>("res://scenes/world/drops/inventory_item_drop.tscn");
        if (dropScene == null)
        {
            GD.PushError($"{nameof(Main)}: failed to load inventory_item_drop.tscn — item kept in inventory.");
            return;
        }

        var instance = dropScene.Instantiate();
        if (instance is not InventoryItemDrop itemDrop)
        {
            instance?.Free();
            GD.PushError($"{nameof(Main)}: inventory_item_drop.tscn did not produce an InventoryItemDrop — item kept in inventory.");
            return;
        }

        // Preflight: resolve the spawn parent.
        var spawnParent = room.GetUnscaledEphemeralRoot();
        if (spawnParent == null)
        {
            itemDrop.Free();
            GD.PushError($"{nameof(Main)}: could not resolve unscaled ephemeral root — item kept in inventory.");
            return;
        }

        // Configure the drop node (still not in inventory at this point).
        itemDrop.ItemDefinition = entry.Definition;
        if (entry is InventoryGearEntry gearEntry)
        {
            // Preserve gear identity across drop/pickup so future rolls survive a world toss.
            itemDrop.GearInstance = gearEntry.Gear;
            itemDrop.Quantity = 1;
        }
        else
        {
            // Partial-stack drags carry an explicit amount; clamp to the live stack
            // so a stale UI selection can never spawn more than the source holds.
            itemDrop.Quantity = Math.Min(requestedAmount, entry.Quantity);
        }
        itemDrop.PickupMode = DropPickupMode.InteractOnly;

        // Compute spawn motion in the coordinate space of the ephemeral root's Node2D parent.
        if (spawnParent.GetParent() is Node2D spawnOriginNode)
        {
            var angle = (float)GD.RandRange(0.0, Mathf.Tau);
            var distance = (float)GD.RandRange(8.0, 20.0);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            var localOrigin = spawnOriginNode.ToLocal(_player.GlobalPosition);
            itemDrop.ConfigureSpawnMotion(localOrigin, localOrigin + offset);
        }

        // All preflight checks passed — remove from inventory now.
        if (entry is InventoryGearEntry)
        {
            var taken = _inventoryController.TakeEntry(slotIndex);
            if (taken == null)
            {
                // Slot was vacated between preflight and take; discard the pre-built drop node.
                itemDrop.Free();
                return;
            }
        }
        else
        {
            // Stack source: pull only the requested amount (or the whole stack when
            // the request meets/exceeds it). The controller handles emptying the slot.
            if (!_inventoryController.TryTakePartialStack(slotIndex, requestedAmount, out var takenStack) ||
                takenStack == null)
            {
                itemDrop.Free();
                return;
            }

            itemDrop.Quantity = takenStack.Quantity;
        }

        spawnParent.CallDeferred(Node.MethodName.AddChild, itemDrop);
    }
}
