using Godot;

using System;

// TextureRect that surfaces the custom spell tooltip while hovered (action-bar spell
// slots). The spell is resolved lazily at hover time so the tooltip always reflects
// the currently equipped spell. Construction sites should use MouseFilter Pass so
// hover is detected while clicks keep falling through to world input handlers
// (e.g. armed placement spell casts).
public partial class SpellTooltipIcon : TextureRect
{
    public Func<Spell> SpellProvider { get; set; }

    // Non-empty hover text makes the engine treat the icon as tooltip-bearing and
    // route hover through _MakeCustomTooltip below.
    public override string _GetTooltip(Vector2 atPosition)
    {
        return ResolveSpell()?.DisplayLabel ?? string.Empty;
    }

    public override Control _MakeCustomTooltip(string forText)
    {
        return TooltipFactory.Build(ResolveSpell());
    }

    private Spell ResolveSpell()
    {
        var spell = SpellProvider?.Invoke();
        return spell != null && GodotObject.IsInstanceValid(spell) ? spell : null;
    }
}
