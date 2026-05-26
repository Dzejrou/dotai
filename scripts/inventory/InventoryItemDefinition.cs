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

    private int _maxStackSize = 99;
    private int _sellPrice;
    private int _gearXpPerUnit;
}
