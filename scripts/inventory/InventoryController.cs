using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class InventoryController : Node
{
    [Signal]
    public delegate void InventoryChangedEventHandler();

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

    private readonly List<InventoryStack> _slots = new();
    private int _slotCapacity = 50;
    private bool _startingStacksApplied;

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

    public bool TryGetStack(int slotIndex, out InventoryStack stack)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
        {
            stack = null;
            return false;
        }

        stack = _slots[slotIndex];
        return stack != null;
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

    public int GetQuantityByKeyKind(InventoryKeyKind keyKind)
    {
        if (keyKind == InventoryKeyKind.None)
            return 0;

        var total = 0;
        foreach (var stack in _slots)
        {
            if (stack?.Item == null || stack.Item.KeyKind != keyKind)
                continue;

            total += stack.Quantity;
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
            var stack = _slots[i];
            if (stack?.Item == null || stack.Item.KeyKind != keyKind)
                continue;

            remaining -= stack.RemoveQuantity(remaining);
            if (stack.IsEmpty)
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
            var stack = _slots[i];
            if (stack == null || !CanMerge(stack, item))
                continue;

            remaining = stack.AddQuantity(remaining);
        }

        for (var i = 0; i < _slots.Count && remaining > 0; i++)
        {
            if (_slots[i] != null)
                continue;

            var stackAmount = Math.Min(item.MaxStackSize, remaining);
            _slots[i] = new InventoryStack(item, stackAmount);
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

        foreach (var stack in _slots)
        {
            if (stack == null || !CanMerge(stack, item))
                continue;

            remaining -= Math.Min(stack.AvailableSpace, remaining);
            if (remaining == 0)
                return 0;
        }

        foreach (var stack in _slots)
        {
            if (stack != null)
                continue;

            remaining -= Math.Min(item.MaxStackSize, remaining);
            if (remaining == 0)
                return 0;
        }

        return remaining;
    }

    private bool CanMerge(InventoryStack stack, InventoryItemDefinition item)
    {
        if (stack?.Item == null || item == null || stack.AvailableSpace <= 0)
            return false;

        if (ReferenceEquals(stack.Item, item))
            return true;

        if (!string.IsNullOrEmpty(stack.Item.Id) && stack.Item.Id == item.Id)
            return true;

        return !string.IsNullOrEmpty(stack.Item.ResourcePath) &&
            stack.Item.ResourcePath == item.ResourcePath;
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
