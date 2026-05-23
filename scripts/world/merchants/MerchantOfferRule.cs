using Godot;

using System;

[GlobalClass]
public partial class MerchantOfferRule : Resource
{
    [Export]
    public MerchantOfferKind Kind { get; set; } = MerchantOfferKind.StackItem;

    [Export]
    public InventoryItemDefinition StackItem { get; set; }

    [Export]
    public int StackQuantity
    {
        get => _stackQuantity;
        set => _stackQuantity = Math.Max(1, value);
    }

    [Export]
    public MerchantOfferSlotMode SlotMode { get; set; } = MerchantOfferSlotMode.SpecificSlot;

    [Export]
    public EquipmentSlot GearSlot { get; set; } = EquipmentSlot.Head;

    [Export]
    public MerchantOfferQualityMode QualityMode { get; set; } = MerchantOfferQualityMode.SpecificQuality;

    [Export]
    public GearQuality GearQuality { get; set; } = GearQuality.Common;

    [Export]
    public int Price
    {
        get => _price;
        set => _price = Math.Max(0, value);
    }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float AppearanceChance
    {
        get => _appearanceChance;
        set => _appearanceChance = Math.Clamp(value, 0.0f, 1.0f);
    }

    // Number of substats visible in the merchant tooltip. Default 4 preserves the
    // pre-existing "reveal everything" behavior since current gear caps at 4 substats.
    // Values above the actual substat count just reveal all available substats; the
    // remainder render as "???" placeholders. Only used by GeneratedGear offers.
    [Export]
    public int RevealedSubstatCount
    {
        get => _revealedSubstatCount;
        set => _revealedSubstatCount = Math.Max(0, value);
    }

    private int _stackQuantity = 1;
    private int _price;
    private float _appearanceChance = 1.0f;
    private int _revealedSubstatCount = 4;
}
