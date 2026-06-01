using Godot;

using System;

// Persistent food/drink quick slot assignments, stored by item-definition id rather
// than by inventory slot so they survive stack movement, splitting, selling, trashing
// and inventory reordering. Lives as a child of Player, parallel to SpellLoadout.
[GlobalClass]
public partial class QuickConsumableLoadout : Node
{
    [Signal]
    public delegate void QuickConsumablesChangedEventHandler();

    private string _foodItemId = string.Empty;
    private string _drinkItemId = string.Empty;

    public string FoodItemId => _foodItemId;

    public string DrinkItemId => _drinkItemId;

    public string GetAssignedItemId(ConsumableKind kind)
    {
        return kind switch
        {
            ConsumableKind.Food => _foodItemId,
            ConsumableKind.Drink => _drinkItemId,
            _ => string.Empty,
        };
    }

    // Assigns the slot matching the definition's ConsumableKind. The inventory stack
    // is never mutated by assignment. Returns true when the stored id changed.
    public bool TryAssign(InventoryItemDefinition definition)
    {
        return definition != null && TryAssign(definition.ConsumableKind, definition);
    }

    // Assigns to the given slot only when the definition's kind matches it. Food slots
    // accept only Food, drink slots only Drink. Returns true when the stored id changed.
    public bool TryAssign(ConsumableKind slotKind, InventoryItemDefinition definition)
    {
        if (definition == null)
            return false;

        if (slotKind != ConsumableKind.Food && slotKind != ConsumableKind.Drink)
            return false;

        if (definition.ConsumableKind != slotKind)
            return false;

        if (string.IsNullOrEmpty(definition.Id))
            return false;

        return SetAssignedItemId(slotKind, definition.Id);
    }

    public bool Clear(ConsumableKind slotKind)
    {
        return SetAssignedItemId(slotKind, string.Empty);
    }

    // Applies loaded assignments in a single batch (one change signal). Callers are
    // responsible for resolving/validating ids; unresolved ids should be passed as
    // empty so the slot loads empty.
    public void ApplyLoadedAssignments(string foodItemId, string drinkItemId)
    {
        var food = foodItemId ?? string.Empty;
        var drink = drinkItemId ?? string.Empty;

        var changed = false;
        if (!string.Equals(_foodItemId, food, StringComparison.Ordinal))
        {
            _foodItemId = food;
            changed = true;
        }

        if (!string.Equals(_drinkItemId, drink, StringComparison.Ordinal))
        {
            _drinkItemId = drink;
            changed = true;
        }

        if (changed)
            EmitSignal(SignalName.QuickConsumablesChanged);
    }

    private bool SetAssignedItemId(ConsumableKind slotKind, string itemId)
    {
        var normalized = itemId ?? string.Empty;
        switch (slotKind)
        {
            case ConsumableKind.Food:
                if (string.Equals(_foodItemId, normalized, StringComparison.Ordinal))
                    return false;
                _foodItemId = normalized;
                break;
            case ConsumableKind.Drink:
                if (string.Equals(_drinkItemId, normalized, StringComparison.Ordinal))
                    return false;
                _drinkItemId = normalized;
                break;
            default:
                return false;
        }

        EmitSignal(SignalName.QuickConsumablesChanged);
        return true;
    }
}
