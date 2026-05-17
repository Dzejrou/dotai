using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class PlayerDebugStatsWindow : Control
{
    private enum FieldKind
    {
        Level,
        CurrentExperience,
        CurrentHealth,
        CurrentMana,
        MaxHealth,
        MaxMana,
        MP5,
        Power,
        CritRate,
        CritDamage,
        Haste,
        MovementSpeedMultiplier,
        PhysicalDamageBonus,
        FireDamageBonus,
        IceDamageBonus,
        PoisonDamageBonus,
        ArcaneDamageBonus,
        PhysicalResistance,
        FireResistance,
        IceResistance,
        PoisonResistance,
        ArcaneResistance,
    }

    private sealed class FieldRow
    {
        public FieldKind Kind;
        public SpinBox SpinBox;
    }

    [Export]
    public NodePath CloseButtonPath { get; set; } = new("Center/Panel/Margin/VBox/Header/CloseButton");

    [Export]
    public NodePath RefreshButtonPath { get; set; } = new("Center/Panel/Margin/VBox/Header/RefreshButton");

    [Export]
    public NodePath RowsContainerPath { get; set; } = new("Center/Panel/Margin/VBox/Scroll/Rows");

    [Export]
    public NodePath RequiredXpLabelPath { get; set; } = new("Center/Panel/Margin/VBox/RequiredXpLabel");

    private Player _player;
    private Button _closeButton;
    private Button _refreshButton;
    private VBoxContainer _rowsContainer;
    private Label _requiredXpLabel;
    private readonly List<FieldRow> _rows = new();
    private bool _suppressEditingSignals;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _closeButton = GetNodeOrNull<Button>(CloseButtonPath);
        _refreshButton = GetNodeOrNull<Button>(RefreshButtonPath);
        _rowsContainer = GetNodeOrNull<VBoxContainer>(RowsContainerPath);
        _requiredXpLabel = GetNodeOrNull<Label>(RequiredXpLabelPath);

        if (_closeButton != null)
            _closeButton.Pressed += CloseWindow;

        if (_refreshButton != null)
            _refreshButton.Pressed += RefreshValues;

        BuildRows();
    }

    public override void _ExitTree()
    {
        if (_closeButton != null)
            _closeButton.Pressed -= CloseWindow;

        if (_refreshButton != null)
            _refreshButton.Pressed -= RefreshValues;

        UnbindPlayer();
    }

    public void Bind(Player player)
    {
        UnbindPlayer();
        _player = player;
        if (Visible)
            RefreshValues();
    }

    public void ToggleWindow()
    {
        if (Visible)
            CloseWindow();
        else
            OpenWindow();
    }

    public void OpenWindow()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        Visible = true;
        RefreshValues();
    }

    public void CloseWindow()
    {
        Visible = false;
    }

    private void UnbindPlayer()
    {
        _player = null;
    }

    private void BuildRows()
    {
        if (_rowsContainer == null)
            return;

        foreach (var child in _rowsContainer.GetChildren())
        {
            _rowsContainer.RemoveChild(child);
            child.QueueFree();
        }

        _rows.Clear();

        AddSection("Progression");
        AddRow(FieldKind.Level, "Level", min: 1, max: 60, step: 1, isInteger: true);
        AddRow(FieldKind.CurrentExperience, "Current XP", min: 0, max: 1_000_000, step: 10, isInteger: true);

        AddSection("Current State");
        AddRow(FieldKind.CurrentHealth, "Current Health", min: 0, max: 1_000_000, step: 1, isInteger: true);
        AddRow(FieldKind.CurrentMana, "Current Mana", min: 0, max: 1_000_000, step: 1, isInteger: true);

        AddSection("Stats");
        AddRow(FieldKind.MaxHealth, "Max Health", min: 1, max: 1_000_000, step: 10, isInteger: true);
        AddRow(FieldKind.MaxMana, "Max Mana", min: 0, max: 1_000_000, step: 10, isInteger: true);
        AddRow(FieldKind.MP5, "MP5", min: 0, max: 1_000_000, step: 1, isInteger: true);
        AddRow(FieldKind.Power, "Power", min: 0.0, max: 1_000_000.0, step: 1.0);
        AddRow(FieldKind.CritRate, "Crit Rate", min: 0.0, max: 1.0, step: 0.01);
        AddRow(FieldKind.CritDamage, "Crit Damage", min: 0.0, max: 10.0, step: 0.05);
        AddRow(FieldKind.Haste, "Haste", min: 0, max: 100_000, step: 10, isInteger: true);
        AddRow(FieldKind.MovementSpeedMultiplier, "Move Speed x", min: 0.0, max: 10.0, step: 0.05);

        AddSection("Damage Bonuses");
        AddRow(FieldKind.PhysicalDamageBonus, "Physical", min: -1.0, max: 10.0, step: 0.05);
        AddRow(FieldKind.FireDamageBonus, "Fire", min: -1.0, max: 10.0, step: 0.05);
        AddRow(FieldKind.IceDamageBonus, "Ice", min: -1.0, max: 10.0, step: 0.05);
        AddRow(FieldKind.PoisonDamageBonus, "Poison", min: -1.0, max: 10.0, step: 0.05);
        AddRow(FieldKind.ArcaneDamageBonus, "Arcane", min: -1.0, max: 10.0, step: 0.05);

        AddSection("Resistances");
        AddRow(FieldKind.PhysicalResistance, "Physical", min: -1.0, max: 1.0, step: 0.05);
        AddRow(FieldKind.FireResistance, "Fire", min: -1.0, max: 1.0, step: 0.05);
        AddRow(FieldKind.IceResistance, "Ice", min: -1.0, max: 1.0, step: 0.05);
        AddRow(FieldKind.PoisonResistance, "Poison", min: -1.0, max: 1.0, step: 0.05);
        AddRow(FieldKind.ArcaneResistance, "Arcane", min: -1.0, max: 1.0, step: 0.05);
    }

    private void AddSection(string title)
    {
        if (_rowsContainer == null)
            return;

        var label = new Label
        {
            Name = $"{title}_Section",
            Text = title,
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        _rowsContainer.AddChild(label);

        var separator = new HSeparator { Name = $"{title}_Sep" };
        _rowsContainer.AddChild(separator);
    }

    private void AddRow(FieldKind kind, string label, double min, double max, double step, bool isInteger = false)
    {
        if (_rowsContainer == null)
            return;

        var row = new HBoxContainer
        {
            Name = $"{kind}_Row",
        };
        row.AddThemeConstantOverride("separation", 8);

        var nameLabel = new Label
        {
            Name = "Label",
            Text = label,
            CustomMinimumSize = new Vector2(180.0f, 0.0f),
        };
        row.AddChild(nameLabel);

        var spinBox = new SpinBox
        {
            Name = "Value",
            MinValue = min,
            MaxValue = max,
            Step = step,
            CustomMinimumSize = new Vector2(160.0f, 0.0f),
            Rounded = isInteger,
            AllowGreater = false,
            AllowLesser = false,
        };
        row.AddChild(spinBox);

        var field = new FieldRow
        {
            Kind = kind,
            SpinBox = spinBox,
        };
        _rows.Add(field);

        spinBox.ValueChanged += value => OnRowValueChanged(field, value);

        _rowsContainer.AddChild(row);
    }

    private void OnRowValueChanged(FieldRow field, double value)
    {
        if (_suppressEditingSignals)
            return;

        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        var stats = _player.DebugStats;
        switch (field.Kind)
        {
            case FieldKind.Level:
                _player.DebugSetLevel((int)Math.Round(value));
                break;
            case FieldKind.CurrentExperience:
                _player.DebugSetCurrentExperience((int)Math.Round(value));
                break;
            case FieldKind.CurrentHealth:
                _player.DebugSetCurrentHealth((int)Math.Round(value));
                break;
            case FieldKind.CurrentMana:
                _player.DebugSetCurrentMana((int)Math.Round(value));
                break;
            case FieldKind.MaxHealth:
                if (stats != null)
                {
                    stats.MaxHealth = Math.Max(1, (int)Math.Round(value));
                    _player.DebugResyncMaxHealthFromStats();
                }
                break;
            case FieldKind.MaxMana:
                if (stats != null)
                {
                    stats.MaxMana = Math.Max(0, (int)Math.Round(value));
                    _player.DebugResyncMaxManaFromStats();
                }
                break;
            case FieldKind.MP5:
                if (stats != null)
                    stats.MP5 = Math.Max(0, (int)Math.Round(value));
                break;
            case FieldKind.Power:
                if (stats != null)
                    stats.Power = Math.Max(0.0f, (float)value);
                break;
            case FieldKind.CritRate:
                if (stats != null)
                    stats.CritRate = Math.Max(0.0f, (float)value);
                break;
            case FieldKind.CritDamage:
                if (stats != null)
                    stats.CritDamage = Math.Max(0.0f, (float)value);
                break;
            case FieldKind.Haste:
                if (stats != null)
                    stats.Haste = Math.Max(0, (int)Math.Round(value));
                break;
            case FieldKind.MovementSpeedMultiplier:
                if (stats != null)
                    stats.MovementSpeedMultiplier = Math.Max(0.0f, (float)value);
                break;
            case FieldKind.PhysicalDamageBonus:
                if (stats != null) stats.PhysicalDamageBonus = (float)value;
                break;
            case FieldKind.FireDamageBonus:
                if (stats != null) stats.FireDamageBonus = (float)value;
                break;
            case FieldKind.IceDamageBonus:
                if (stats != null) stats.IceDamageBonus = (float)value;
                break;
            case FieldKind.PoisonDamageBonus:
                if (stats != null) stats.PoisonDamageBonus = (float)value;
                break;
            case FieldKind.ArcaneDamageBonus:
                if (stats != null) stats.ArcaneDamageBonus = (float)value;
                break;
            case FieldKind.PhysicalResistance:
                if (stats != null) stats.PhysicalResistance = (float)value;
                break;
            case FieldKind.FireResistance:
                if (stats != null) stats.FireResistance = (float)value;
                break;
            case FieldKind.IceResistance:
                if (stats != null) stats.IceResistance = (float)value;
                break;
            case FieldKind.PoisonResistance:
                if (stats != null) stats.PoisonResistance = (float)value;
                break;
            case FieldKind.ArcaneResistance:
                if (stats != null) stats.ArcaneResistance = (float)value;
                break;
        }

        RefreshValues();
    }

    private void RefreshValues()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        var stats = _player.DebugStats;
        var health = _player.DebugHealthState;
        var mana = _player.DebugManaState;

        _suppressEditingSignals = true;
        try
        {
            foreach (var row in _rows)
            {
                if (row.SpinBox == null || !GodotObject.IsInstanceValid(row.SpinBox))
                    continue;

                var value = ResolveCurrentValue(row.Kind, stats, health, mana);
                row.SpinBox.SetValueNoSignal(value);
            }
        }
        finally
        {
            _suppressEditingSignals = false;
        }

        if (_requiredXpLabel != null)
        {
            var required = _player.GetRequiredExperienceForCurrentLevel();
            _requiredXpLabel.Text = $"Required XP for level {_player.Level}: {required}";
        }
    }

    private double ResolveCurrentValue(FieldKind kind, Stats stats, HealthState health, ManaState mana)
    {
        return kind switch
        {
            FieldKind.Level => _player.Level,
            FieldKind.CurrentExperience => _player.CurrentExperience,
            FieldKind.CurrentHealth => health?.Current ?? 0,
            FieldKind.CurrentMana => mana?.Current ?? 0,
            FieldKind.MaxHealth => stats?.MaxHealth ?? 0,
            FieldKind.MaxMana => stats?.MaxMana ?? 0,
            FieldKind.MP5 => stats?.MP5 ?? 0,
            FieldKind.Power => stats?.Power ?? 0.0f,
            FieldKind.CritRate => stats?.CritRate ?? 0.0f,
            FieldKind.CritDamage => stats?.CritDamage ?? 0.0f,
            FieldKind.Haste => stats?.Haste ?? 0,
            FieldKind.MovementSpeedMultiplier => stats?.MovementSpeedMultiplier ?? 1.0f,
            FieldKind.PhysicalDamageBonus => stats?.PhysicalDamageBonus ?? 0.0f,
            FieldKind.FireDamageBonus => stats?.FireDamageBonus ?? 0.0f,
            FieldKind.IceDamageBonus => stats?.IceDamageBonus ?? 0.0f,
            FieldKind.PoisonDamageBonus => stats?.PoisonDamageBonus ?? 0.0f,
            FieldKind.ArcaneDamageBonus => stats?.ArcaneDamageBonus ?? 0.0f,
            FieldKind.PhysicalResistance => stats?.PhysicalResistance ?? 0.0f,
            FieldKind.FireResistance => stats?.FireResistance ?? 0.0f,
            FieldKind.IceResistance => stats?.IceResistance ?? 0.0f,
            FieldKind.PoisonResistance => stats?.PoisonResistance ?? 0.0f,
            FieldKind.ArcaneResistance => stats?.ArcaneResistance ?? 0.0f,
            _ => 0.0,
        };
    }
}
