using Godot;

using System;

// Button that surfaces a custom TooltipFactory tooltip built lazily at hover time.
// TooltipTextProvider supplies the hover text the engine uses to decide whether a
// tooltip exists (empty = none); TooltipBuilder supplies the rich tooltip control.
// With neither delegate set the button tooltips like a plain Button (TooltipText).
public partial class TooltipButton : Button
{
    public Func<string> TooltipTextProvider { get; set; }
    public Func<Control> TooltipBuilder { get; set; }

    public override string _GetTooltip(Vector2 atPosition)
    {
        return TooltipTextProvider != null
            ? TooltipTextProvider() ?? string.Empty
            : TooltipText;
    }

    public override Control _MakeCustomTooltip(string forText)
    {
        return TooltipBuilder?.Invoke();
    }
}
