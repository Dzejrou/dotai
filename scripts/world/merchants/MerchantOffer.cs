// Runtime offer surfaced to the merchant UI. Origin indicates whether this offer came from
// the merchant definition's StaticOffers list (recreated on refresh) or DynamicOffers
// (rerolled on refresh). Either way it is rebuilt from scratch when MerchantStock.RebuildStock
// runs, so Purchased is always false on a freshly built offer.
public sealed class MerchantOffer
{
    public MerchantOfferKind Kind { get; init; }

    // Provenance: which list in MerchantDefinition produced this offer.
    public MerchantOfferOrigin Origin { get; init; }

    public int Price { get; init; }

    // Populated when Kind == StackItem.
    public InventoryItemDefinition StackItem { get; init; }
    public int StackQuantity { get; init; }

    // Populated when Kind == GeneratedGear. Pre-generated at RebuildStock time so the player
    // sees the actual rolled stats; the same GearInstance is transferred into inventory on
    // purchase.
    public GearInstance Gear { get; init; }

    // How many substats the merchant tooltip should reveal for this offer. Captured at
    // build time so future rules (per-offer reveal) keep working even if the underlying
    // MerchantOfferRule is mutated later. Defaults high enough to show every substat.
    public int RevealedSubstatCount { get; init; } = int.MaxValue;

    public bool Purchased { get; set; }

    public string DisplayName
    {
        get
        {
            return Kind switch
            {
                MerchantOfferKind.StackItem => StackItem?.DisplayName ?? string.Empty,
                MerchantOfferKind.GeneratedGear => Gear?.Definition?.DisplayName ?? string.Empty,
                _ => string.Empty,
            };
        }
    }

    public Godot.Texture2D Icon
    {
        get
        {
            return Kind switch
            {
                MerchantOfferKind.StackItem => StackItem?.Icon,
                MerchantOfferKind.GeneratedGear => Gear?.Definition?.Icon,
                _ => null,
            };
        }
    }
}

public enum MerchantOfferOrigin
{
    Static = 0,
    Dynamic = 1,
}
