using Godot;

using System;

public enum GearLevelingReferenceKind
{
    Target,
    Material,
}

public enum GearLevelingSourceKind
{
    None,
    Inventory,
    Equipment,
}

public enum GearLevelingMaterialKind
{
    None,
    Crystal,
    GearFodder,
}

// Reference-only drop target used by the character window's leveling panel.
// Stores a pointer (inventory slot index or equipment slot enum) to where the
// referenced item actually lives; it never owns the item.
[GlobalClass]
public partial class GearLevelingReferenceSlot : PanelContainer
{
    public GearLevelingReferenceKind Kind { get; set; } = GearLevelingReferenceKind.Target;

    public InventoryController Inventory { get; set; }
    public EquipmentController Equipment { get; set; }

    public GearLevelingSourceKind SourceKind { get; private set; } = GearLevelingSourceKind.None;
    public int InventorySlotIndex { get; private set; } = -1;
    public EquipmentSlot EquipmentSlot { get; private set; }

    public Action ReferenceChanged { get; set; }
    public Action FocusRequested { get; set; }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
                FocusRequested?.Invoke();
            else if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                ClearReference();
                ReferenceChanged?.Invoke();
            }
        }
    }

    public void ClearReference()
    {
        SourceKind = GearLevelingSourceKind.None;
        InventorySlotIndex = -1;
    }

    public bool ResolveTargetGear(out GearInstance gear)
    {
        gear = null;
        if (Kind != GearLevelingReferenceKind.Target)
            return false;

        switch (SourceKind)
        {
            case GearLevelingSourceKind.Inventory:
                if (Inventory == null || !Inventory.TryGetEntry(InventorySlotIndex, out var entry))
                    return false;
                if (entry is not InventoryGearEntry gearEntry)
                    return false;
                gear = gearEntry.Gear;
                return gear != null;

            case GearLevelingSourceKind.Equipment:
                if (Equipment == null || !GodotObject.IsInstanceValid(Equipment))
                    return false;
                gear = Equipment.GetEquipped(EquipmentSlot);
                return gear != null;

            default:
                return false;
        }
    }

    // Resolves what's currently in the referenced inventory slot. Returns Crystal for
    // an arcane_crystal stack, GearFodder for any gear entry, and None if the source
    // vanished, isn't an inventory slot, or the entry no longer matches a supported
    // material kind. The matching out-parameter is populated; the other stays null.
    public GearLevelingMaterialKind ResolveMaterial(
        out InventoryStackEntry crystalStack,
        out InventoryGearEntry fodderEntry)
    {
        crystalStack = null;
        fodderEntry = null;

        if (Kind != GearLevelingReferenceKind.Material)
            return GearLevelingMaterialKind.None;
        if (SourceKind != GearLevelingSourceKind.Inventory)
            return GearLevelingMaterialKind.None;
        if (Inventory == null || !Inventory.TryGetEntry(InventorySlotIndex, out var entry))
            return GearLevelingMaterialKind.None;

        if (entry is InventoryStackEntry stackEntry)
        {
            var item = stackEntry.Stack?.Item;
            if (item != null &&
                string.Equals(item.Id, GearLevelingMaterials.ArcaneCrystalId, StringComparison.Ordinal))
            {
                crystalStack = stackEntry;
                return GearLevelingMaterialKind.Crystal;
            }
            return GearLevelingMaterialKind.None;
        }

        if (entry is InventoryGearEntry gearEntry && gearEntry.Gear != null)
        {
            fodderEntry = gearEntry;
            return GearLevelingMaterialKind.GearFodder;
        }

        return GearLevelingMaterialKind.None;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return Kind == GearLevelingReferenceKind.Target
            ? CanAcceptTarget(data)
            : CanAcceptMaterial(data);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var changed = Kind == GearLevelingReferenceKind.Target
            ? AcceptTarget(data)
            : AcceptMaterial(data);

        if (!changed)
            return;

        // Tell the source inventory slot its drag was consumed by us. Otherwise
        // InventoryWindow's drag-end handler will spawn a world drop because we
        // referenced the stack without removing it.
        if (data.VariantType == Variant.Type.Int)
            InventorySlotControl.NotifyDragConsumed(data.AsInt32());

        ReferenceChanged?.Invoke();
    }

    private bool CanAcceptTarget(Variant data)
    {
        if (data.VariantType == Variant.Type.Int)
        {
            if (Inventory == null)
                return false;
            return Inventory.TryGetEntry(data.AsInt32(), out var entry) &&
                   entry is InventoryGearEntry;
        }

        if (TryReadEquipmentPayload(data, out _))
            return Equipment != null && GodotObject.IsInstanceValid(Equipment);

        return false;
    }

    private bool AcceptTarget(Variant data)
    {
        if (data.VariantType == Variant.Type.Int)
        {
            var slot = data.AsInt32();
            if (Inventory == null || !Inventory.TryGetEntry(slot, out var entry) ||
                entry is not InventoryGearEntry)
                return false;

            SourceKind = GearLevelingSourceKind.Inventory;
            InventorySlotIndex = slot;
            return true;
        }

        if (TryReadEquipmentPayload(data, out var equipmentSlot))
        {
            SourceKind = GearLevelingSourceKind.Equipment;
            EquipmentSlot = equipmentSlot;
            InventorySlotIndex = -1;
            return true;
        }

        return false;
    }

    private bool CanAcceptMaterial(Variant data)
    {
        // Only inventory-origin drags. Equipment-origin gear is explicitly disallowed
        // as fodder (the user has to unequip it first).
        if (data.VariantType != Variant.Type.Int)
            return false;
        if (Inventory == null || !Inventory.TryGetEntry(data.AsInt32(), out var entry))
            return false;

        if (entry is InventoryStackEntry stackEntry)
        {
            var item = stackEntry.Stack?.Item;
            return item != null &&
                   string.Equals(item.Id, GearLevelingMaterials.ArcaneCrystalId, StringComparison.Ordinal);
        }

        // Any inventory gear is acceptable as fodder; the self-fodder check happens
        // at Enhance time against the target reference (this slot doesn't know the target).
        return entry is InventoryGearEntry;
    }

    private bool AcceptMaterial(Variant data)
    {
        if (!CanAcceptMaterial(data))
            return false;

        SourceKind = GearLevelingSourceKind.Inventory;
        InventorySlotIndex = data.AsInt32();
        return true;
    }

    private static bool TryReadEquipmentPayload(Variant data, out EquipmentSlot slot)
    {
        slot = default;
        if (data.VariantType != Variant.Type.Dictionary)
            return false;
        var dict = data.AsGodotDictionary();
        if (!dict.TryGetValue("source", out var source) || source.AsString() != "equipment")
            return false;
        if (!dict.TryGetValue("slot", out var slotValue))
            return false;
        slot = (EquipmentSlot)slotValue.AsInt32();
        return true;
    }

    public override Control _MakeCustomTooltip(string forText)
    {
        if (Kind == GearLevelingReferenceKind.Target && ResolveTargetGear(out var gear))
            return TooltipFactory.Build(gear);

        if (Kind == GearLevelingReferenceKind.Material)
        {
            var matKind = ResolveMaterial(out var crystalEntry, out var fodderEntry);
            if (matKind == GearLevelingMaterialKind.GearFodder && fodderEntry?.Gear != null)
                return TooltipFactory.Build(fodderEntry.Gear);
            if (matKind == GearLevelingMaterialKind.Crystal && crystalEntry?.Stack?.Item != null)
                return TooltipFactory.Build(crystalEntry.Stack.Item, crystalEntry.Stack.Quantity);
        }

        return null;
    }
}
