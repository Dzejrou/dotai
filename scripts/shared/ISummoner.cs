using Godot;

public interface ISummoner
{
    Node2D SummonerNode { get; }
    bool IsSummonerActive { get; }
}
