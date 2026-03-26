using Godot;

public interface ISpellCaster : IFactionMember
{
    Node2D SpellOrigin { get; }
    string SpellDirectionName { get; }
    Node2D SpellTarget { get; }
    ManaState ManaState { get; }
    FactionState FactionState { get; }
    bool CanCastSpells { get; }
    void NotifyManaChanged();
}
