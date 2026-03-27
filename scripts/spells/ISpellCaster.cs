using Godot;

public interface ISpellCaster : IFactionMember
{
    Node2D SpellOrigin { get; }
    string SpellDirectionName { get; }
    Vector2 SpellDirection { get; }
    Node2D SpellTarget { get; }
    ManaState ManaState { get; }
    bool CanCastSpells { get; }
    void NotifyManaChanged();
}
