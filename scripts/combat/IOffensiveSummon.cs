using Godot;

public interface IOffensiveSummon
{
    void CommandAttackTarget(Node2D target, bool forceRetarget = false);
}
