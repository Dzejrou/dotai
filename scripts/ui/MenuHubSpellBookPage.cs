using Godot;

using System.Collections.Generic;

[GlobalClass]
public partial class MenuHubSpellBookPage : Control
{
    private sealed class SpellButtonView
    {
        public Button Button { get; init; }
        public TextureRect Icon { get; init; }
        public Label Placeholder { get; init; }
        public Label NameLabel { get; init; }
        public Label BindLabel { get; init; }
        public Spell Template { get; init; }
    }

    private sealed class SlotButtonView
    {
        public Button Button { get; init; }
        public TextureRect Icon { get; init; }
        public Label Placeholder { get; init; }
        public Label KeyLabel { get; init; }
        public Label NameLabel { get; init; }
        public StringName SlotAction { get; init; }
    }

    [Export]
    public NodePath SelectionLabelPath { get; set; } = new("Margin/VBox/SelectionLabel");

    [Export]
    public NodePath SpellGridPath { get; set; } = new("Margin/VBox/SpellList/SpellGrid");

    [Export]
    public NodePath TestTogglePath { get; set; } = new("Margin/VBox/Header/TestToggle");

    [Export]
    public NodePath SlotGridPath { get; set; } = new("Margin/VBox/SlotSection/SlotGrid");

    [Export]
    public NodePath SaveButtonPath { get; set; } = new("Margin/VBox/Footer/SaveButton");

    private static readonly Vector2 IconHolderSize = new(48.0f, 48.0f);
    private static readonly Color SelectedTint = new(1.0f, 0.93f, 0.72f, 1.0f);
    private static readonly Color SpellNameColor = new(0.96f, 0.96f, 0.96f, 1.0f);
    private static readonly Color EmptyNameColor = new(0.72f, 0.72f, 0.72f, 0.9f);
    private static readonly Color KeyLabelColor = new(0.95f, 0.84f, 0.48f, 1.0f);
    private static readonly Color BindLabelColor = new(0.62f, 0.82f, 0.98f, 1.0f);
    private static readonly Color PlaceholderColor = new(0.6f, 0.62f, 0.7f, 0.9f);

    private Player _player;
    private Label _selectionLabel;
    private CheckButton _testToggle;
    private GridContainer _spellGrid;
    private GridContainer _slotGrid;
    private Button _saveButton;
    private readonly List<SpellButtonView> _spellButtonViews = new();
    private readonly List<SlotButtonView> _slotButtonViews = new();
    private Spell _selectedSpellTemplate;
    private bool _includeTestSpells;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _selectionLabel = GetNodeOrNull<Label>(SelectionLabelPath);
        _testToggle = GetNodeOrNull<CheckButton>(TestTogglePath);
        _spellGrid = GetNodeOrNull<GridContainer>(SpellGridPath);
        _slotGrid = GetNodeOrNull<GridContainer>(SlotGridPath);
        _saveButton = GetNodeOrNull<Button>(SaveButtonPath);

        if (_testToggle != null)
        {
            _testToggle.ButtonPressed = false;
            _testToggle.Toggled += OnTestToggleToggled;
        }

        if (_saveButton != null)
            _saveButton.Pressed += OnSavePressed;

        RefreshSelectionLabel();
        RebuildSlotButtons();
    }

    public override void _ExitTree()
    {
        if (_testToggle != null)
            _testToggle.Toggled -= OnTestToggleToggled;

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

    // Called by MenuHub when this page becomes the active one.
    public void OnPageEntered()
    {
        _selectedSpellTemplate = null;
        RebuildSpellButtons();
        RefreshSlotButtons();
        RefreshSelectionLabel();
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

        foreach (var view in _spellButtonViews)
        {
            if (GodotObject.IsInstanceValid(view.Button))
                view.Button.QueueFree();
        }

        _spellButtonViews.Clear();

        if (_player?.SpellBookNode == null)
            return;

        foreach (var spellTemplate in _player.GetBindableSpells(_includeTestSpells))
            _spellButtonViews.Add(CreateSpellButton(spellTemplate));

        RefreshSpellButtons();
    }

    private SpellButtonView CreateSpellButton(Spell spellTemplate)
    {
        var button = new TooltipButton
        {
            CustomMinimumSize = new Vector2(140.0f, 92.0f),
            TooltipTextProvider = () => ResolveSpellSafe(spellTemplate)?.DisplayLabel ?? string.Empty,
            TooltipBuilder = () => TooltipFactory.Build(ResolveSpellSafe(spellTemplate)),
        };

        var vbox = CreateCardLayout(button);
        var (iconHolder, icon, placeholder) = CreateIconHolder();
        vbox.AddChild(iconHolder);

        var nameLabel = CreateCenteredLabel(spellTemplate.DisplayLabel, 13, SpellNameColor);
        vbox.AddChild(nameLabel);

        var bindLabel = CreateCenteredLabel(string.Empty, 11, BindLabelColor);
        vbox.AddChild(bindLabel);

        button.Pressed += () => OnSpellTemplatePressed(spellTemplate);
        _spellGrid.AddChild(button);

        return new SpellButtonView
        {
            Button = button,
            Icon = icon,
            Placeholder = placeholder,
            NameLabel = nameLabel,
            BindLabel = bindLabel,
            Template = spellTemplate,
        };
    }

    private void OnTestToggleToggled(bool toggledOn)
    {
        _includeTestSpells = toggledOn;
        if (_selectedSpellTemplate != null && !IsCurrentSpellTemplateVisible(_selectedSpellTemplate))
            _selectedSpellTemplate = null;

        RebuildSpellButtons();
        RefreshSelectionLabel();
    }

    private void RebuildSlotButtons()
    {
        if (_slotGrid == null)
            return;

        foreach (var view in _slotButtonViews)
        {
            if (GodotObject.IsInstanceValid(view.Button))
                view.Button.QueueFree();
        }

        _slotButtonViews.Clear();

        foreach (var slotAction in SpellLoadout.SlotActions)
            _slotButtonViews.Add(CreateSlotButton(slotAction));

        RefreshSlotButtons();
    }

    private SlotButtonView CreateSlotButton(StringName slotAction)
    {
        var button = new TooltipButton
        {
            CustomMinimumSize = new Vector2(140.0f, 100.0f),
            TooltipTextProvider = () => ResolveEquippedSpell(slotAction)?.DisplayLabel ?? string.Empty,
            TooltipBuilder = () => TooltipFactory.Build(ResolveEquippedSpell(slotAction)),
        };

        var vbox = CreateCardLayout(button);

        var keyLabel = CreateCenteredLabel(ResolveActionLabel(slotAction), 16, KeyLabelColor);
        vbox.AddChild(keyLabel);

        var (iconHolder, icon, placeholder) = CreateIconHolder();
        vbox.AddChild(iconHolder);

        var nameLabel = CreateCenteredLabel("Empty", 12, EmptyNameColor);
        vbox.AddChild(nameLabel);

        var capturedSlotAction = slotAction;
        button.Pressed += () => OnSlotPressed(capturedSlotAction);
        _slotGrid.AddChild(button);

        return new SlotButtonView
        {
            Button = button,
            Icon = icon,
            Placeholder = placeholder,
            KeyLabel = keyLabel,
            NameLabel = nameLabel,
            SlotAction = slotAction,
        };
    }

    private static VBoxContainer CreateCardLayout(Button button)
    {
        var vbox = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        button.AddChild(vbox);
        return vbox;
    }

    private static (Control Holder, TextureRect Icon, Label Placeholder) CreateIconHolder()
    {
        var holder = new Control
        {
            CustomMinimumSize = IconHolderSize,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        var icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        holder.AddChild(icon);

        var placeholder = new Label
        {
            Text = "?",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        placeholder.AddThemeFontSizeOverride("font_size", 22);
        placeholder.AddThemeColorOverride("font_color", PlaceholderColor);
        placeholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        holder.AddChild(placeholder);

        return (holder, icon, placeholder);
    }

    private static Label CreateCenteredLabel(string text, int fontSize, Color color)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void RefreshSpellButtons()
    {
        foreach (var view in _spellButtonViews)
        {
            if (!GodotObject.IsInstanceValid(view.Button) || view.Template == null)
                continue;

            ApplyIcon(view.Icon, view.Placeholder, view.Template.Icon);

            view.BindLabel.Text =
                _player?.SpellLoadoutNode != null &&
                _player.SpellLoadoutNode.TryFindAssignedSlotAction(view.Template.SpellId, out var assignedSlot)
                    ? ResolveActionLabel(assignedSlot)
                    : string.Empty;
            view.BindLabel.Visible = !string.IsNullOrEmpty(view.BindLabel.Text);

            view.Button.Modulate = ReferenceEquals(_selectedSpellTemplate, view.Template)
                ? SelectedTint
                : Colors.White;
        }
    }

    private void RefreshSlotButtons()
    {
        foreach (var view in _slotButtonViews)
        {
            if (!GodotObject.IsInstanceValid(view.Button))
                continue;

            var equippedSpell = _player?.SpellLoadoutNode?.GetEquippedSpell(view.SlotAction);
            if (equippedSpell == null || !GodotObject.IsInstanceValid(equippedSpell))
            {
                view.Icon.Texture = null;
                view.Icon.Visible = false;
                view.Placeholder.Visible = false;
                view.NameLabel.Text = "Empty";
                view.NameLabel.AddThemeColorOverride("font_color", EmptyNameColor);
            }
            else
            {
                ApplyIcon(view.Icon, view.Placeholder, equippedSpell.Icon);
                view.NameLabel.Text = equippedSpell.DisplayLabel;
                view.NameLabel.AddThemeColorOverride("font_color", SpellNameColor);
            }

            view.Button.Disabled = _player?.SpellLoadoutNode == null;
        }

        if (_saveButton != null)
            _saveButton.Disabled = _player?.SpellLoadoutNode == null;
    }

    private static void ApplyIcon(TextureRect icon, Label placeholder, Texture2D texture)
    {
        var hasIcon = texture != null;
        icon.Texture = texture;
        icon.Visible = hasIcon;
        placeholder.Visible = !hasIcon;
    }

    private static Spell ResolveSpellSafe(Spell spell)
    {
        return spell != null && GodotObject.IsInstanceValid(spell) ? spell : null;
    }

    private Spell ResolveEquippedSpell(StringName slotAction)
    {
        return ResolveSpellSafe(_player?.SpellLoadoutNode?.GetEquippedSpell(slotAction));
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

    private bool IsCurrentSpellTemplateVisible(Spell spellTemplate)
    {
        if (_player == null || spellTemplate == null)
            return false;

        foreach (var visibleSpell in _player.GetBindableSpells(_includeTestSpells))
        {
            if (ReferenceEquals(visibleSpell, spellTemplate))
                return true;
        }

        return false;
    }
}
