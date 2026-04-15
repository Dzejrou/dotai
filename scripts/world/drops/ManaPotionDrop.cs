using Godot;

[GlobalClass]
public partial class ManaPotionDrop : Drop
{
    [Export]
    public int ManaRestoreAmount { get; set; } = 100;

    protected override void ApplyTo(Player player)
    {
        player.RestoreManaFromDrop(ManaRestoreAmount);
    }
}
