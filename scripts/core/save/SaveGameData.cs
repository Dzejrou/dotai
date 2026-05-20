using System.Collections.Generic;

public sealed class SaveGameData
{
    public string Schema { get; set; } = SaveGameStore.SchemaTag;
    public int Version { get; set; } = SaveGameStore.CurrentVersion;
    public string Timestamp { get; set; } = string.Empty;
    public PlayerSaveData Player { get; set; }
    public InventorySaveData Inventory { get; set; }
    public Dictionary<string, GearInstanceSaveData> Equipment { get; set; } = new();
}

public sealed class PlayerSaveData
{
    public int Level { get; set; } = 1;
    public int CurrentExperience { get; set; }
    public int CurrentHealth { get; set; }
    public int CurrentMana { get; set; }
}

public sealed class InventorySaveData
{
    public int Gold { get; set; }
    public int GearXp { get; set; }
    public int SlotCapacity { get; set; }
    public List<InventorySlotSaveData> Slots { get; set; } = new();
}

public sealed class InventorySlotSaveData
{
    // "stack" or "gear"; null/empty for an empty slot.
    public string Type { get; set; }

    public string ItemId { get; set; }
    public string ItemResourcePath { get; set; }
    public int Quantity { get; set; }

    public GearInstanceSaveData Gear { get; set; }
}

public sealed class GearInstanceSaveData
{
    public string Slot { get; set; }
    public string Quality { get; set; }
    public int Level { get; set; } = 1;
    public int CurrentXp { get; set; }
    public List<GearStatModifierSaveData> MainStats { get; set; } = new();
    public List<GearStatModifierSaveData> Substats { get; set; } = new();
}

public sealed class GearStatModifierSaveData
{
    public string StatId { get; set; }
    public float Value { get; set; }
}
