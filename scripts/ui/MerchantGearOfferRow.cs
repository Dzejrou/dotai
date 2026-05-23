using Godot;

// HBoxContainer that exposes the same custom gear tooltip used by inventory and equipped
// gear. MerchantWindow groups the icon + name controls inside one of these so hovering
// either child triggers the tooltip via this parent.
//
// Future hidden-stats merchant work can add another property (e.g. a revealed-substat
// count) and forward it into a GearTooltipFactory overload without changing callers.
public partial class MerchantGearOfferRow : HBoxContainer
{
    public GearInstance Gear { get; set; }

    public override Control _MakeCustomTooltip(string forText)
    {
        return Gear?.Definition == null ? null : GearTooltipFactory.Build(Gear);
    }
}
