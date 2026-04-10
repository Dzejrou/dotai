using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class SpellBook : Node
{
    public IReadOnlyList<Spell> GetSpellTemplates()
    {
        var spells = new List<Spell>();
        foreach (var child in GetChildren())
        {
            if (child is Spell spell)
                spells.Add(spell);
        }

        return spells;
    }

    public Spell GetSpellTemplateById(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId))
            return null;

        foreach (var spell in GetSpellTemplates())
        {
            if (string.Equals(spell.SpellId, spellId, StringComparison.Ordinal))
                return spell;
        }

        return null;
    }

    public override void _Ready()
    {
        ValidateTemplates();
    }

    private void ValidateTemplates()
    {
        var spellIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in GetChildren())
        {
            if (child is not Spell spell)
            {
                GD.PushError($"{GetPath()}: SpellBook child {child.Name} must inherit Spell.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(spell.SpellId))
            {
                GD.PushWarning($"{spell.GetPath()}: SpellBook template is missing SpellId.");
                continue;
            }

            if (!spellIds.Add(spell.SpellId))
                GD.PushError($"{spell.GetPath()}: duplicate SpellId '{spell.SpellId}' in SpellBook.");
        }
    }
}
