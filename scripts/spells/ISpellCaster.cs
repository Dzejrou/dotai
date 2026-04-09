using Godot;

public interface ISpellCaster : IFactionMember
{
    Node2D SpellOrigin { get; }
    ManaState ManaState { get; }
    bool CanCastSpells { get; }
    float CastSpeedMultiplier { get; }
    void NotifyManaChanged();
}
