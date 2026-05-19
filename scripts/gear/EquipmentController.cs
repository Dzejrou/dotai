using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class EquipmentController : Node
{
    [Signal]
    public delegate void ChangedEventHandler();

    private readonly Dictionary<EquipmentSlot, GearInstance> _equipped = new();

    public GearInstance GetEquipped(EquipmentSlot slot)
    {
        return _equipped.TryGetValue(slot, out var gear) ? gear : null;
    }

    public IEnumerable<GearInstance> EnumerateEquipped()
    {
        foreach (var gear in _equipped.Values)
        {
            if (gear != null)
                yield return gear;
        }
    }

    public bool TryEquip(GearInstance gear, EquipmentSlot slot, out GearInstance displaced)
    {
        displaced = null;
        if (gear?.Definition == null)
            return false;

        if (gear.Definition.Slot != slot)
            return false;

        _equipped.TryGetValue(slot, out displaced);
        _equipped[slot] = gear;
        EmitSignal(SignalName.Changed);
        return true;
    }

    public bool TryUnequip(EquipmentSlot slot, out GearInstance removed)
    {
        if (!_equipped.TryGetValue(slot, out removed) || removed == null)
        {
            removed = null;
            return false;
        }

        _equipped.Remove(slot);
        EmitSignal(SignalName.Changed);
        return true;
    }

    // Additive sum of all matching stat modifiers across equipped gear. Float-valued; callers
    // that consume integer stats round at the use site so the controller stays stat-agnostic.
    public float ResolveStatBonus(string statId)
    {
        if (string.IsNullOrEmpty(statId))
            return 0.0f;

        var total = 0.0f;
        foreach (var gear in _equipped.Values)
        {
            if (gear == null)
                continue;

            foreach (var modifier in gear.AllModifiers)
            {
                if (string.Equals(modifier.StatId, statId, StringComparison.Ordinal))
                    total += modifier.Value;
            }
        }

        return total;
    }

    public int ResolveIntBonus(string statId)
    {
        return (int)Math.Round(ResolveStatBonus(statId));
    }

    public Dictionary<string, GearInstanceSaveData> CreateSaveSnapshot()
    {
        var snapshot = new Dictionary<string, GearInstanceSaveData>();
        foreach (var pair in _equipped)
        {
            if (pair.Value == null)
                continue;

            snapshot[pair.Key.ToString()] = GearSaveSerializer.Serialize(pair.Value);
        }
        return snapshot;
    }

    public void LoadFromSnapshot(Dictionary<string, GearInstanceSaveData> snapshot, GearGenerationRules rules)
    {
        _equipped.Clear();

        if (snapshot != null)
        {
            foreach (var pair in snapshot)
            {
                if (pair.Value == null)
                    continue;

                if (!Enum.TryParse<EquipmentSlot>(pair.Key, out var slot))
                {
                    GD.PushWarning($"{nameof(EquipmentController)}: dropping unknown equipment slot '{pair.Key}'.");
                    continue;
                }

                var gear = GearSaveSerializer.Rehydrate(pair.Value, rules);
                if (gear == null)
                {
                    GD.PushWarning(
                        $"{nameof(EquipmentController)}: dropping equipment slot '{pair.Key}'; could not rehydrate gear.");
                    continue;
                }

                if (gear.Definition.Slot != slot)
                {
                    GD.PushWarning(
                        $"{nameof(EquipmentController)}: equipped slot '{slot}' but gear is for '{gear.Definition.Slot}'; skipping.");
                    continue;
                }

                _equipped[slot] = gear;
            }
        }

        EmitSignal(SignalName.Changed);
    }
}
