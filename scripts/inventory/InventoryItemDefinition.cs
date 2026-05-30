using Godot;

using System;

[GlobalClass]
public partial class InventoryItemDefinition : Resource
{
    [Export]
    public string Id { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public Texture2D Icon { get; set; }

    [Export(PropertyHint.Range, "1,999,1")]
    public int MaxStackSize
    {
        get => _maxStackSize;
        set => _maxStackSize = Math.Max(1, value);
    }

    [Export]
    public InventoryKeyKind KeyKind { get; set; } = InventoryKeyKind.None;

    [Export]
    public ItemQuality Quality { get; set; } = ItemQuality.Common;

    // Per-unit merchant sell price. 0 means the item is unsellable.
    [Export(PropertyHint.Range, "0,99999,1")]
    public int SellPrice
    {
        get => _sellPrice;
        set => _sellPrice = Math.Max(0, value);
    }

    // Per-unit gear XP this stack item grants when used as a gear leveling material.
    // 0 means the item is not a gear leveling crystal.
    [Export(PropertyHint.Range, "0,1000000,1")]
    public int GearXpPerUnit
    {
        get => _gearXpPerUnit;
        set => _gearXpPerUnit = Math.Max(0, value);
    }

    [Export]
    public ConsumableKind ConsumableKind { get; set; } = ConsumableKind.None;

    [Export(PropertyHint.Range, "1,9999,1")]
    public int ConsumableAmountPerTick
    {
        get => _consumableAmountPerTick;
        set => _consumableAmountPerTick = Math.Max(1, value);
    }

    [Export(PropertyHint.Range, "0.1,120,0.1")]
    public float ConsumableDurationSeconds
    {
        get => _consumableDurationSeconds;
        set => _consumableDurationSeconds = Math.Max(0.1f, value);
    }

    [Export(PropertyHint.Range, "0.1,60,0.1")]
    public float ConsumableTickIntervalSeconds
    {
        get => _consumableTickIntervalSeconds;
        set => _consumableTickIntervalSeconds = Math.Max(0.1f, value);
    }

    private int _maxStackSize = 99;
    private int _sellPrice;
    private int _gearXpPerUnit;
    private int _consumableAmountPerTick = 1;
    private float _consumableDurationSeconds = 20.0f;
    private float _consumableTickIntervalSeconds = 2.0f;
}
