using System;
using System.Collections.Generic;

using Godot;

[GlobalClass]
public partial class MenuHubLogPage : Control
{
    private const int DefaultLineFontSize = 14;
    private const float ScrollBottomThreshold = 8.0f;

    private static readonly Color InfoColor = new(0.88f, 0.88f, 0.88f, 1.0f);
    private static readonly Color DamageColor = new(1.0f, 0.45f, 0.45f, 1.0f);
    private static readonly Color HealColor = new(0.45f, 1.0f, 0.55f, 1.0f);
    private static readonly Color AbsorbColor = new(1.0f, 0.95f, 0.45f, 1.0f);
    private static readonly Color DebugColor = new(0.6f, 0.75f, 1.0f, 1.0f);

    private static readonly Color ActiveFilterTint = new(1.0f, 0.85f, 0.35f);
    private static readonly Color InactiveFilterTint = Colors.White;

    [Export]
    public NodePath FilterRowPath { get; set; } = new("Margin/VBox/FilterRow");

    [Export]
    public NodePath ScrollPath { get; set; } = new("Margin/VBox/Scroll");

    [Export]
    public NodePath RowsPath { get; set; } = new("Margin/VBox/Scroll/Rows");

    [Export]
    public NodePath AllFilterButtonPath { get; set; } = new("Margin/VBox/FilterRow/AllFilterButton");

    [Export]
    public NodePath DamageFilterButtonPath { get; set; } = new("Margin/VBox/FilterRow/DamageFilterButton");

    [Export]
    public NodePath HealingFilterButtonPath { get; set; } = new("Margin/VBox/FilterRow/HealingFilterButton");

    [Export]
    public NodePath LootFilterButtonPath { get; set; } = new("Margin/VBox/FilterRow/LootFilterButton");

    [Export]
    public NodePath SystemFilterButtonPath { get; set; } = new("Margin/VBox/FilterRow/SystemFilterButton");

    [Export]
    public NodePath DebugFilterButtonPath { get; set; } = new("Margin/VBox/FilterRow/DebugFilterButton");

    private ScrollContainer _scroll;
    private VScrollBar _scrollBar;
    private VBoxContainer _rowsContainer;
    private Button _allFilterButton;
    private Button _damageFilterButton;
    private Button _healingFilterButton;
    private Button _lootFilterButton;
    private Button _systemFilterButton;
    private Button _debugFilterButton;
    private Action<CombatLogEntry> _entryHandler;
    private Action _scrollBarChangedHandler;

    private bool _showAll = true;
    private readonly HashSet<CombatLogCategory> _activeCategories = new();
    private bool _pendingScrollToBottom;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _scroll = GetNodeOrNull<ScrollContainer>(ScrollPath);
        _rowsContainer = GetNodeOrNull<VBoxContainer>(RowsPath);
        _allFilterButton = GetNodeOrNull<Button>(AllFilterButtonPath);
        _damageFilterButton = GetNodeOrNull<Button>(DamageFilterButtonPath);
        _healingFilterButton = GetNodeOrNull<Button>(HealingFilterButtonPath);
        _lootFilterButton = GetNodeOrNull<Button>(LootFilterButtonPath);
        _systemFilterButton = GetNodeOrNull<Button>(SystemFilterButtonPath);
        _debugFilterButton = GetNodeOrNull<Button>(DebugFilterButtonPath);

        if (_scroll != null)
        {
            _scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            _scrollBar = _scroll.GetVScrollBar();
            if (_scrollBar != null && GodotObject.IsInstanceValid(_scrollBar))
            {
                _scrollBarChangedHandler = OnScrollBarChanged;
                _scrollBar.Changed += _scrollBarChangedHandler;
            }
        }

        WireFilterButton(_allFilterButton, OnAllFilterPressed);
        WireFilterButton(_damageFilterButton, OnDamageFilterPressed);
        WireFilterButton(_healingFilterButton, OnHealingFilterPressed);
        WireFilterButton(_lootFilterButton, OnLootFilterPressed);
        WireFilterButton(_systemFilterButton, OnSystemFilterPressed);
        WireFilterButton(_debugFilterButton, OnDebugFilterPressed);

        UpdateFilterHighlights();

        _entryHandler = OnCombatLogEntry;
        CombatLog.Emitted += _entryHandler;

        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_entryHandler != null)
            CombatLog.Emitted -= _entryHandler;

        UnwireFilterButton(_allFilterButton, OnAllFilterPressed);
        UnwireFilterButton(_damageFilterButton, OnDamageFilterPressed);
        UnwireFilterButton(_healingFilterButton, OnHealingFilterPressed);
        UnwireFilterButton(_lootFilterButton, OnLootFilterPressed);
        UnwireFilterButton(_systemFilterButton, OnSystemFilterPressed);
        UnwireFilterButton(_debugFilterButton, OnDebugFilterPressed);

        if (_scrollBar != null && GodotObject.IsInstanceValid(_scrollBar) && _scrollBarChangedHandler != null)
        {
            _scrollBar.Changed -= _scrollBarChangedHandler;
            _scrollBarChangedHandler = null;
        }
    }

    public void OnPageEntered()
    {
        Rebuild();
    }

    private void OnCombatLogEntry(CombatLogEntry entry)
    {
        if (!IsEntryVisible(entry))
            return;

        var shouldFollow = IsScrollAtBottom();
        AppendRow(entry);
        if (shouldFollow)
            _pendingScrollToBottom = true;
    }

    private bool IsEntryVisible(CombatLogEntry entry)
    {
        if (_showAll)
            return true;

        return _activeCategories.Contains(entry.Category);
    }

    private void Rebuild()
    {
        if (_rowsContainer == null || !GodotObject.IsInstanceValid(_rowsContainer))
            return;

        ClearRows();

        foreach (var entry in CombatLog.Recent)
        {
            if (IsEntryVisible(entry))
                AppendRow(entry);
        }

        _pendingScrollToBottom = true;
    }

    private void ClearRows()
    {
        if (_rowsContainer == null)
            return;

        foreach (var child in _rowsContainer.GetChildren())
        {
            _rowsContainer.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void AppendRow(CombatLogEntry entry)
    {
        if (_rowsContainer == null || !GodotObject.IsInstanceValid(_rowsContainer))
            return;

        var label = new Label
        {
            Text = entry.Text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", DefaultLineFontSize);
        label.AddThemeColorOverride("font_color", ResolveColorFor(entry.Kind));
        _rowsContainer.AddChild(label);
    }

    private bool IsScrollAtBottom()
    {
        if (_scrollBar == null || !GodotObject.IsInstanceValid(_scrollBar))
            return true;

        var page = (float)_scrollBar.Page;
        var max = (float)_scrollBar.MaxValue;
        var value = (float)_scrollBar.Value;

        if (max <= page)
            return true;

        return value + page >= max - ScrollBottomThreshold;
    }

    private void OnScrollBarChanged()
    {
        if (!_pendingScrollToBottom)
            return;

        if (_scroll == null || !GodotObject.IsInstanceValid(_scroll))
            return;

        if (_scrollBar == null || !GodotObject.IsInstanceValid(_scrollBar))
            return;

        _pendingScrollToBottom = false;
        _scroll.ScrollVertical = (int)_scrollBar.MaxValue;
    }

    private void OnAllFilterPressed()
    {
        _showAll = true;
        _activeCategories.Clear();
        UpdateFilterHighlights();
        Rebuild();
    }

    private void OnDamageFilterPressed() => SelectSingleCategory(CombatLogCategory.Damage);

    private void OnHealingFilterPressed() => SelectSingleCategory(CombatLogCategory.Healing);

    private void OnLootFilterPressed() => SelectSingleCategory(CombatLogCategory.Loot);

    private void OnSystemFilterPressed() => SelectSingleCategory(CombatLogCategory.System);

    private void OnDebugFilterPressed() => SelectSingleCategory(CombatLogCategory.Debug);

    private void SelectSingleCategory(CombatLogCategory category)
    {
        _showAll = false;
        _activeCategories.Clear();
        _activeCategories.Add(category);
        UpdateFilterHighlights();
        Rebuild();
    }

    private void UpdateFilterHighlights()
    {
        ApplyFilterTint(_allFilterButton, _showAll);
        ApplyFilterTint(_damageFilterButton, !_showAll && _activeCategories.Contains(CombatLogCategory.Damage));
        ApplyFilterTint(_healingFilterButton, !_showAll && _activeCategories.Contains(CombatLogCategory.Healing));
        ApplyFilterTint(_lootFilterButton, !_showAll && _activeCategories.Contains(CombatLogCategory.Loot));
        ApplyFilterTint(_systemFilterButton, !_showAll && _activeCategories.Contains(CombatLogCategory.System));
        ApplyFilterTint(_debugFilterButton, !_showAll && _activeCategories.Contains(CombatLogCategory.Debug));
    }

    private static void ApplyFilterTint(Button button, bool active)
    {
        if (button == null)
            return;

        button.SelfModulate = active ? ActiveFilterTint : InactiveFilterTint;
    }

    private static void WireFilterButton(Button button, Action handler)
    {
        if (button == null)
            return;

        button.Pressed += handler;
    }

    private static void UnwireFilterButton(Button button, Action handler)
    {
        if (button == null)
            return;

        button.Pressed -= handler;
    }

    private static Color ResolveColorFor(CombatLogEntryKind kind)
    {
        return kind switch
        {
            CombatLogEntryKind.Damage => DamageColor,
            CombatLogEntryKind.Heal => HealColor,
            CombatLogEntryKind.Absorb => AbsorbColor,
            CombatLogEntryKind.Debug => DebugColor,
            _ => InfoColor,
        };
    }
}
