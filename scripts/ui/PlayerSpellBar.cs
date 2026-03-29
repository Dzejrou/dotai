using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class PlayerSpellBar : Control
{
    private sealed class SpellSlotView
    {
        public Spell Spell { get; init; }
        public Control Root { get; init; }
        public ColorRect Frame { get; init; }
        public Label ManaLabel { get; init; }
        public ColorRect ArmedOverlay { get; init; }
        public ColorRect ManaUnavailableOverlay { get; init; }
        public ColorRect Overlay { get; init; }
        public Label CooldownLabel { get; init; }
    }

    [Export]
    public NodePath SlotsPath { get; set; } = new NodePath("Slots");

    [Export]
    public Vector2 SlotSize { get; set; } = new Vector2(120.0f, 58.0f);

    [Export]
    public Vector2 ScreenMargin { get; set; } = new Vector2(12.0f, 12.0f);

    private static readonly Color DefaultFrameColor = new(0.05f, 0.06f, 0.08f, 0.95f);
    private static readonly Color ArmedFrameColor = new(0.92f, 0.68f, 0.22f, 1.0f);
    private static readonly Color ArmedOverlayColor = new(0.98f, 0.78f, 0.18f, 0.18f);

    private Player _player;
    private HBoxContainer _slots;
    private readonly List<SpellSlotView> _slotViews = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _slots = GetNodeOrNull<HBoxContainer>(SlotsPath);
        ApplyBarLayout();
    }

    public override void _Process(double delta)
    {
        foreach (var slotView in _slotViews)
        {
            UpdateArmedPlacementView(slotView);
            UpdateManaAvailabilityView(slotView);
            UpdateCooldownView(slotView);
        }
    }

    public void Bind(Player player)
    {
        _player = player;
        ClearSlots();
        if (_slots == null || player == null)
        {
            Visible = false;
            ApplyBarLayout();
            return;
        }

        var spellsNode = player.GetNodeOrNull<Node>("Spells");
        if (spellsNode == null)
        {
            Visible = false;
            ApplyBarLayout();
            return;
        }

        foreach (Node child in spellsNode.GetChildren())
        {
            if (child is not Spell spell)
                continue;

            _slotViews.Add(CreateSpellSlot(spell));
        }

        Visible = _slotViews.Count > 0;
        ApplyBarLayout();

        foreach (var slotView in _slotViews)
            UpdateCooldownView(slotView);
    }

    private SpellSlotView CreateSpellSlot(Spell spell)
    {
        var slotRoot = new Control
        {
            Name = $"{spell.Name}Slot",
            CustomMinimumSize = SlotSize,
            Size = SlotSize,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _slots.AddChild(slotRoot);

        var frame = new ColorRect
        {
            Name = "Frame",
            Color = DefaultFrameColor,
            Size = SlotSize,
        };
        slotRoot.AddChild(frame);

        var body = new ColorRect
        {
            Name = "Body",
            Color = new Color(0.18f, 0.21f, 0.26f, 0.96f),
            Position = new Vector2(2.0f, 2.0f),
            Size = SlotSize - new Vector2(4.0f, 4.0f),
        };
        slotRoot.AddChild(body);

        var keybindLabel = new Label
        {
            Name = "Keybind",
            Text = ResolveActionLabel(spell.CastAction),
            Position = new Vector2(8.0f, 4.0f),
            Size = new Vector2(SlotSize.X - 16.0f, 14.0f),
        };
        keybindLabel.AddThemeFontSizeOverride("font_size", 14);
        keybindLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.48f, 1.0f));
        slotRoot.AddChild(keybindLabel);

        var nameLabel = new Label
        {
            Name = "Name",
            Text = spell.DisplayLabel,
            Position = new Vector2(8.0f, 18.0f),
            Size = new Vector2(SlotSize.X - 16.0f, 18.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 15);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.96f, 0.96f, 1.0f));
        slotRoot.AddChild(nameLabel);

        var manaLabel = new Label
        {
            Name = "ManaCost",
            Text = $"{spell.DisplayManaCost} MP",
            Position = new Vector2(8.0f, SlotSize.Y - 20.0f),
            Size = new Vector2(SlotSize.X - 16.0f, 14.0f),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        manaLabel.AddThemeFontSizeOverride("font_size", 13);
        manaLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.78f, 1.0f, 1.0f));
        slotRoot.AddChild(manaLabel);

        var armedOverlay = new ColorRect
        {
            Name = "ArmedOverlay",
            Color = ArmedOverlayColor,
            Position = new Vector2(2.0f, 2.0f),
            Size = SlotSize - new Vector2(4.0f, 4.0f),
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        slotRoot.AddChild(armedOverlay);

        var manaUnavailableOverlay = new ColorRect
        {
            Name = "ManaUnavailableOverlay",
            Color = new Color(0.16f, 0.04f, 0.06f, 0.38f),
            Size = SlotSize,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        slotRoot.AddChild(manaUnavailableOverlay);

        var cooldownOverlay = new ColorRect
        {
            Name = "CooldownOverlay",
            Color = new Color(0.0f, 0.0f, 0.0f, 0.55f),
            Size = SlotSize,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        slotRoot.AddChild(cooldownOverlay);

        var cooldownLabel = new Label
        {
            Name = "CooldownText",
            Position = new Vector2(SlotSize.X - 34.0f, 4.0f),
            Size = new Vector2(28.0f, 16.0f),
            HorizontalAlignment = HorizontalAlignment.Right,
            Visible = false,
        };
        cooldownLabel.AddThemeFontSizeOverride("font_size", 16);
        cooldownLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.34f, 0.34f, 1.0f));
        slotRoot.AddChild(cooldownLabel);

        return new SpellSlotView
        {
            Spell = spell,
            Root = slotRoot,
            Frame = frame,
            ManaLabel = manaLabel,
            ArmedOverlay = armedOverlay,
            ManaUnavailableOverlay = manaUnavailableOverlay,
            Overlay = cooldownOverlay,
            CooldownLabel = cooldownLabel,
        };
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
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        if (_slots == null || _slotViews.Count == 0)
        {
            OffsetLeft = ScreenMargin.X;
            OffsetRight = ScreenMargin.X;
            OffsetBottom = -ScreenMargin.Y;
            OffsetTop = OffsetBottom;
            return;
        }

        var spacing = _slots.GetThemeConstant("separation");
        var totalWidth = (SlotSize.X * _slotViews.Count) + (spacing * Math.Max(0, _slotViews.Count - 1));
        var totalHeight = SlotSize.Y;
        OffsetLeft = ScreenMargin.X;
        OffsetRight = ScreenMargin.X + totalWidth;
        OffsetBottom = -ScreenMargin.Y;
        OffsetTop = OffsetBottom - totalHeight;
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

    private void UpdateManaAvailabilityView(SpellSlotView slotView)
    {
        if (_player == null ||
            !GodotObject.IsInstanceValid(_player) ||
            slotView.Spell == null ||
            !GodotObject.IsInstanceValid(slotView.Spell) ||
            slotView.ManaLabel == null ||
            slotView.ManaUnavailableOverlay == null ||
            slotView.Root == null)
        {
            return;
        }

        var slotSize = slotView.Root.Size;
        if (slotSize == Vector2.Zero)
            slotSize = SlotSize;

        var canAffordSpell = _player.CurrentMana >= Math.Max(0, slotView.Spell.DisplayManaCost);
        slotView.ManaLabel.AddThemeColorOverride(
            "font_color",
            canAffordSpell
                ? new Color(0.55f, 0.78f, 1.0f, 1.0f)
                : new Color(1.0f, 0.34f, 0.34f, 1.0f));

        slotView.ManaUnavailableOverlay.Visible = !canAffordSpell;
        slotView.ManaUnavailableOverlay.Position = Vector2.Zero;
        slotView.ManaUnavailableOverlay.Size = slotSize;
    }

    private void UpdateArmedPlacementView(SpellSlotView slotView)
    {
        if (_player == null ||
            !GodotObject.IsInstanceValid(_player) ||
            slotView.Spell == null ||
            !GodotObject.IsInstanceValid(slotView.Spell) ||
            slotView.Frame == null ||
            slotView.ArmedOverlay == null ||
            slotView.Root == null)
        {
            return;
        }

        var isArmedPlacementSpell = ReferenceEquals(_player.ArmedPlacementSpell, slotView.Spell);
        slotView.Frame.Color = isArmedPlacementSpell ? ArmedFrameColor : DefaultFrameColor;
        slotView.ArmedOverlay.Visible = isArmedPlacementSpell;

        var slotSize = slotView.Root.Size;
        if (slotSize == Vector2.Zero)
            slotSize = SlotSize;

        slotView.ArmedOverlay.Position = new Vector2(2.0f, 2.0f);
        slotView.ArmedOverlay.Size = slotSize - new Vector2(4.0f, 4.0f);
    }

    private void UpdateCooldownView(SpellSlotView slotView)
    {
        if (slotView.Spell == null ||
            !GodotObject.IsInstanceValid(slotView.Spell) ||
            slotView.Overlay == null ||
            slotView.CooldownLabel == null ||
            slotView.Root == null)
        {
            return;
        }

        var cooldownDuration = Math.Max(0.0f, slotView.Spell.CooldownDuration);
        var cooldownRemaining = Math.Max(0.0f, slotView.Spell.CooldownRemaining);
        if (cooldownDuration <= 0.0f || cooldownRemaining <= 0.0f)
        {
            slotView.Overlay.Visible = false;
            slotView.CooldownLabel.Visible = false;
            return;
        }

        var slotSize = slotView.Root.Size;
        if (slotSize == Vector2.Zero)
            slotSize = SlotSize;

        var cooldownFraction = Mathf.Clamp(cooldownRemaining / cooldownDuration, 0.0f, 1.0f);
        slotView.Overlay.Visible = true;
        slotView.Overlay.Position = Vector2.Zero;
        slotView.Overlay.Size = new Vector2(slotSize.X, slotSize.Y * cooldownFraction);

        slotView.CooldownLabel.Visible = true;
        slotView.CooldownLabel.Position = new Vector2(slotSize.X - 34.0f, 4.0f);
        slotView.CooldownLabel.Size = new Vector2(28.0f, 16.0f);
        slotView.CooldownLabel.Text = cooldownRemaining >= 1.0f
            ? Mathf.CeilToInt(cooldownRemaining).ToString()
            : $"{cooldownRemaining:0.0}";
    }
}
