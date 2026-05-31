using Godot;

using System;
using System.Globalization;

[GlobalClass]
public partial class MenuHubCharacterPage : Control
{
    [Export]
    public NodePath LevelLabelPath { get; set; } = new("Margin/VBox/Header/LevelLabel");

    [Export]
    public NodePath ExperienceLabelPath { get; set; } = new("Margin/VBox/Header/ExperienceLabel");

    [Export]
    public NodePath CoreStatsContainerPath { get; set; } = new("Margin/VBox/Columns/CoreStatsColumn/CoreStats");

    [Export]
    public NodePath DamageBonusContainerPath { get; set; } = new("Margin/VBox/Columns/DamageBonusColumn/DamageBonuses");

    [Export]
    public NodePath ResistanceContainerPath { get; set; } = new("Margin/VBox/Columns/ResistanceColumn/Resistances");

    private Player _player;
    private EquipmentController _equipment;
    private bool _equipmentChangedBound;
    private bool _playerLevelBound;
    private bool _playerExperienceBound;

    private Label _levelLabel;
    private Label _experienceLabel;
    private VBoxContainer _coreStatsContainer;
    private VBoxContainer _damageBonusContainer;
    private VBoxContainer _resistanceContainer;

    private Label _statMaxHealth;
    private Label _statMaxMana;
    private Label _statPower;
    private Label _statMP5;
    private Label _statCritRate;
    private Label _statCritDamage;
    private Label _statHaste;
    private Label _statMoveSpeed;
    private Label _statGenericDamageBonus;

    private Label _dmgBonusPhysical;
    private Label _dmgBonusFire;
    private Label _dmgBonusIce;
    private Label _dmgBonusPoison;
    private Label _dmgBonusArcane;

    private Label _resistPhysical;
    private Label _resistFire;
    private Label _resistIce;
    private Label _resistPoison;
    private Label _resistArcane;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _levelLabel = GetNodeOrNull<Label>(LevelLabelPath);
        _experienceLabel = GetNodeOrNull<Label>(ExperienceLabelPath);
        _coreStatsContainer = GetNodeOrNull<VBoxContainer>(CoreStatsContainerPath);
        _damageBonusContainer = GetNodeOrNull<VBoxContainer>(DamageBonusContainerPath);
        _resistanceContainer = GetNodeOrNull<VBoxContainer>(ResistanceContainerPath);

        BuildCoreStats();
        BuildDamageBonuses();
        BuildResistances();

        Refresh();
    }

    public override void _ExitTree()
    {
        UnbindCurrentPlayer();
        UnbindCurrentEquipment();
    }

    public void Bind(Player player, EquipmentController equipment)
    {
        if (!ReferenceEquals(_player, player))
        {
            UnbindCurrentPlayer();
            _player = player;

            if (_player != null && GodotObject.IsInstanceValid(_player))
            {
                var levelCallable = new Callable(this, nameof(OnPlayerLevelChanged));
                if (!_player.IsConnected(Player.SignalName.LevelChanged, levelCallable))
                    _player.Connect(Player.SignalName.LevelChanged, levelCallable);
                _playerLevelBound = true;

                var xpCallable = new Callable(this, nameof(OnPlayerExperienceChanged));
                if (!_player.IsConnected(Player.SignalName.ExperienceChanged, xpCallable))
                    _player.Connect(Player.SignalName.ExperienceChanged, xpCallable);
                _playerExperienceBound = true;
            }
        }

        if (!ReferenceEquals(_equipment, equipment))
        {
            UnbindCurrentEquipment();
            _equipment = equipment;

            if (_equipment != null && GodotObject.IsInstanceValid(_equipment))
            {
                var callable = new Callable(this, nameof(OnEquipmentChanged));
                if (!_equipment.IsConnected(EquipmentController.SignalName.Changed, callable))
                    _equipment.Connect(EquipmentController.SignalName.Changed, callable);

                _equipmentChangedBound = true;
            }
        }

        Refresh();
    }

    // Called by MenuHub when this page becomes the active one.
    public void OnPageEntered()
    {
        Refresh();
    }

    private void OnPlayerLevelChanged(int newLevel)
    {
        Refresh();
    }

    private void OnPlayerExperienceChanged(int currentExperience, int requiredExperience, int level)
    {
        RefreshLevelAndXp();
    }

    private void OnEquipmentChanged()
    {
        RefreshStats();
    }

    private void Refresh()
    {
        RefreshLevelAndXp();
        RefreshStats();
    }

    private void RefreshLevelAndXp()
    {
        var hasPlayer = _player != null && GodotObject.IsInstanceValid(_player);

        if (_levelLabel != null)
            _levelLabel.Text = hasPlayer ? $"Level {_player.Level}" : "Level -";

        if (_experienceLabel == null)
            return;

        if (!hasPlayer)
        {
            _experienceLabel.Text = "XP -";
            return;
        }

        var maxLevel = Math.Max(1, _player.MaxLevel);
        if (_player.Level >= maxLevel)
        {
            _experienceLabel.Text = "XP MAX";
            return;
        }

        var required = Math.Max(1, _player.GetRequiredExperienceForCurrentLevel());
        var current = Math.Max(0, _player.CurrentExperience);
        _experienceLabel.Text = $"XP {current} / {required}";
    }

    private void BuildCoreStats()
    {
        if (_coreStatsContainer == null)
            return;

        ClearChildren(_coreStatsContainer);

        _statMaxHealth = AppendStatLabel(_coreStatsContainer);
        _statMaxMana = AppendStatLabel(_coreStatsContainer);
        _statPower = AppendStatLabel(_coreStatsContainer);
        _statMP5 = AppendStatLabel(_coreStatsContainer);
        _statCritRate = AppendStatLabel(_coreStatsContainer);
        _statCritDamage = AppendStatLabel(_coreStatsContainer);
        _statHaste = AppendStatLabel(_coreStatsContainer);
        _statMoveSpeed = AppendStatLabel(_coreStatsContainer);
        _statGenericDamageBonus = AppendStatLabel(_coreStatsContainer);
    }

    private void BuildDamageBonuses()
    {
        if (_damageBonusContainer == null)
            return;

        ClearChildren(_damageBonusContainer);

        _dmgBonusPhysical = AppendStatLabel(_damageBonusContainer);
        _dmgBonusFire = AppendStatLabel(_damageBonusContainer);
        _dmgBonusIce = AppendStatLabel(_damageBonusContainer);
        _dmgBonusPoison = AppendStatLabel(_damageBonusContainer);
        _dmgBonusArcane = AppendStatLabel(_damageBonusContainer);
    }

    private void BuildResistances()
    {
        if (_resistanceContainer == null)
            return;

        ClearChildren(_resistanceContainer);

        _resistPhysical = AppendStatLabel(_resistanceContainer);
        _resistFire = AppendStatLabel(_resistanceContainer);
        _resistIce = AppendStatLabel(_resistanceContainer);
        _resistPoison = AppendStatLabel(_resistanceContainer);
        _resistArcane = AppendStatLabel(_resistanceContainer);
    }

    private static Label AppendStatLabel(Container container)
    {
        var label = new Label
        {
            Text = "-",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        container.AddChild(label);
        return label;
    }

    private static void ClearChildren(Node container)
    {
        if (container == null)
            return;

        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void RefreshStats()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            SetMissingStats();
            return;
        }

        SetTripleInt(_statMaxHealth, "Max Health", _player.ResolvedMaxHealth, _player.BaseMaxHealth);
        SetTripleInt(_statMaxMana, "Max Mana", _player.ResolvedMaxMana, _player.BaseMaxMana);
        SetTripleInt(_statPower, "Power",
            (int)Math.Round(_player.ResolvedPower),
            (int)Math.Round(_player.BasePower));
        SetTotalInt(_statMP5, "MP5", _player.ResolvedMP5);
        SetTriplePercent(_statCritRate, "Crit Rate", _player.ResolvedCritRate, _player.BaseCritRate);
        SetTriplePercent(_statCritDamage, "Crit Damage", _player.ResolvedCritDamage, _player.BaseCritDamage);
        SetTotalInt(_statHaste, "Haste", _player.ResolvedHaste);
        SetTriplePercent(_statMoveSpeed, "Move Speed",
            _player.MovementSpeedMultiplier,
            _player.BaseMovementSpeedMultiplier);
        SetTotalPercent(_statGenericDamageBonus, "Damage Bonus", _player.ResolvedGenericDamageBonus);

        SetTotalPercent(_dmgBonusPhysical, "Physical Damage Bonus", _player.ResolveDamageBonus(DamageSchool.Physical));
        SetTotalPercent(_dmgBonusFire, "Fire Damage Bonus", _player.ResolveDamageBonus(DamageSchool.Fire));
        SetTotalPercent(_dmgBonusIce, "Ice Damage Bonus", _player.ResolveDamageBonus(DamageSchool.Ice));
        SetTotalPercent(_dmgBonusPoison, "Poison Damage Bonus", _player.ResolveDamageBonus(DamageSchool.Poison));
        SetTotalPercent(_dmgBonusArcane, "Arcane Damage Bonus", _player.ResolveDamageBonus(DamageSchool.Arcane));

        SetTotalPercent(_resistPhysical, "Physical Resistance", _player.ResolveResistance(DamageSchool.Physical));
        SetTotalPercent(_resistFire, "Fire Resistance", _player.ResolveResistance(DamageSchool.Fire));
        SetTotalPercent(_resistIce, "Ice Resistance", _player.ResolveResistance(DamageSchool.Ice));
        SetTotalPercent(_resistPoison, "Poison Resistance", _player.ResolveResistance(DamageSchool.Poison));
        SetTotalPercent(_resistArcane, "Arcane Resistance", _player.ResolveResistance(DamageSchool.Arcane));
    }

    private void SetMissingStats()
    {
        foreach (var label in new[]
        {
            _statMaxHealth, _statMaxMana, _statPower, _statMP5,
            _statCritRate, _statCritDamage, _statHaste, _statMoveSpeed,
            _statGenericDamageBonus,
            _dmgBonusPhysical, _dmgBonusFire, _dmgBonusIce, _dmgBonusPoison, _dmgBonusArcane,
            _resistPhysical, _resistFire, _resistIce, _resistPoison, _resistArcane,
        })
        {
            if (label != null)
                label.Text = "-";
        }
    }

    private static void SetTripleInt(Label label, string name, int total, int baseValue)
    {
        if (label == null)
            return;
        var bonus = total - baseValue;
        label.Text = $"{name}: {total} ({baseValue} + {bonus})";
    }

    private static void SetTotalInt(Label label, string name, int total)
    {
        if (label == null)
            return;
        label.Text = $"{name}: {total}";
    }

    private static void SetTriplePercent(Label label, string name, float total, float baseValue)
    {
        if (label == null)
            return;
        var bonus = total - baseValue;
        label.Text = $"{name}: {FormatPercent(total)} ({FormatPercent(baseValue)} + {FormatPercent(bonus)})";
    }

    private static void SetTotalPercent(Label label, string name, float total)
    {
        if (label == null)
            return;
        label.Text = $"{name}: {FormatPercent(total)}";
    }

    private static string FormatPercent(float value)
    {
        return ((int)Math.Round(value * 100.0f)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    private void UnbindCurrentPlayer()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            _playerLevelBound = false;
            _playerExperienceBound = false;
            _player = null;
            return;
        }

        if (_playerLevelBound)
        {
            var callable = new Callable(this, nameof(OnPlayerLevelChanged));
            if (_player.IsConnected(Player.SignalName.LevelChanged, callable))
                _player.Disconnect(Player.SignalName.LevelChanged, callable);
        }

        if (_playerExperienceBound)
        {
            var callable = new Callable(this, nameof(OnPlayerExperienceChanged));
            if (_player.IsConnected(Player.SignalName.ExperienceChanged, callable))
                _player.Disconnect(Player.SignalName.ExperienceChanged, callable);
        }

        _playerLevelBound = false;
        _playerExperienceBound = false;
        _player = null;
    }

    private void UnbindCurrentEquipment()
    {
        if (!_equipmentChangedBound || _equipment == null || !GodotObject.IsInstanceValid(_equipment))
        {
            _equipmentChangedBound = false;
            _equipment = null;
            return;
        }

        var callable = new Callable(this, nameof(OnEquipmentChanged));
        if (_equipment.IsConnected(EquipmentController.SignalName.Changed, callable))
            _equipment.Disconnect(EquipmentController.SignalName.Changed, callable);

        _equipmentChangedBound = false;
        _equipment = null;
    }
}
