using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class PlayerSpellBar : Control
{
    [Signal]
    public delegate void MenuRequestedEventHandler();

    private enum SlotKind
    {
        Spell,
        Food,
        Drink,
        Menu,
    }

    private sealed class ActionSlotView
    {
        public SlotKind Kind { get; init; }
        public StringName SlotAction { get; init; }
        public ConsumableKind ConsumableKind { get; init; }
        public Spell Spell { get; set; }
        public Control Root { get; init; }
        public ColorRect Frame { get; init; }
        public TextureRect Icon { get; init; }
        public Label ManaLabel { get; init; }
        public ColorRect ArmedOverlay { get; init; }
        public ColorRect ManaUnavailableOverlay { get; init; }
        public ColorRect CooldownOverlay { get; init; }
        public Label CooldownLabel { get; init; }
    }

    [Export]
    public NodePath SlotsPath { get; set; } = new NodePath("Slots");

    [Export]
    public Vector2 SlotSize { get; set; } = new Vector2(40.0f, 50.0f);

    [Export]
    public int SlotSeparation { get; set; } = 3;

    [Export]
    public float BottomMargin { get; set; } = 10.0f;

    private const float IconSize = 32.0f;

    private static readonly Color DefaultFrameColor = new(0.05f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BodyColor = new(0.18f, 0.21f, 0.26f, 0.96f);
    private static readonly Color ArmedFrameColor = new(0.92f, 0.68f, 0.22f, 1.0f);
    private static readonly Color ArmedOverlayColor = new(0.98f, 0.78f, 0.18f, 0.18f);
    private static readonly Color ManaUnavailableColor = new(0.16f, 0.04f, 0.06f, 0.38f);
    private static readonly Color CooldownColor = new(0.0f, 0.0f, 0.0f, 0.55f);
    private static readonly Color KeyLabelColor = new(0.95f, 0.84f, 0.48f, 1.0f);
    private static readonly Color ManaAvailableColor = new(0.55f, 0.78f, 1.0f, 1.0f);
    private static readonly Color ManaDepletedColor = new(1.0f, 0.34f, 0.34f, 1.0f);
    private static readonly Color MenuGlyphColor = new(0.86f, 0.88f, 0.94f, 1.0f);

    // Fixed display labels for the seven spell cast keys, in slot order (slots 1-7).
    private static readonly string[] SlotKeyLabels = { "Q", "E", "R", "T", "F", "C", "V" };

    private Player _player;
    private HBoxContainer _slots;
    private readonly List<ActionSlotView> _slotViews = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _slots = GetNodeOrNull<HBoxContainer>(SlotsPath);
        _slots?.AddThemeConstantOverride("separation", SlotSeparation);
        ApplyBarLayout();
    }

    public override void _Process(double delta)
    {
        foreach (var slotView in _slotViews)
        {
            switch (slotView.Kind)
            {
                case SlotKind.Spell:
                    RefreshSpellSlot(slotView);
                    UpdateArmedPlacementView(slotView);
                    UpdateManaAvailabilityView(slotView);
                    UpdateCooldownView(slotView);
                    break;
                case SlotKind.Food:
                case SlotKind.Drink:
                    RefreshConsumableSlot(slotView);
                    break;
            }
        }
    }

    public void Bind(Player player)
    {
        _player = player;
        ClearSlots();
        if (_slots == null || player == null || !GodotObject.IsInstanceValid(player))
        {
            Visible = false;
            ApplyBarLayout();
            return;
        }

        for (var slotIndex = 0; slotIndex < SpellLoadout.SlotActions.Length; slotIndex++)
            _slotViews.Add(CreateSpellSlot(SpellLoadout.SlotActions[slotIndex], slotIndex));

        _slotViews.Add(CreateConsumableSlot(ConsumableKind.Food));
        _slotViews.Add(CreateConsumableSlot(ConsumableKind.Drink));
        _slotViews.Add(CreateMenuSlot());

        Visible = _slotViews.Count > 0;
        ApplyBarLayout();

        foreach (var slotView in _slotViews)
        {
            if (slotView.Kind == SlotKind.Spell)
                RefreshSpellSlot(slotView);
            else if (slotView.Kind is SlotKind.Food or SlotKind.Drink)
                RefreshConsumableSlot(slotView);
        }
    }

    private Control CreateSlotRoot(string name)
    {
        var slotRoot = new Control
        {
            Name = name,
            CustomMinimumSize = SlotSize,
            Size = SlotSize,
            // Don't clip: the key label sits at the bottom edge, and clipping shaves off
            // glyph descenders (e.g. the tail of "Q", which then reads as "O").
            ClipContents = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _slots.AddChild(slotRoot);

        var frame = new ColorRect
        {
            Name = "Frame",
            Color = DefaultFrameColor,
            Size = SlotSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        slotRoot.AddChild(frame);

        var body = new ColorRect
        {
            Name = "Body",
            Color = BodyColor,
            Position = new Vector2(2.0f, 2.0f),
            Size = SlotSize - new Vector2(4.0f, 4.0f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        slotRoot.AddChild(body);

        return slotRoot;
    }

    private TextureRect CreateIconRect(Control slotRoot)
    {
        return SetUpIconRect(slotRoot, new TextureRect());
    }

    private TextureRect CreateSpellIconRect(Control slotRoot, StringName slotAction)
    {
        var icon = SetUpIconRect(slotRoot, new SpellTooltipIcon
        {
            SpellProvider = () => ResolveEquippedSpell(slotAction),
        });
        // Pass instead of Ignore: hovering the icon surfaces the spell tooltip while
        // unconsumed clicks keep falling through to world input (placement casts).
        icon.MouseFilter = MouseFilterEnum.Pass;
        return icon;
    }

    private T SetUpIconRect<T>(Control slotRoot, T icon) where T : TextureRect
    {
        icon.Name = "Icon";
        icon.Position = new Vector2((SlotSize.X - IconSize) * 0.5f, 2.0f);
        icon.Size = new Vector2(IconSize, IconSize);
        icon.CustomMinimumSize = new Vector2(IconSize, IconSize);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        icon.Visible = false;
        icon.MouseFilter = MouseFilterEnum.Ignore;
        slotRoot.AddChild(icon);
        return icon;
    }

    private ColorRect CreateOverlay(Control slotRoot, string name, Color color, Vector2 position, Vector2 size)
    {
        var overlay = new ColorRect
        {
            Name = name,
            Color = color,
            Position = position,
            Size = size,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        slotRoot.AddChild(overlay);
        return overlay;
    }

    private ActionSlotView CreateSpellSlot(StringName slotAction, int slotIndex)
    {
        var slotRoot = CreateSlotRoot($"{slotAction}Slot");
        var icon = CreateSpellIconRect(slotRoot, slotAction);

        var manaLabel = new Label
        {
            Name = "ManaCost",
            Position = new Vector2(2.0f, 0.0f),
            Size = new Vector2(SlotSize.X - 4.0f, 13.0f),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        manaLabel.AddThemeFontSizeOverride("font_size", 11);
        manaLabel.AddThemeColorOverride("font_color", ManaAvailableColor);
        manaLabel.AddThemeConstantOverride("outline_size", 3);
        manaLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.85f));
        slotRoot.AddChild(manaLabel);

        var keyLabel = new Label
        {
            Name = "Keybind",
            Text = slotIndex >= 0 && slotIndex < SlotKeyLabels.Length ? SlotKeyLabels[slotIndex] : string.Empty,
            Position = new Vector2(0.0f, SlotSize.Y - 16.0f),
            Size = new Vector2(SlotSize.X, 16.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        keyLabel.AddThemeFontSizeOverride("font_size", 14);
        keyLabel.AddThemeColorOverride("font_color", KeyLabelColor);
        slotRoot.AddChild(keyLabel);

        var armedOverlay = CreateOverlay(
            slotRoot, "ArmedOverlay", ArmedOverlayColor, new Vector2(2.0f, 2.0f), SlotSize - new Vector2(4.0f, 4.0f));
        var manaUnavailableOverlay = CreateOverlay(
            slotRoot, "ManaUnavailableOverlay", ManaUnavailableColor, Vector2.Zero, SlotSize);
        var cooldownOverlay = CreateOverlay(
            slotRoot, "CooldownOverlay", CooldownColor, Vector2.Zero, SlotSize);

        var cooldownLabel = new Label
        {
            Name = "CooldownText",
            Position = new Vector2(0.0f, (SlotSize.Y - 18.0f) * 0.5f),
            Size = new Vector2(SlotSize.X, 18.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        cooldownLabel.AddThemeFontSizeOverride("font_size", 15);
        cooldownLabel.AddThemeColorOverride("font_color", ManaDepletedColor);
        cooldownLabel.AddThemeConstantOverride("outline_size", 3);
        cooldownLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.85f));
        slotRoot.AddChild(cooldownLabel);

        return new ActionSlotView
        {
            Kind = SlotKind.Spell,
            SlotAction = slotAction,
            Root = slotRoot,
            Frame = GetFrame(slotRoot),
            Icon = icon,
            ManaLabel = manaLabel,
            ArmedOverlay = armedOverlay,
            ManaUnavailableOverlay = manaUnavailableOverlay,
            CooldownOverlay = cooldownOverlay,
            CooldownLabel = cooldownLabel,
        };
    }

    private ActionSlotView CreateConsumableSlot(ConsumableKind kind)
    {
        var slotRoot = CreateSlotRoot($"{kind}Slot");
        var icon = CreateIconRect(slotRoot);
        var button = AddClickButton(slotRoot, kind == ConsumableKind.Food ? OnFoodPressed : OnDrinkPressed);
        button.TooltipTextProvider = () => ResolveAssignedConsumable(kind)?.DisplayName ?? string.Empty;
        button.TooltipBuilder = () => BuildConsumableTooltip(kind);

        return new ActionSlotView
        {
            Kind = kind == ConsumableKind.Food ? SlotKind.Food : SlotKind.Drink,
            ConsumableKind = kind,
            Root = slotRoot,
            Frame = GetFrame(slotRoot),
            Icon = icon,
        };
    }

    private ActionSlotView CreateMenuSlot()
    {
        var slotRoot = CreateSlotRoot("MenuSlot");

        // Asset-free hamburger glyph drawn from three centered bars.
        var barWidth = IconSize * 0.625f;
        var barHeight = 3.0f;
        var barX = (SlotSize.X - barWidth) * 0.5f;
        var centerY = SlotSize.Y * 0.5f;
        foreach (var offsetY in new[] { -8.0f, 0.0f, 8.0f })
        {
            var bar = new ColorRect
            {
                Name = "MenuBar",
                Color = MenuGlyphColor,
                Position = new Vector2(barX, centerY + offsetY - (barHeight * 0.5f)),
                Size = new Vector2(barWidth, barHeight),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            slotRoot.AddChild(bar);
        }

        var button = AddClickButton(slotRoot, OnMenuPressed);
        button.TooltipText = "Game Menu";

        return new ActionSlotView
        {
            Kind = SlotKind.Menu,
            Root = slotRoot,
            Frame = GetFrame(slotRoot),
        };
    }

    private TooltipButton AddClickButton(Control slotRoot, Action onPressed)
    {
        var button = new TooltipButton
        {
            Name = "ClickArea",
            Flat = true,
            FocusMode = FocusModeEnum.None,
            Size = SlotSize,
            CustomMinimumSize = SlotSize,
            MouseFilter = MouseFilterEnum.Stop,
        };
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += onPressed;
        slotRoot.AddChild(button);
        return button;
    }

    private static ColorRect GetFrame(Control slotRoot)
    {
        return slotRoot.GetNodeOrNull<ColorRect>("Frame");
    }

    private void OnFoodPressed()
    {
        if (_player != null && GodotObject.IsInstanceValid(_player))
            _player.TryConsumeQuickAssignment(ConsumableKind.Food);
    }

    private void OnDrinkPressed()
    {
        if (_player != null && GodotObject.IsInstanceValid(_player))
            _player.TryConsumeQuickAssignment(ConsumableKind.Drink);
    }

    private void OnMenuPressed()
    {
        EmitSignal(SignalName.MenuRequested);
    }

    private void ClearSlots()
    {
        foreach (var slotView in _slotViews)
        {
            if (slotView.Root != null && GodotObject.IsInstanceValid(slotView.Root))
                slotView.Root.QueueFree();
        }

        _slotViews.Clear();
    }

    private void ApplyBarLayout()
    {
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 1.0f;
        AnchorBottom = 1.0f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Begin;

        if (_slots == null || _slotViews.Count == 0)
        {
            OffsetLeft = 0.0f;
            OffsetRight = 0.0f;
            OffsetTop = -BottomMargin;
            OffsetBottom = -BottomMargin;
            return;
        }

        var spacing = _slots.GetThemeConstant("separation");
        var totalWidth = (SlotSize.X * _slotViews.Count) + (spacing * Math.Max(0, _slotViews.Count - 1));
        var totalHeight = SlotSize.Y;
        OffsetLeft = -totalWidth * 0.5f;
        OffsetRight = totalWidth * 0.5f;
        OffsetBottom = -BottomMargin;
        OffsetTop = OffsetBottom - totalHeight;
    }

    private void RefreshSpellSlot(ActionSlotView slotView)
    {
        if (slotView == null || slotView.Icon == null || slotView.ManaLabel == null)
            return;

        slotView.Spell = ResolveEquippedSpell(slotView.SlotAction);
        var spell = slotView.Spell;
        if (spell == null || !GodotObject.IsInstanceValid(spell))
        {
            slotView.Icon.Texture = null;
            slotView.Icon.Visible = false;
            slotView.ManaLabel.Visible = false;
            return;
        }

        slotView.Icon.Texture = spell.Icon;
        slotView.Icon.Visible = spell.Icon != null;

        // Always show the cost, including 0, so every bound spell slot reads consistently.
        slotView.ManaLabel.Text = spell.DisplayManaCost.ToString();
        slotView.ManaLabel.Visible = true;
    }

    private void RefreshConsumableSlot(ActionSlotView slotView)
    {
        if (slotView?.Icon == null)
            return;

        var icon = ResolveConsumableIcon(slotView.ConsumableKind);
        slotView.Icon.Texture = icon;
        slotView.Icon.Visible = icon != null;
    }

    private Texture2D ResolveConsumableIcon(ConsumableKind kind)
    {
        return ResolveAssignedConsumable(kind)?.Icon;
    }

    private InventoryItemDefinition ResolveAssignedConsumable(ConsumableKind kind)
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return null;

        var loadout = _player.QuickConsumableLoadoutNode;
        if (loadout == null || !GodotObject.IsInstanceValid(loadout))
            return null;

        var assignedId = loadout.GetAssignedItemId(kind);
        if (string.IsNullOrEmpty(assignedId))
            return null;

        return _player.InventoryController?.ItemCatalog?.Resolve(assignedId, null);
    }

    private Control BuildConsumableTooltip(ConsumableKind kind)
    {
        var definition = ResolveAssignedConsumable(kind);
        if (definition == null)
            return null;

        var inventory = _player?.InventoryController;
        var quantity = inventory != null && GodotObject.IsInstanceValid(inventory)
            ? inventory.GetQuantityByItemId(definition.Id)
            : 0;
        return TooltipFactory.Build(definition, quantity, alwaysShowQuantity: true);
    }

    private Spell ResolveEquippedSpell(StringName slotAction)
    {
        if (_player == null ||
            !GodotObject.IsInstanceValid(_player) ||
            _player.SpellLoadoutNode == null ||
            !GodotObject.IsInstanceValid(_player.SpellLoadoutNode))
        {
            return null;
        }

        return _player.SpellLoadoutNode.GetEquippedSpell(slotAction);
    }

    private void UpdateManaAvailabilityView(ActionSlotView slotView)
    {
        if (_player == null ||
            !GodotObject.IsInstanceValid(_player) ||
            slotView.Spell == null ||
            !GodotObject.IsInstanceValid(slotView.Spell) ||
            slotView.ManaLabel == null ||
            slotView.ManaUnavailableOverlay == null)
        {
            if (slotView?.ManaUnavailableOverlay != null)
                slotView.ManaUnavailableOverlay.Visible = false;
            return;
        }

        var canAffordSpell = _player.CurrentMana >= Math.Max(0, slotView.Spell.DisplayManaCost);
        slotView.ManaLabel.AddThemeColorOverride(
            "font_color", canAffordSpell ? ManaAvailableColor : ManaDepletedColor);

        slotView.ManaUnavailableOverlay.Visible = !canAffordSpell;
        slotView.ManaUnavailableOverlay.Position = Vector2.Zero;
        slotView.ManaUnavailableOverlay.Size = ResolveSlotSize(slotView);
    }

    private void UpdateArmedPlacementView(ActionSlotView slotView)
    {
        if (_player == null ||
            !GodotObject.IsInstanceValid(_player) ||
            slotView.Spell == null ||
            !GodotObject.IsInstanceValid(slotView.Spell) ||
            slotView.Frame == null ||
            slotView.ArmedOverlay == null)
        {
            if (slotView?.Frame != null)
                slotView.Frame.Color = DefaultFrameColor;
            if (slotView?.ArmedOverlay != null)
                slotView.ArmedOverlay.Visible = false;
            return;
        }

        var isArmedPlacementSpell = ReferenceEquals(_player.ArmedPlacementSpell, slotView.Spell);
        slotView.Frame.Color = isArmedPlacementSpell ? ArmedFrameColor : DefaultFrameColor;
        slotView.ArmedOverlay.Visible = isArmedPlacementSpell;

        var slotSize = ResolveSlotSize(slotView);
        slotView.ArmedOverlay.Position = new Vector2(2.0f, 2.0f);
        slotView.ArmedOverlay.Size = slotSize - new Vector2(4.0f, 4.0f);
    }

    private void UpdateCooldownView(ActionSlotView slotView)
    {
        if (slotView.Spell == null ||
            !GodotObject.IsInstanceValid(slotView.Spell) ||
            slotView.CooldownOverlay == null ||
            slotView.CooldownLabel == null)
        {
            if (slotView?.CooldownOverlay != null)
                slotView.CooldownOverlay.Visible = false;
            if (slotView?.CooldownLabel != null)
                slotView.CooldownLabel.Visible = false;
            return;
        }

        var cooldownDuration = Math.Max(0.0f, slotView.Spell.CooldownDuration);
        var cooldownRemaining = Math.Max(0.0f, slotView.Spell.CooldownRemaining);
        if (cooldownDuration <= 0.0f || cooldownRemaining <= 0.0f)
        {
            slotView.CooldownOverlay.Visible = false;
            slotView.CooldownLabel.Visible = false;
            return;
        }

        var slotSize = ResolveSlotSize(slotView);
        var cooldownFraction = Mathf.Clamp(cooldownRemaining / cooldownDuration, 0.0f, 1.0f);
        slotView.CooldownOverlay.Visible = true;
        slotView.CooldownOverlay.Position = Vector2.Zero;
        slotView.CooldownOverlay.Size = new Vector2(slotSize.X, slotSize.Y * cooldownFraction);

        slotView.CooldownLabel.Visible = true;
        slotView.CooldownLabel.Text = cooldownRemaining >= 1.0f
            ? Mathf.CeilToInt(cooldownRemaining).ToString()
            : $"{cooldownRemaining:0.0}";
    }

    private Vector2 ResolveSlotSize(ActionSlotView slotView)
    {
        if (slotView?.Root == null)
            return SlotSize;

        var slotSize = slotView.Root.Size;
        return slotSize == Vector2.Zero ? SlotSize : slotSize;
    }
}
