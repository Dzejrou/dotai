using Godot;

[GlobalClass]
public partial class ActorExperienceRewardLevelDifferenceEntry : Resource
{
    [Export]
    public int MinDifference { get; set; } = 0;

    [Export(PropertyHint.Range, "0,100,0.05,or_greater")]
    public float Multiplier { get; set; } = 1.0f;
}
