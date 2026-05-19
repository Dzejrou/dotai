using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class InventoryController : Node
{
    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Signal]
    public delegate void GoldChangedEventHandler(int totalGold);

    [Export(PropertyHint.Range, "1,500,1")]
    public int SlotCapacity
    {
        get => _slotCapacity;
        set
        {
            ResizeSlots(Math.Max(1, value), IsInsideTree());
        }
    }

    [Export]
    public Godot.Collections.Array<InventoryStartingStack> StartingStacks { get; set; } = new();

    private readonly List<InventoryEntry> _slots = new();
    private int _slotCapacity = 50;
    private bool _startingStacksApplied;
    private int _gold;

    public int Gold => _gold;

    public int AddGold(int amount)
    {
        if (amount <= 0)
            return 0;

        _gold += amount;
        EmitSignal(SignalName.GoldChanged, _gold);
        return amount;
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0 || _gold < amount)
            return false;

        _gold -= amount;
        EmitSignal(SignalName.GoldChanged, _gold);
        return true;
    }

    public void SetGoldForDebugOrLoad(int amount)
    {
        var clamped = Math.Max(0, amount);
        if (clamped == _gold)
            return;

        _gold = clamped;
        EmitSignal(SignalName.GoldChanged, _gold);
    }

    public override void _Ready()
    {
        ResizeSlots(Math.Max(1, _slotCapacity), false);
        ApplyStartingStacks();
        EmitInventoryChanged();
    }

    public int GetSlotCount()
    {
        return _slots.Count;
    }

    public bool TryGetEntry(int slotIndex, out InventoryEntry entry)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
        {
            entry = null;
            return false;
        }

        entry = _slots[slotIndex];
        return entry != null;
    }

    public int AddItem(InventoryItemDefinition item, int quantity)
    {
        var remaining = AddItemInternal(item, quantity);
        if (remaining != Math.Max(0, quantity))
            EmitInventoryChanged();

        return remaining;
    }

    public bool CanAddItem(InventoryItemDefinition item, int quantity)
    {
        return GetRemainingQuantityAfterAdd(item, quantity) == 0;
    }

    public bool CanAddGear(GearInstance gear)
    {
        if (gear?.Definition == null)
            return false;

        for (var i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] == null)
                return true;
        }

        return false;
    }

    public bool AddGear(GearInstance gear)
    {
        if (gear?.Definition == null)
            return false;

        for (var i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
                continue;

            _slots[i] = new InventoryGearEntry(gear);
            EmitInventoryChanged();
            return true;
        }

        return false;
    }

    public bool IsSlotEmpty(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return false;

        return _slots[slotIndex] == null;
    }

    public bool TryPlaceGear(int slotIndex, GearInstance gear)
    {
        if (gear?.Definition == null)
            return false;

        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return false;

        if (_slots[slotIndex] != null)
            return false;

        _slots[slotIndex] = new InventoryGearEntry(gear);
        EmitInventoryChanged();
        return true;
    }

    public int GetQuantityByKeyKind(InventoryKeyKind keyKind)
    {
        if (keyKind == InventoryKeyKind.None)
            return 0;

        var total = 0;
        foreach (var entry in _slots)
        {
            if (entry is not InventoryStackEntry stackEntry)
                continue;

            var item = stackEntry.Stack.Item;
            if (item == null || item.KeyKind != keyKind)
                continue;

            total += stackEntry.Stack.Quantity;
        }

        return total;
    }

    public bool HasKeyKind(InventoryKeyKind keyKind, int quantity = 1)
    {
        return GetQuantityByKeyKind(keyKind) >= Math.Max(1, quantity);
    }

    public bool TryConsumeKeyKind(InventoryKeyKind keyKind, int quantity = 1)
    {
        var remaining = Math.Max(1, quantity);
        if (!HasKeyKind(keyKind, remaining))
            return false;

        for (var i = 0; i < _slots.Count && remaining > 0; i++)
        {
            if (_slots[i] is not InventoryStackEntry stackEntry)
                continue;

            var item = stackEntry.Stack.Item;
            if (item == null || item.KeyKind != keyKind)
                continue;

            remaining -= stackEntry.Stack.RemoveQuantity(remaining);
            if (stackEntry.Stack.IsEmpty)
                _slots[i] = null;
        }

        EmitInventoryChanged();
        return true;
    }

    public void Clear()
    {
        var changed = false;
        for (var i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] == null)
                continue;

            _slots[i] = null;
            changed = true;
        }

        if (changed)
            EmitInventoryChanged();
    }

    public bool TryInteractSlots(int fromSlot, int toSlot)
    {
        if (fromSlot == toSlot) return false;
        if (fromSlot < 0 || fromSlot >= _slots.Count) return false;
        if (toSlot < 0 || toSlot >= _slots.Count) return false;

        var fromEntry = _slots[fromSlot];
        if (fromEntry == null) return false;

        var toEntry = _slots[toSlot];

        if (toEntry == null)
        {
            _slots[toSlot] = fromEntry;
            _slots[fromSlot] = null;
        }
        else if (toEntry is InventoryStackEntry toStackEntry &&
                 fromEntry is InventoryStackEntry fromStackEntry &&
                 toStackEntry.CanAcceptMergeFrom(fromStackEntry))
        {
            var remaining = toStackEntry.Stack.AddQuantity(fromStackEntry.Stack.Quantity);
            _slots[fromSlot] = remaining > 0
                ? new InventoryStackEntry(new InventoryStack(fromStackEntry.Stack.Item, remaining))
                : null;
        }
        else
        {
            _slots[fromSlot] = toEntry;
            _slots[toSlot] = fromEntry;
        }

        EmitInventoryChanged();
        return true;
    }

    public InventoryEntry TakeEntry(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return null;

        var entry = _slots[slotIndex];
        if (entry == null) return null;

        _slots[slotIndex] = null;
        EmitInventoryChanged();
        return entry;
    }

    private void ApplyStartingStacks()
    {
        if (_startingStacksApplied)
            return;

        _startingStacksApplied = true;

        foreach (var startingStack in StartingStacks)
        {
            if (startingStack?.Item == null)
                continue;

            var remaining = AddItemInternal(startingStack.Item, startingStack.Quantity);
            if (remaining > 0)
            {
                GD.PushWarning(
                    $"{nameof(InventoryController)} could not fit {remaining} of '{startingStack.Item.DisplayName}' into the starting inventory.");
            }
        }
    }

    private int AddItemInternal(InventoryItemDefinition item, int quantity)
    {
        if (item == null)
            return Math.Max(0, quantity);

        var remaining = Math.Max(0, quantity);
        if (remaining == 0)
            return 0;

        for (var i = 0; i < _slots.Count && remaining > 0; i++)
        {
            if (_slots[i] is not InventoryStackEntry stackEntry || !CanMergeDefinitionIntoStack(stackEntry.Stack, item))
                continue;

            remaining = stackEntry.Stack.AddQuantity(remaining);
        }

        for (var i = 0; i < _slots.Count && remaining > 0; i++)
        {
            if (_slots[i] != null)
                continue;

            var stackAmount = Math.Min(item.MaxStackSize, remaining);
            _slots[i] = new InventoryStackEntry(new InventoryStack(item, stackAmount));
            remaining -= stackAmount;
        }

        return remaining;
    }

    private int GetRemainingQuantityAfterAdd(InventoryItemDefinition item, int quantity)
    {
        if (item == null)
            return Math.Max(0, quantity);

        var remaining = Math.Max(0, quantity);
        if (remaining == 0)
            return 0;

        foreach (var entry in _slots)
        {
            if (entry is not InventoryStackEntry stackEntry || !CanMergeDefinitionIntoStack(stackEntry.Stack, item))
                continue;

            remaining -= Math.Min(stackEntry.Stack.AvailableSpace, remaining);
            if (remaining == 0)
                return 0;
        }

        foreach (var entry in _slots)
        {
            if (entry != null)
                continue;

            remaining -= Math.Min(item.MaxStackSize, remaining);
            if (remaining == 0)
                return 0;
        }

        return remaining;
    }

    private static bool CanMergeDefinitionIntoStack(InventoryStack stack, InventoryItemDefinition item)
    {
        if (stack?.Item == null || item == null || stack.AvailableSpace <= 0)
            return false;

        return InventoryStackEntry.DefinitionsMatch(stack.Item, item);
    }

    private void ResizeSlots(int slotCapacity, bool emitChanged)
    {
        var targetCapacity = Math.Max(1, slotCapacity);

        if (_slots.Count > targetCapacity)
        {
            for (var i = targetCapacity; i < _slots.Count; i++)
            {
                if (_slots[i] == null)
                    continue;

                GD.PushWarning($"{nameof(InventoryController)} refused to shrink below occupied slots.");
                targetCapacity = _slots.Count;
                break;
            }
        }

        if (_slots.Count > targetCapacity)
            _slots.RemoveRange(targetCapacity, _slots.Count - targetCapacity);

        while (_slots.Count < targetCapacity)
            _slots.Add(null);

        _slotCapacity = _slots.Count;

        if (emitChanged)
            EmitInventoryChanged();
    }

    private void EmitInventoryChanged()
    {
        EmitSignal(SignalName.InventoryChanged);
    }
}
