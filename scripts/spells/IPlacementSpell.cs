using Godot;

public interface IPlacementSpell
{
    bool IsAwaitingPlacement { get; }
    bool TryBeginPlacement(ISpellCaster caster);
    bool TryPlace(ISpellCaster caster, Vector2 worldPosition);
    void CancelPlacement();
}
