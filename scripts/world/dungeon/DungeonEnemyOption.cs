using Godot;

[GlobalClass]
public partial class DungeonEnemyOption : Resource
{
    [Export]
    public PackedScene EnemyScene { get; set; }

    [Export(PropertyHint.Range, "1,100,1")]
    public int Weight { get; set; } = 1;

    public bool IsConfigured => EnemyScene != null && Weight > 0;
}
