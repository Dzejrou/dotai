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

    [Signal]
    public delegate void GearXpChangedEventHandler(int totalGearXp);

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

    [Export]
    public InventoryItemCatalog ItemCatalog { get; set; }

    [Export]
    public GearGenerationRules GearGenerationRules { get; set; }

    private readonly List<InventoryEntry> _slots = new();
    private int _slotCapacity = 50;
    private bool _startingStacksApplied;
    private int _gold;
    private int _gearXp;

    public int Gold => _gold;

    public int GearXp => _gearXp;

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
        if (amount < 0)
            return false;

        if (amount == 0)
            return true;

        if (_gold < amount)
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

    public int AddGearXp(int amount)
    {
        if (amount <= 0)
            return 0;

        _gearXp += amount;
        EmitSignal(SignalName.GearXpChanged, _gearXp);
        return amount;
    }

    public bool TrySpendGearXp(int amount)
    {
        if (amount <= 0 || _gearXp < amount)
            return false;

        _gearXp -= amount;
        EmitSignal(SignalName.GearXpChanged, _gearXp);
        return true;
    }

    public void SetGearXpForDebugOrLoad(int amount)
    {
        var clamped = Math.Max(0, amount);
        // Always emit on bulk load so listeners refresh, even when the value didn't change.
        _gearXp = clamped;
        EmitSignal(SignalName.GearXpChanged, _gearXp);
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

    // Consumes up to `quantity` from a specific stack slot, but only if its item id matches
    // the expected id. Returns the actual amount consumed (0 if mismatched, empty, or out of range).
    // Empties the slot when the stack hits zero. Emits InventoryChanged on any change.
    public int TryConsumeFromStackSlot(int slotIndex, string expectedItemId, int quantity)
    {
        if (quantity <= 0)
            return 0;
        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return 0;
        if (_slots[slotIndex] is not InventoryStackEntry stackEntry)
            return 0;

        var item = stackEntry.Stack.Item;
        if (item == null)
            return 0;
        if (!string.IsNullOrEmpty(expectedItemId) &&
            !string.Equals(item.Id, expectedItemId, StringComparison.Ordinal))
            return 0;

        var consumed = stackEntry.Stack.RemoveQuantity(quantity);
        if (consumed > 0)
        {
            if (stackEntry.Stack.IsEmpty)
                _slots[slotIndex] = null;
            EmitInventoryChanged();
        }
        return consumed;
    }

    // Returns the index of the first stack slot whose item id matches, or -1 when
    // none is found. Gear entries are ignored: assignments operate on stacks only.
    public int FindFirstStackSlotByItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return -1;

        for (var i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] is not InventoryStackEntry stackEntry)
                continue;

            var item = stackEntry.Stack.Item;
            if (item != null && string.Equals(item.Id, itemId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    // Total quantity held across every stack slot matching the item id. Gear entries
    // are ignored.
    public int GetQuantityByItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;

        var total = 0;
        foreach (var entry in _slots)
        {
            if (entry is not InventoryStackEntry stackEntry)
                continue;

            var item = stackEntry.Stack.Item;
            if (item != null && string.Equals(item.Id, itemId, StringComparison.Ordinal))
                total += stackEntry.Stack.Quantity;
        }

        return total;
    }

    // Consumes a single unit of the given item id from the first matching stack slot.
    // Returns true when one unit was consumed.
    public bool TryConsumeOneByItemId(string itemId)
    {
        var slotIndex = FindFirstStackSlotByItemId(itemId);
        if (slotIndex < 0)
            return false;

        return TryConsumeFromStackSlot(slotIndex, itemId, 1) > 0;
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

    // Moves up to `amount` from a stack at fromSlot into toSlot. Full-stack drags
    // (amount >= source quantity) fall through to TryInteractSlots so swap/merge
    // semantics stay identical. Partial drags must target either an empty slot or
    // a matching stack with available space; partial drags onto any other entry
    // (gear, non-matching stack, full matching stack) are rejected without mutation.
    // Returns true when a change occurred (and emits InventoryChanged in that case).
    public bool TryMovePartialStack(int fromSlot, int toSlot, int amount)
    {
        if (amount <= 0) return false;
        if (fromSlot == toSlot) return false;
        if (fromSlot < 0 || fromSlot >= _slots.Count) return false;
        if (toSlot < 0 || toSlot >= _slots.Count) return false;

        if (_slots[fromSlot] is not InventoryStackEntry fromStack)
            return false;

        if (amount >= fromStack.Stack.Quantity)
            return TryInteractSlots(fromSlot, toSlot);

        var item = fromStack.Stack.Item;
        if (item == null)
            return false;

        var toEntry = _slots[toSlot];
        if (toEntry == null)
        {
            fromStack.Stack.RemoveQuantity(amount);
            _slots[toSlot] = new InventoryStackEntry(new InventoryStack(item, amount));
            EmitInventoryChanged();
            return true;
        }

        if (toEntry is InventoryStackEntry toStack &&
            InventoryStackEntry.DefinitionsMatch(toStack.Stack.Item, item))
        {
            var available = toStack.Stack.AvailableSpace;
            if (available <= 0)
                return false;

            var moved = Math.Min(amount, available);
            toStack.Stack.AddQuantity(moved);
            fromStack.Stack.RemoveQuantity(moved);
            EmitInventoryChanged();
            return true;
        }

        return false;
    }

    // Splits up to `amount` off a stack slot for callers that need the resulting
    // InventoryStack (e.g. spawning a world drop). Behaves like TakeEntry when
    // amount >= source quantity: empties the slot and returns the full stack.
    // Returns false (with taken=null) when the slot is empty, not a stack, or amount<=0.
    // Emits InventoryChanged on any successful split or take.
    public bool TryTakePartialStack(int slotIndex, int amount, out InventoryStack taken)
    {
        taken = null;
        if (amount <= 0) return false;
        if (slotIndex < 0 || slotIndex >= _slots.Count) return false;
        if (_slots[slotIndex] is not InventoryStackEntry stackEntry) return false;

        var stack = stackEntry.Stack;
        if (stack?.Item == null) return false;

        if (amount >= stack.Quantity)
        {
            taken = stack;
            _slots[slotIndex] = null;
            EmitInventoryChanged();
            return true;
        }

        stack.RemoveQuantity(amount);
        taken = new InventoryStack(stack.Item, amount);
        EmitInventoryChanged();
        return true;
    }

    // Places `stack` into a specific slot for callers that already own a detached
    // InventoryStack (e.g. the hub's trash buffer restoring its contents). Empty
    // targets receive the stack as-is. Matching stacks merge up to their capacity
    // and the leftover is returned through `remainder`. Mismatched non-empty targets
    // are rejected without mutation.
    public bool TryPlaceStackAtSlot(int slotIndex, InventoryStack stack, out InventoryStack remainder)
    {
        remainder = null;
        if (slotIndex < 0 || slotIndex >= _slots.Count) return false;
        if (stack?.Item == null || stack.Quantity <= 0) return false;

        var entry = _slots[slotIndex];
        if (entry == null)
        {
            _slots[slotIndex] = new InventoryStackEntry(stack);
            EmitInventoryChanged();
            return true;
        }

        if (entry is InventoryStackEntry toStack &&
            InventoryStackEntry.DefinitionsMatch(toStack.Stack.Item, stack.Item))
        {
            var available = toStack.Stack.AvailableSpace;
            if (available <= 0)
                return false;

            var moved = Math.Min(stack.Quantity, available);
            toStack.Stack.AddQuantity(moved);
            var leftover = stack.Quantity - moved;
            remainder = leftover > 0 ? new InventoryStack(stack.Item, leftover) : null;
            EmitInventoryChanged();
            return true;
        }

        return false;
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

    public InventorySaveData CreateSaveSnapshot()
    {
        var data = new InventorySaveData
        {
            Gold = _gold,
            GearXp = _gearXp,
            SlotCapacity = _slots.Count,
        };

        foreach (var slot in _slots)
            data.Slots.Add(SnapshotEntry(slot));

        return data;
    }

    public void LoadFromSnapshot(InventorySaveData data)
    {
        if (data == null)
            return;

        var capacityFromData = data.SlotCapacity > 0
            ? data.SlotCapacity
            : data.Slots?.Count ?? _slots.Count;
        var capacity = Math.Max(1, capacityFromData);

        _slots.Clear();
        while (_slots.Count < capacity)
            _slots.Add(null);
        _slotCapacity = _slots.Count;

        if (data.Slots != null)
        {
            for (var i = 0; i < data.Slots.Count && i < _slots.Count; i++)
                _slots[i] = RehydrateEntry(data.Slots[i], i);
        }

        // Suppress later starting-stack application; the save defines the inventory now.
        _startingStacksApplied = true;

        SetGoldForDebugOrLoad(data.Gold);
        SetGearXpForDebugOrLoad(data.GearXp);
        EmitInventoryChanged();
    }

    private static InventorySlotSaveData SnapshotEntry(InventoryEntry entry)
    {
        if (entry == null)
            return null;

        if (entry is InventoryStackEntry stackEntry && stackEntry.Stack?.Item != null)
        {
            return new InventorySlotSaveData
            {
                Type = "stack",
                ItemId = stackEntry.Stack.Item.Id ?? string.Empty,
                ItemResourcePath = stackEntry.Stack.Item.ResourcePath ?? string.Empty,
                Quantity = stackEntry.Stack.Quantity,
            };
        }

        if (entry is InventoryGearEntry gearEntry && gearEntry.Gear != null)
        {
            return new InventorySlotSaveData
            {
                Type = "gear",
                Gear = GearSaveSerializer.Serialize(gearEntry.Gear),
            };
        }

        return null;
    }

    private InventoryEntry RehydrateEntry(InventorySlotSaveData slotData, int slotIndex)
    {
        if (slotData == null || string.IsNullOrEmpty(slotData.Type))
            return null;

        if (string.Equals(slotData.Type, "stack", StringComparison.Ordinal))
        {
            var item = ItemCatalog?.Resolve(slotData.ItemId, slotData.ItemResourcePath);
            if (item == null)
            {
                GD.PushWarning(
                    $"{nameof(InventoryController)}: dropping slot {slotIndex}; unknown stack item id='{slotData.ItemId}' path='{slotData.ItemResourcePath}'.");
                return null;
            }

            var quantity = Math.Max(1, slotData.Quantity);
            return new InventoryStackEntry(new InventoryStack(item, quantity));
        }

        if (string.Equals(slotData.Type, "gear", StringComparison.Ordinal))
        {
            if (slotData.Gear == null)
            {
                GD.PushWarning(
                    $"{nameof(InventoryController)}: dropping slot {slotIndex}; gear entry has no data.");
                return null;
            }

            var gear = GearSaveSerializer.Rehydrate(slotData.Gear, GearGenerationRules);
            if (gear == null)
            {
                GD.PushWarning(
                    $"{nameof(InventoryController)}: dropping slot {slotIndex}; could not rehydrate gear.");
                return null;
            }

            return new InventoryGearEntry(gear);
        }

        GD.PushWarning(
            $"{nameof(InventoryController)}: dropping slot {slotIndex}; unknown entry type '{slotData.Type}'.");
        return null;
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
