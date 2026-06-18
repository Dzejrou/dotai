using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class MenuHubDebugRoomPage : Control
{
    private const string BuiltInContentLabel = "Built-in content";
    private const string EmptyContentLabel = "Empty";
    private const string NoRetainedInstanceText = "Retained instance: none";

    [Export]
    public DebugRoomCatalog Catalog { get; set; }

    [Export]
    public NodePath RoomSelectorPath { get; set; } = new("Margin/VBox/RoomRow/RoomSelector");

    [Export]
    public NodePath ContentSelectorPath { get; set; } = new("Margin/VBox/ContentRow/ContentSelector");

    [Export]
    public NodePath LevelSpinBoxPath { get; set; } = new("Margin/VBox/LevelRow/LevelSpinBox");

    [Export]
    public NodePath KeepInstanceTogglePath { get; set; } = new("Margin/VBox/KeepInstanceToggle");

    [Export]
    public NodePath EnterButtonPath { get; set; } = new("Margin/VBox/EnterButton");

    [Export]
    public NodePath RetainedLabelPath { get; set; } = new("Margin/VBox/RetainedLabel");

    [Export]
    public NodePath ReenterButtonPath { get; set; } = new("Margin/VBox/RetainedRow/ReenterButton");

    [Export]
    public NodePath FreeButtonPath { get; set; } = new("Margin/VBox/RetainedRow/FreeButton");

    [Export]
    public NodePath ReturnButtonPath { get; set; } = new("Margin/VBox/ReturnButton");

    private OptionButton _roomSelector;
    private OptionButton _contentSelector;
    private SpinBox _levelSpinBox;
    private int _selectedRoomLevel = 1;
    private BaseButton _keepInstanceToggle;
    private Button _enterButton;
    private Label _retainedLabel;
    private Button _reenterButton;
    private Button _freeButton;
    private Button _returnButton;

    private World _world;
    private Action _roomEntered;
    private readonly List<DebugRoomCatalogEntry> _roomEntries = new();
    private readonly List<RoomContentOption> _contentOptions = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _roomSelector = GetNodeOrNull<OptionButton>(RoomSelectorPath);
        _contentSelector = GetNodeOrNull<OptionButton>(ContentSelectorPath);
        _levelSpinBox = GetNodeOrNull<SpinBox>(LevelSpinBoxPath);
        _keepInstanceToggle = GetNodeOrNull<BaseButton>(KeepInstanceTogglePath);
        _enterButton = GetNodeOrNull<Button>(EnterButtonPath);
        _retainedLabel = GetNodeOrNull<Label>(RetainedLabelPath);
        _reenterButton = GetNodeOrNull<Button>(ReenterButtonPath);
        _freeButton = GetNodeOrNull<Button>(FreeButtonPath);
        _returnButton = GetNodeOrNull<Button>(ReturnButtonPath);

        if (_roomSelector != null)
            _roomSelector.ItemSelected += OnRoomSelected;

        // Mirrors the Debug Tray character-level SpinBox: integer 1-100, default 1. The
        // value is owned here and stays stable across page refreshes/room selection.
        if (_levelSpinBox != null)
        {
            _levelSpinBox.MinValue = 1;
            _levelSpinBox.MaxValue = 100;
            _levelSpinBox.Step = 1;
            _levelSpinBox.Rounded = true;
            _levelSpinBox.Value = _selectedRoomLevel;
            _levelSpinBox.ValueChanged += OnLevelChanged;
        }

        if (_enterButton != null)
            _enterButton.Pressed += OnEnterPressed;

        if (_reenterButton != null)
            _reenterButton.Pressed += OnReenterPressed;

        if (_freeButton != null)
            _freeButton.Pressed += OnFreePressed;

        if (_returnButton != null)
            _returnButton.Pressed += OnReturnPressed;

        RefreshAll();
    }

    public override void _ExitTree()
    {
        if (_roomSelector != null)
            _roomSelector.ItemSelected -= OnRoomSelected;

        if (_levelSpinBox != null)
            _levelSpinBox.ValueChanged -= OnLevelChanged;

        if (_enterButton != null)
            _enterButton.Pressed -= OnEnterPressed;

        if (_reenterButton != null)
            _reenterButton.Pressed -= OnReenterPressed;

        if (_freeButton != null)
            _freeButton.Pressed -= OnFreePressed;

        if (_returnButton != null)
            _returnButton.Pressed -= OnReturnPressed;
    }

    public void Bind(World world, Action roomEntered)
    {
        _world = world;
        _roomEntered = roomEntered;
        RefreshAll();
    }

    public void OnPageEntered()
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        RebuildRoomOptions();
        RefreshRetainedView();
        RefreshSessionView();
    }

    private void RebuildRoomOptions()
    {
        if (_roomSelector == null)
            return;

        var previousEntry = GetSelectedRoomEntry();

        _roomEntries.Clear();
        _roomSelector.Clear();

        if (Catalog?.Entries != null)
        {
            foreach (var entry in Catalog.Entries)
            {
                if (entry?.IsConfigured != true)
                    continue;

                _roomEntries.Add(entry);
                _roomSelector.AddItem(ResolveRoomName(entry.Definition));
            }
        }

        var hasRooms = _roomEntries.Count > 0;
        _roomSelector.Disabled = !hasRooms;
        if (_enterButton != null)
            _enterButton.Disabled = !hasRooms;

        if (hasRooms)
        {
            var restoredIndex = previousEntry != null ? _roomEntries.IndexOf(previousEntry) : -1;
            _roomSelector.Select(restoredIndex >= 0 ? restoredIndex : 0);
        }

        RebuildContentOptions();
    }

    private void RebuildContentOptions()
    {
        if (_contentSelector == null)
            return;

        _contentOptions.Clear();
        _contentSelector.Clear();

        var entry = GetSelectedRoomEntry();
        if (entry == null)
        {
            _contentSelector.Disabled = true;
            return;
        }

        if (entry.UsesBuiltInContent)
        {
            _contentSelector.AddItem(BuiltInContentLabel);
            _contentSelector.Select(0);
            _contentSelector.Disabled = true;
            return;
        }

        // Synthetic Empty option: enter the room with intentionally no content.
        _contentOptions.Add(null);
        _contentSelector.AddItem(EmptyContentLabel);

        if (entry.Definition.ContentOptions != null)
        {
            foreach (var option in entry.Definition.ContentOptions)
            {
                if (option?.ContentScene == null)
                    continue;

                _contentOptions.Add(option);
                _contentSelector.AddItem(ResolveContentName(option));
            }
        }

        _contentSelector.Select(0);
        _contentSelector.Disabled = false;
    }

    private void RefreshRetainedView()
    {
        var hasRetained = _world != null && GodotObject.IsInstanceValid(_world) && _world.HasRetainedDebugRoom;

        if (_retainedLabel != null)
        {
            _retainedLabel.Text = hasRetained
                ? $"Retained instance: {_world.RetainedDebugRoomLabel}"
                : NoRetainedInstanceText;
        }

        if (_reenterButton != null)
            _reenterButton.Disabled = !hasRetained;

        if (_freeButton != null)
            _freeButton.Disabled = !hasRetained;
    }

    private void RefreshSessionView()
    {
        if (_returnButton != null)
            _returnButton.Visible = _world != null && GodotObject.IsInstanceValid(_world) && _world.IsDebugRoomSessionActive;
    }

    private DebugRoomCatalogEntry GetSelectedRoomEntry()
    {
        if (_roomSelector == null)
            return null;

        var index = _roomSelector.Selected;
        return index >= 0 && index < _roomEntries.Count ? _roomEntries[index] : null;
    }

    private RoomContentOption GetSelectedContentOption()
    {
        if (_contentSelector == null)
            return null;

        var index = _contentSelector.Selected;
        return index >= 0 && index < _contentOptions.Count ? _contentOptions[index] : null;
    }

    private void OnRoomSelected(long index)
    {
        RebuildContentOptions();
    }

    private void OnLevelChanged(double value)
    {
        _selectedRoomLevel = Math.Max(1, (int)value);
    }

    private void OnEnterPressed()
    {
        if (_world == null || !GodotObject.IsInstanceValid(_world))
            return;

        var entry = GetSelectedRoomEntry();
        if (entry?.IsConfigured != true)
            return;

        var keepInstance = _keepInstanceToggle?.ButtonPressed == true;
        var entered = _world.TryEnterDebugRoom(
            entry.Definition,
            entry.UsesBuiltInContent ? null : GetSelectedContentOption(),
            useExternalContent: !entry.UsesBuiltInContent,
            keepInstance,
            _selectedRoomLevel);

        RefreshRetainedView();
        RefreshSessionView();

        if (entered)
            _roomEntered?.Invoke();
    }

    private void OnReenterPressed()
    {
        if (_world == null || !GodotObject.IsInstanceValid(_world))
            return;

        var entered = _world.TryReenterRetainedDebugRoom();

        RefreshRetainedView();
        RefreshSessionView();

        if (entered)
            _roomEntered?.Invoke();
    }

    private void OnFreePressed()
    {
        if (_world == null || !GodotObject.IsInstanceValid(_world))
            return;

        _world.FreeRetainedDebugRoom();
        RefreshRetainedView();
    }

    private void OnReturnPressed()
    {
        if (_world == null || !GodotObject.IsInstanceValid(_world))
            return;

        var returned = _world.TryReturnFromDebugRoom();

        RefreshRetainedView();
        RefreshSessionView();

        if (returned)
            _roomEntered?.Invoke();
    }

    private static string ResolveRoomName(RoomTemplateDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.DisplayName
            : definition.GetLabel();
    }

    private static string ResolveContentName(RoomContentOption option)
    {
        if (!string.IsNullOrWhiteSpace(option.DisplayName))
            return option.DisplayName;

        return option.Id != null && !option.Id.IsEmpty ? option.Id : "Unnamed content";
    }
}
