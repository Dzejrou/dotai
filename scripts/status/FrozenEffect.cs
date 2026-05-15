using Godot;

[GlobalClass]
public partial class FrozenEffect : StatusEffect
{
    public static readonly StringName StatusKeyName = "frozen";
    private const string VisualNodeName = "__FrozenVisual";

    [Export]
    public Texture2D VisualTexture { get; set; } = GD.Load<Texture2D>("res://assets/frost_nova/frozen.png");

    [Export]
    public Vector2 VisualOffset { get; set; } = Vector2.Zero;

    [Export]
    public int VisualZIndex { get; set; } = 1;

    [Export]
    public float VisualScale { get; set; } = 1.0f;

    private Sprite2D _visual;

    public override StringName StatusKey => StatusKeyName;

    public override bool IsUniqueByStatusKey => true;

    public override bool PreventsMovement => true;

    protected override void OnApplied()
    {
        SpawnVisual();
    }

    protected override void OnRemoved(bool expired)
    {
        ClearVisual();
    }

    private void SpawnVisual()
    {
        if (OwnerNode == null || !GodotObject.IsInstanceValid(OwnerNode) || VisualTexture == null)
            return;

        if (_visual != null && GodotObject.IsInstanceValid(_visual))
            return;

        _visual = new Sprite2D
        {
            Name = VisualNodeName,
            Texture = VisualTexture,
            Position = VisualOffset,
            ZIndex = VisualZIndex,
            Scale = Vector2.One * Mathf.Max(0.01f, VisualScale),
        };
        OwnerNode.AddChild(_visual);
    }

    private void ClearVisual()
    {
        if (_visual != null && GodotObject.IsInstanceValid(_visual))
            _visual.QueueFree();
        _visual = null;
    }
}
