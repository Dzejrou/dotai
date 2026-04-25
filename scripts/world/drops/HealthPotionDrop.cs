using Godot;

[GlobalClass]
public partial class HealthPotionDrop : Drop
{
    [Export]
    public int HealthRestoreAmount { get; set; } = 25;

    protected override bool TryApplyTo(Player player)
    {
        player.RestoreHealthFromDrop(HealthRestoreAmount);
        return true;
    }
}
