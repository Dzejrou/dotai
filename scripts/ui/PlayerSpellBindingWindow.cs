using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class PlayerSpellBindingWindow : Control
{
    [Export]
    public NodePath TitleLabelPath { get; set; } = new("Center/Panel/Margin/VBox/Header/Title");

    [Export]
    public NodePath CloseButtonPath { get; set; } = new("Center/Panel/Margin/VBox/Header/CloseButton");

    [Export]
    public NodePath SelectionLabelPath { get; set; } = new("Center/Panel/Margin/VBox/SelectionLabel");

    [Export]
    public NodePath SpellGridPath { get; set; } = new("Center/Panel/Margin/VBox/SpellList/SpellGrid");

    [Export]
    public NodePath SlotGridPath { get; set; } = new("Center/Panel/Margin/VBox/SlotSection/SlotGrid");

    [Export]
    public NodePath SaveButtonPath { get; set; } = new("Center/Panel/Margin/VBox/Footer/SaveButton");

    private Player _player;
    private Label _titleLabel;
    private Button _closeButton;
    private Label _selectionLabel;
    private GridContainer _spellGrid;
    private GridContainer _slotGrid;
    private Button _saveButton;
    private readonly Dictionary<Button, Spell> _spellButtons = new();
    private readonly Dictionary<Button, StringName> _slotButtons = new();
    private Spell _selectedSpellTemplate;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
        _closeButton = GetNodeOrNull<Button>(CloseButtonPath);
        _selectionLabel = GetNodeOrNull<Label>(SelectionLabelPath);
        _spellGrid = GetNodeOrNull<GridContainer>(SpellGridPath);
        _slotGrid = GetNodeOrNull<GridContainer>(SlotGridPath);
        _saveButton = GetNodeOrNull<Button>(SaveButtonPath);

        if (_closeButton != null)
            _closeButton.Pressed += CloseWindow;

        if (_saveButton != null)
            _saveButton.Pressed += OnSavePressed;

        RefreshSelectionLabel();
        RebuildSlotButtons();
    }

    public override void _ExitTree()
    {
        if (_closeButton != null)
            _closeButton.Pressed -= CloseWindow;

        if (_saveButton != null)
            _saveButton.Pressed -= OnSavePressed;

        UnbindCurrentPlayer();
    }

    public void Bind(Player player)
    {
        if (ReferenceEquals(_player, player))
        {
            RefreshSpellButtons();
            RefreshSlotButtons();
            return;
        }

        UnbindCurrentPlayer();
        _player = player;

        if (_player != null &&
            GodotObject.IsInstanceValid(_player) &&
            !_player.IsConnected(Player.SignalName.SpellLoadoutChanged, new Callable(this, nameof(OnPlayerSpellLoadoutChanged))))
        {
            _player.Connect(Player.SignalName.SpellLoadoutChanged, new Callable(this, nameof(OnPlayerSpellLoadoutChanged)));
        }

        _selectedSpellTemplate = null;
        RebuildSpellButtons();
        RefreshSlotButtons();
        RefreshSelectionLabel();
    }

    public void ToggleWindow()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        if (Visible)
        {
            CloseWindow();
            return;
        }

        Visible = true;
        _selectedSpellTemplate = null;
        RefreshSpellButtons();
        RefreshSlotButtons();
        RefreshSelectionLabel();
    }

    public void CloseWindow()
    {
        Visible = false;
        _selectedSpellTemplate = null;
        RefreshSelectionLabel();
        RefreshSpellButtons();
    }

    private void OnPlayerSpellLoadoutChanged()
    {
        RefreshSpellButtons();
        RefreshSlotButtons();
    }

    private void RebuildSpellButtons()
    {
        if (_spellGrid == null)
            return;

        foreach (var pair in _spellButtons)
        {
            if (GodotObject.IsInstanceValid(pair.Key))
                pair.Key.QueueFree();
        }

        _spellButtons.Clear();

        if (_player?.SpellBookNode == null)
            return;

        foreach (var spellTemplate in _player.SpellBookNode.GetSpellTemplates())
        {
            var spellButton = new Button
            {
                CustomMinimumSize = new Vector2(132.0f, 56.0f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ClipText = false,
            };
            spellButton.Pressed += () => OnSpellTemplatePressed(spellTemplate);
            _spellGrid.AddChild(spellButton);
            _spellButtons[spellButton] = spellTemplate;
        }

        RefreshSpellButtons();
    }

    private void RebuildSlotButtons()
    {
        if (_slotGrid == null)
            return;

        foreach (var pair in _slotButtons)
        {
            if (GodotObject.IsInstanceValid(pair.Key))
                pair.Key.QueueFree();
        }

        _slotButtons.Clear();

        foreach (var slotAction in SpellLoadout.SlotActions)
        {
            var slotButton = new Button
            {
                CustomMinimumSize = new Vector2(116.0f, 60.0f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            var capturedSlotAction = slotAction;
            slotButton.Pressed += () => OnSlotPressed(capturedSlotAction);
            _slotGrid.AddChild(slotButton);
            _slotButtons[slotButton] = slotAction;
        }

        RefreshSlotButtons();
    }

    private void RefreshSpellButtons()
    {
        foreach (var pair in _spellButtons)
        {
            var button = pair.Key;
            var spellTemplate = pair.Value;
            if (!GodotObject.IsInstanceValid(button) || spellTemplate == null)
                continue;

            button.Text = BuildSpellButtonText(spellTemplate);
            button.Modulate = ReferenceEquals(_selectedSpellTemplate, spellTemplate)
                ? new Color(1.0f, 0.93f, 0.72f, 1.0f)
                : Colors.White;
        }
    }

    private void RefreshSlotButtons()
    {
        foreach (var pair in _slotButtons)
        {
            var button = pair.Key;
            var slotAction = pair.Value;
            if (!GodotObject.IsInstanceValid(button))
                continue;

            button.Text = BuildSlotButtonText(slotAction);
            button.Disabled = _player?.SpellLoadoutNode == null;
        }

        if (_saveButton != null)
            _saveButton.Disabled = _player?.SpellLoadoutNode == null;
    }

    private string BuildSpellButtonText(Spell spellTemplate)
    {
        if (_player?.SpellLoadoutNode != null &&
            _player.SpellLoadoutNode.TryFindAssignedSlotAction(spellTemplate.SpellId, out var assignedSlot))
        {
            return $"{spellTemplate.DisplayLabel}\n{ResolveActionLabel(assignedSlot)}";
        }

        return spellTemplate.DisplayLabel;
    }

    private string BuildSlotButtonText(StringName slotAction)
    {
        var keybindLabel = ResolveActionLabel(slotAction);
        var equippedSpell = _player?.SpellLoadoutNode?.GetEquippedSpell(slotAction);
        if (equippedSpell == null || !GodotObject.IsInstanceValid(equippedSpell))
            return $"{keybindLabel}\nEmpty";

        return $"{keybindLabel}\n{equippedSpell.DisplayLabel}";
    }

    private void OnSpellTemplatePressed(Spell spellTemplate)
    {
        _selectedSpellTemplate = spellTemplate;
        RefreshSelectionLabel();
        RefreshSpellButtons();
    }

    private void OnSlotPressed(StringName slotAction)
    {
        if (_selectedSpellTemplate == null ||
            _player?.SpellLoadoutNode == null ||
            !GodotObject.IsInstanceValid(_player.SpellLoadoutNode))
        {
            return;
        }

        _player.SpellLoadoutNode.AssignSpell(_selectedSpellTemplate, slotAction);
        RefreshSlotButtons();
        RefreshSpellButtons();
        RefreshSelectionLabel();
    }

    private void OnSavePressed()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        if (_player.TrySaveSpellLoadoutConfiguration(out var message))
        {
            GD.Print(message);
            _player.ShowFloatingText("Spell loadout saved", new Color(0.62f, 0.95f, 0.72f, 1.0f));
            return;
        }

        GD.PushWarning(message);
        _player.ShowFloatingText("Spell loadout save failed", new Color(1.0f, 0.62f, 0.62f, 1.0f));
    }

    private void RefreshSelectionLabel()
    {
        if (_selectionLabel == null)
            return;

        if (_selectedSpellTemplate == null)
        {
            _selectionLabel.Text = "Select a spell, then click a slot to bind it.";
            return;
        }

        _selectionLabel.Text = $"Selected: {_selectedSpellTemplate.DisplayLabel}";
    }

    private static string ResolveActionLabel(StringName action)
    {
        foreach (var inputEvent in InputMap.ActionGetEvents(action))
        {
            if (inputEvent is not InputEventKey keyEvent)
                continue;

            var keycode = keyEvent.PhysicalKeycode != Key.None
                ? keyEvent.PhysicalKeycode
                : keyEvent.Keycode;
            if (keycode != Key.None)
                return OS.GetKeycodeString(keycode).ToUpperInvariant();
        }

        return action.ToString();
    }

    private void UnbindCurrentPlayer()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            _player = null;
            return;
        }

        var callable = new Callable(this, nameof(OnPlayerSpellLoadoutChanged));
        if (_player.IsConnected(Player.SignalName.SpellLoadoutChanged, callable))
            _player.Disconnect(Player.SignalName.SpellLoadoutChanged, callable);

        _player = null;
    }
}
