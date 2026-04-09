using Godot;

public interface IPlacementSpell
{
    bool IsAwaitingPlacement { get; }
    bool TryBeginPlacement(ISpellCaster caster, SpellCastRequest request);
    bool TryPlace(ISpellCaster caster, SpellCastRequest request);
    void UpdatePlacementPreview(SpellCastRequest request);
    void CancelPlacement();
}
