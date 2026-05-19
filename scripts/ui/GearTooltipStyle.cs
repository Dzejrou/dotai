using Godot;

[GlobalClass]
public partial class GearTooltipStyle : Resource
{
    [Export]
    public Color PanelBackground { get; set; } = new(0.06f, 0.06f, 0.08f, 0.98f);

    [Export]
    public Color PanelBorder { get; set; } = new(0.25f, 0.25f, 0.30f, 1.0f);

    [Export(PropertyHint.Range, "0,1024,1")]
    public int MinWidth { get; set; } = 320;

    [Export(PropertyHint.Range, "0,64,1")]
    public int PaddingLeft { get; set; } = 10;

    [Export(PropertyHint.Range, "0,64,1")]
    public int PaddingTop { get; set; } = 8;

    [Export(PropertyHint.Range, "0,64,1")]
    public int PaddingRight { get; set; } = 10;

    [Export(PropertyHint.Range, "0,64,1")]
    public int PaddingBottom { get; set; } = 8;

    [Export(PropertyHint.Range, "0,32,1")]
    public int CornerRadius { get; set; } = 3;

    [Export(PropertyHint.Range, "0,32,1")]
    public int LineSpacing { get; set; } = 2;

    [Export(PropertyHint.Range, "0,64,1")]
    public int NameFontSize { get; set; } = 0;

    [Export(PropertyHint.Range, "0,64,1")]
    public int BodyFontSize { get; set; } = 0;
}
