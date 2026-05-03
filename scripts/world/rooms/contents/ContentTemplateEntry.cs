using Godot;

[GlobalClass]
public partial class ContentTemplateEntry : Resource
{
    [Export]
    public PackedScene ContentScene { get; set; }

    [Export(PropertyHint.Range, "0,100,0.1,or_greater")]
    public float Weight { get; set; } = 1.0f;

    public bool IsConfigured => ContentScene != null && float.IsFinite(Weight) && Weight > 0.0f;
}
