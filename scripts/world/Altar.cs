using Godot;

[GlobalClass]
public partial class Altar : WorldObject, IInteractable
{
    private const string DefaultVisualDirection = "south";
    private const string DefaultInteractionLabel = "Cleanse Poison";

    [Export(PropertyHint.Enum, "east,south-east,south,south-west,west,north-west,north,north-east")]
    public string VisualDirection { get; set; } = DefaultVisualDirection;

    [Export] public Texture2D EastTexture { get; set; }
    [Export] public Texture2D SouthEastTexture { get; set; }
    [Export] public Texture2D SouthTexture { get; set; }
    [Export] public Texture2D SouthWestTexture { get; set; }
    [Export] public Texture2D WestTexture { get; set; }
    [Export] public Texture2D NorthWestTexture { get; set; }
    [Export] public Texture2D NorthTexture { get; set; }
    [Export] public Texture2D NorthEastTexture { get; set; }

    public override void _Ready()
    {
        InitializeWorldObject();
        AddToGroup(InteractionGroups.Interactables);
        ApplyVisualState();
    }

    public bool CanInteract(Node interactor)
    {
        return interactor != null && interactor.IsInsideTree();
    }

    public string GetInteractionLabel(Node interactor)
    {
        return DefaultInteractionLabel;
    }

    public void Interact(Node interactor)
    {
        if (interactor == null || !interactor.IsInsideTree())
            return;

        var statusEffectController = interactor.GetNodeOrNull<StatusEffectController>("StatusEffectController");
        if (statusEffectController == null || !statusEffectController.HasStatus(PoisonedEffect.StatusKeyName))
            return;

        statusEffectController.RemoveStatus(PoisonedEffect.StatusKeyName);
    }

    private void ApplyVisualState()
    {
        if (VisualSprite == null)
            return;

        VisualSprite.Texture = ResolveVisualTexture();
        VisualSprite.Modulate = Colors.White;
    }

    private Texture2D ResolveVisualTexture()
    {
        var requestedDirection = string.IsNullOrWhiteSpace(VisualDirection) ? DefaultVisualDirection : VisualDirection;
        return requestedDirection switch
        {
            "east" => EastTexture,
            "south-east" => SouthEastTexture,
            "south" => SouthTexture,
            "south-west" => SouthWestTexture,
            "west" => WestTexture,
            "north-west" => NorthWestTexture,
            "north" => NorthTexture,
            "north-east" => NorthEastTexture,
            _ => SouthTexture,
        };
    }
}
