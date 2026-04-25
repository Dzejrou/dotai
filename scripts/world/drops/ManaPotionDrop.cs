using Godot;

[GlobalClass]
public partial class ManaPotionDrop : Drop
{
    [Export]
    public int ManaRestoreAmount { get; set; } = 100;

    protected override bool TryApplyTo(Player player)
    {
        player.RestoreManaFromDrop(ManaRestoreAmount);
        return true;
    }
}
