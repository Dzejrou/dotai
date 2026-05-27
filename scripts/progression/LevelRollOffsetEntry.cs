using Godot;

[GlobalClass]
public partial class LevelRollOffsetEntry : Resource
{
    [Export]
    public int LevelOffset { get; set; } = 0;

    [Export(PropertyHint.Range, "0,1000,1")]
    public float Weight { get; set; } = 1.0f;
}
