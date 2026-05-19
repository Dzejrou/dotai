using Godot;

[GlobalClass]
public partial class GearQualityRules : Resource
{
    [Export]
    public GearQuality Quality { get; set; } = GearQuality.Common;

    [Export(PropertyHint.Range, "1,40,1")]
    public int MaxLevel { get; set; } = 1;

    [Export(PropertyHint.Range, "0,8,1")]
    public int SubstatCount { get; set; } = 2;

    [Export(PropertyHint.Range, "1,10000,1")]
    public int XpPerLevel { get; set; } = 100;

    // Fixed substat values per stat id. Designers edit one row per substat in the inspector.
    [Export]
    public Godot.Collections.Array<GearStatValueEntry> SubstatValues { get; set; } = new();
}
