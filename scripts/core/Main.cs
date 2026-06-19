using Godot;

using System;

[GlobalClass]
public partial class Main : Node2D
{
    [Export]
    public NodePath WorldPath { get; set; } = new NodePath("World");

    [Export]
    public NodePath GameOverPath { get; set; } = new NodePath("GameOver/Root");

    [Export]
    public NodePath MenuHubPath { get; set; } = new NodePath("MenuHub/Root");

    [Export]
    public NodePath DebugTrayPath { get; set; } = new NodePath("DebugTray/Root");

    private World _world;
    private Player _player;
    private InventoryController _inventoryController;
    private Control _gameOverRoot;
    private MenuHub _menuHubRoot;
    private DebugTray _debugTrayRoot;
    private bool _gameOverActive;
    private bool _restartingFromGameOver;
    private bool _menuHubOpen;
    private CastBar _castBar;
    private PlayerSpellBar _spellBar;
    private PlayerDebugStatsWindow _playerDebugStatsWindow;
    private MerchantWindow _merchantWindow;
    private Sprite2D _interactionPrompt;
    private const string CastBarScenePath = "res://scenes/ui/cast_bar.tscn";
    private const string PlayerSpellBarScenePath = "res://scenes/ui/player_spell_bar.tscn";
    private const string PlayerDebugStatsWindowScenePath = "res://scenes/ui/player_debug_stats_window.tscn";
    private const string MerchantWindowScenePath = "res://scenes/ui/merchant_window.tscn";
    private const string CountdownHudScenePath = "res://scenes/ui/countdown_hud.tscn";
    private const string CombatLogPanelScenePath = "res://scenes/ui/combat_log_panel.tscn";
    private const string InteractionPromptGlyphPath = "res://assets/glyphs/letter_g.png";
    private const string SpellBookActionName = "spell_book";
    private const string ToggleInventoryActionName = "toggle_inventory";
    private const string ToggleCharacterWindowActionName = "toggle_character_window";
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
            _world.Connect(World.SignalName.DungeonEntranceInteractionRequested, new Callable(this, nameof(OnDungeonEntranceInteractionRequested)));
        }

        _gameOverRoot = GetNodeOrNull<Control>(GameOverPath);
        _menuHubRoot = GetNodeOrNull<MenuHub>(MenuHubPath);
        _debugTrayRoot = GetNodeOrNull<DebugTray>(DebugTrayPath);
        CreateHud();

        if (_gameOverRoot != null)
        {
            _gameOverRoot.Visible = false;
            _gameOverRoot.ProcessMode = ProcessModeEnum.Always;
        }

        if (_menuHubRoot != null)
        {
            _menuHubRoot.Visible = false;
            _menuHubRoot.ProcessMode = ProcessModeEnum.Always;
            _menuHubRoot.Connect(MenuHub.SignalName.ResumeRequested, new Callable(this, nameof(OnMenuHubResumeRequested)));
            _menuHubRoot.Connect(MenuHub.SignalName.DebugRequested, new Callable(this, nameof(OnMenuHubDebugRequested)));
            _menuHubRoot.Connect(MenuHub.SignalName.SaveRequested, new Callable(this, nameof(OnMenuHubSaveRequested)));
            _menuHubRoot.Connect(MenuHub.SignalName.LoadRequested, new Callable(this, nameof(OnMenuHubLoadRequested)));
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
            _playerDebugStatsWindow?.Bind(player);
            UpdateInteractionPrompt(player.HasInteractionTarget);
        }
        else
        {
            _castBar?.HideCast();
            _spellBar?.Bind(null);
            _playerDebugStatsWindow?.Bind(null);
            UpdateInteractionPrompt(false);
        }

        _inventoryController = _world != null && !_world.InventoryPath.IsEmpty
            ? _world.ResolveInventoryController()
            : null;
        var equipmentController = _player?.EquipmentControllerNode;

        if (_menuHubRoot != null)
        {
            _menuHubRoot.BindInventoryPage(_player, _inventoryController, equipmentController);
            _menuHubRoot.SetInventoryPageWorldDropHandlers(OnInventoryItemDroppedToWorld, OnGearDroppedToWorld);
            _menuHubRoot.BindCharacterPage(_player, equipmentController);
            _menuHubRoot.BindSpellBookPage(_player);
            _menuHubRoot.BindDebugRoomPage(_world, CloseMenuHub);
            _menuHubRoot.BindDungeonPage(_world, CloseMenuHub, TryStartDungeonRunFromHub);
        }

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

        if (GodotObject.IsInstanceValid(_world) &&
            _world.IsConnected(World.SignalName.DungeonEntranceInteractionRequested, new Callable(this, nameof(OnDungeonEntranceInteractionRequested))))
            _world.Disconnect(World.SignalName.DungeonEntranceInteractionRequested, new Callable(this, nameof(OnDungeonEntranceInteractionRequested)));

        if (GodotObject.IsInstanceValid(_player) &&
            _player.IsConnected(Player.SignalName.InteractionAvailabilityChanged, new Callable(this, nameof(OnPlayerInteractionAvailabilityChanged))))
            _player.Disconnect(Player.SignalName.InteractionAvailabilityChanged, new Callable(this, nameof(OnPlayerInteractionAvailabilityChanged)));

        if (GodotObject.IsInstanceValid(_menuHubRoot) &&
            _menuHubRoot.IsConnected(MenuHub.SignalName.ResumeRequested, new Callable(this, nameof(OnMenuHubResumeRequested))))
            _menuHubRoot.Disconnect(MenuHub.SignalName.ResumeRequested, new Callable(this, nameof(OnMenuHubResumeRequested)));

        if (GodotObject.IsInstanceValid(_menuHubRoot) &&
            _menuHubRoot.IsConnected(MenuHub.SignalName.DebugRequested, new Callable(this, nameof(OnMenuHubDebugRequested))))
            _menuHubRoot.Disconnect(MenuHub.SignalName.DebugRequested, new Callable(this, nameof(OnMenuHubDebugRequested)));

        if (GodotObject.IsInstanceValid(_menuHubRoot) &&
            _menuHubRoot.IsConnected(MenuHub.SignalName.SaveRequested, new Callable(this, nameof(OnMenuHubSaveRequested))))
            _menuHubRoot.Disconnect(MenuHub.SignalName.SaveRequested, new Callable(this, nameof(OnMenuHubSaveRequested)));

        if (GodotObject.IsInstanceValid(_menuHubRoot) &&
            _menuHubRoot.IsConnected(MenuHub.SignalName.LoadRequested, new Callable(this, nameof(OnMenuHubLoadRequested))))
            _menuHubRoot.Disconnect(MenuHub.SignalName.LoadRequested, new Callable(this, nameof(OnMenuHubLoadRequested)));

        if (GodotObject.IsInstanceValid(_debugTrayRoot) &&
            _debugTrayRoot.IsConnected(DebugTray.SignalName.PlayerStatsRequested, new Callable(this, nameof(OnDebugTrayPlayerStatsRequested))))
            _debugTrayRoot.Disconnect(DebugTray.SignalName.PlayerStatsRequested, new Callable(this, nameof(OnDebugTrayPlayerStatsRequested)));

        if (GodotObject.IsInstanceValid(_spellBar) &&
            _spellBar.IsConnected(PlayerSpellBar.SignalName.MenuRequested, new Callable(this, nameof(OnSpellBarMenuRequested))))
            _spellBar.Disconnect(PlayerSpellBar.SignalName.MenuRequested, new Callable(this, nameof(OnSpellBarMenuRequested)));
    }

    public override void _Input(InputEvent @event)
    {
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

        TryHandleMenuHubInput(@event);
    }

    public override void _Process(double delta)
    {
        UpdateInteractionPromptPosition();
    }

    private void OnPlayerDied()
    {
        if (_gameOverActive)
            return;

        CloseMenuHub();
        CloseDebugTray(false);
        UpdateInteractionPrompt(false);
        _gameOverActive = true;
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

        _merchantWindow.Open(_inventoryController, stock);
    }

    private void OnDungeonEntranceInteractionRequested(Player player)
    {
        if (_gameOverActive)
            return;

        // Interaction-driven entry: open the HUB straight on the Dungeon page and authorize Start
        // for this HUB session. No run is started here.
        OpenMenuHub(MenuHubPage.Dungeon);
        _menuHubRoot?.GrantDungeonEntranceAuthorization();
    }

    // Bridges the Dungeon page Start request to World. Returns null on success, or an actionable
    // error string the page shows while the HUB stays open. On success the single-use entrance
    // authorization is consumed and the HUB is closed/unpaused only after the run actually starts.
    private string TryStartDungeonRunFromHub(ulong seed, int ordinaryRoomCount, int startingRoomLevel)
    {
        if (_world == null || !GodotObject.IsInstanceValid(_world))
            return "Dungeon runtime is unavailable.";

        if (!_world.TryStartDungeonRun(seed, ordinaryRoomCount, startingRoomLevel, out var error))
            return string.IsNullOrEmpty(error) ? "Failed to start the dungeon run." : error;

        _menuHubRoot?.ConsumeDungeonEntranceAuthorization();
        CloseMenuHub();
        return null;
    }

    private void RestartFromGameOver()
    {
        _restartingFromGameOver = true;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void OnSpellBarMenuRequested()
    {
        if (_menuHubOpen)
            return;

        OpenMenuHub(MenuHubPage.GameMenu);
    }

    private void OnMenuHubResumeRequested()
    {
        CloseMenuHub();
    }

    private void OnMenuHubDebugRequested()
    {
        CloseMenuHub();
        OpenDebugTray();
    }

    private void OnMenuHubSaveRequested()
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

    private void OnMenuHubLoadRequested()
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
            _player.ApplyLoadedQuickConsumables(data.Player.QuickFoodItemId, data.Player.QuickDrinkItemId);
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
            _spellBar.Connect(PlayerSpellBar.SignalName.MenuRequested, new Callable(this, nameof(OnSpellBarMenuRequested)));
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
            hudCanvas.AddChild(combatLogPanel);
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

    private bool TryHandleMenuHubInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (keyEvent.PhysicalKeycode == Key.P)
        {
            if (_debugTrayRoot != null && _debugTrayRoot.TrayVisible)
                CloseDebugTray();
            else
            {
                CloseMenuHub();
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
            OpenMenuHub(MenuHubPage.GameMenu);
            return true;
        }

        if (_menuHubOpen)
            CloseMenuHub();
        else
            OpenMenuHub(ResolveEscapeMenuPage());

        return true;
    }

    // Inside an active dungeon run Esc lands on the Dungeon page; otherwise it opens the Game
    // Menu as before. Spell-cancel keeps priority because it is handled earlier in this method.
    private MenuHubPage ResolveEscapeMenuPage()
    {
        return _world != null && GodotObject.IsInstanceValid(_world) && _world.HasActiveDungeonRun
            ? MenuHubPage.Dungeon
            : MenuHubPage.GameMenu;
    }

    private bool TryHandleSpellBookInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (!InputMap.HasAction(SpellBookActionName) || !@event.IsActionPressed(SpellBookActionName))
            return false;

        if (_debugTrayRoot != null && _debugTrayRoot.TrayVisible)
            return true;

        if (_menuHubOpen)
        {
            if (_menuHubRoot != null && _menuHubRoot.CurrentPage == MenuHubPage.SpellBook)
                CloseMenuHub();
            else
                _menuHubRoot?.SwitchTo(MenuHubPage.SpellBook);
            return true;
        }

        OpenMenuHub(MenuHubPage.SpellBook);
        return true;
    }

    private bool TryHandleInventoryInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        if (!InputMap.HasAction(ToggleInventoryActionName) || !@event.IsActionPressed(ToggleInventoryActionName))
            return false;

        if (_debugTrayRoot != null && _debugTrayRoot.TrayVisible)
            return true;

        if (_menuHubOpen)
        {
            if (_menuHubRoot != null && _menuHubRoot.CurrentPage == MenuHubPage.Inventory)
                CloseMenuHub();
            else
                _menuHubRoot?.SwitchTo(MenuHubPage.Inventory);
            return true;
        }

        OpenMenuHub(MenuHubPage.Inventory);
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

        if (_debugTrayRoot != null && _debugTrayRoot.TrayVisible)
            return true;

        if (_menuHubOpen)
        {
            if (_menuHubRoot != null && _menuHubRoot.CurrentPage == MenuHubPage.Character)
                CloseMenuHub();
            else
                _menuHubRoot?.SwitchTo(MenuHubPage.Character);
            return true;
        }

        OpenMenuHub(MenuHubPage.Character);
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

    private void OpenMenuHub(MenuHubPage page = MenuHubPage.GameMenu)
    {
        CloseDebugTray();
        _merchantWindow?.CloseWindow();
        _menuHubOpen = true;
        if (_menuHubRoot != null)
            _menuHubRoot.Open(page);

        GetTree().Paused = true;
    }

    private void CloseMenuHub()
    {
        _menuHubOpen = false;
        if (_menuHubRoot != null)
            _menuHubRoot.Close();

        if (!_gameOverActive)
            GetTree().Paused = false;
    }

    private void OpenDebugTray()
    {
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

        if (!TryBuildWorldDropNode(entry.Definition, out var itemDrop, out var spawnParent))
            return;

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

    private void OnGearDroppedToWorld(GearInstance gear)
    {
        if (gear?.Definition == null)
            return;

        if (!TryBuildWorldDropNode(gear.Definition, out var itemDrop, out var spawnParent))
        {
            // Roll back: page already unequipped the gear, so put it back in inventory
            // if at all possible to avoid losing it.
            if (_inventoryController != null && GodotObject.IsInstanceValid(_inventoryController))
                _inventoryController.AddGear(gear);
            return;
        }

        itemDrop.GearInstance = gear;
        itemDrop.Quantity = 1;
        spawnParent.CallDeferred(Node.MethodName.AddChild, itemDrop);
    }

    private bool TryBuildWorldDropNode(InventoryItemDefinition definition, out InventoryItemDrop itemDrop, out Node spawnParent)
    {
        itemDrop = null;
        spawnParent = null;

        // Preflight: require an active room and living player to receive the drop.
        var room = _world?.ActiveRoom;
        if (room == null || !GodotObject.IsInstanceValid(room))
            return false;

        if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsInsideTree())
            return false;

        var dropScene = ResourceLoader.Load<PackedScene>("res://scenes/world/drops/inventory_item_drop.tscn");
        if (dropScene == null)
        {
            GD.PushError($"{nameof(Main)}: failed to load inventory_item_drop.tscn.");
            return false;
        }

        var instance = dropScene.Instantiate();
        if (instance is not InventoryItemDrop drop)
        {
            instance?.Free();
            GD.PushError($"{nameof(Main)}: inventory_item_drop.tscn did not produce an InventoryItemDrop.");
            return false;
        }

        var parent = room.GetUnscaledEphemeralRoot();
        if (parent == null)
        {
            drop.Free();
            GD.PushError($"{nameof(Main)}: could not resolve unscaled ephemeral root.");
            return false;
        }

        drop.ItemDefinition = definition;
        drop.PickupMode = DropPickupMode.InteractOnly;

        // Compute spawn motion in the coordinate space of the ephemeral root's Node2D parent.
        if (parent.GetParent() is Node2D spawnOriginNode)
        {
            var angle = (float)GD.RandRange(0.0, Mathf.Tau);
            var distance = (float)GD.RandRange(8.0, 20.0);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            var localOrigin = spawnOriginNode.ToLocal(_player.GlobalPosition);
            drop.ConfigureSpawnMotion(localOrigin, localOrigin + offset);
        }

        itemDrop = drop;
        spawnParent = parent;
        return true;
    }
}
