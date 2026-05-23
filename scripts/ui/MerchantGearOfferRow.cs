using Godot;

// HBoxContainer that exposes the same custom gear tooltip used by inventory and equipped
// gear. MerchantWindow groups the icon + name controls inside one of these so hovering
// either child triggers the tooltip via this parent. RevealedSubstatCount lets the
// merchant offer hide some substats (rendered as "???") while the underlying gear is
// already fully rolled; default int.MaxValue preserves the old reveal-everything behavior.
public partial class MerchantGearOfferRow : HBoxContainer
{
    public GearInstance Gear { get; set; }
    public int RevealedSubstatCount { get; set; } = int.MaxValue;

    public override Control _MakeCustomTooltip(string forText)
    {
        return Gear?.Definition == null ? null : GearTooltipFactory.Build(Gear, RevealedSubstatCount);
    }
}
