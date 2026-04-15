using Godot;

[GlobalClass]
public partial class GoldSackDrop : Drop
{
    [Export]
    public int GoldAmount { get; set; } = 5;

    protected override void ApplyTo(Player player)
    {
        player.AddGold(GoldAmount);
    }
}
