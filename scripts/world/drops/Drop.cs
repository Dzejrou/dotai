using Godot;

[GlobalClass]
public partial class Drop : Area2D
{
    [Export]
    public Texture2D WorldSprite { get; set; }

    private Sprite2D _sprite;
    private CollisionShape2D _collisionShape;
    private bool _collected;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

        if (_sprite == null)
        {
            GD.PushError($"{GetPath()}: Drop is missing a Sprite2D child.");
        }
        else
        {
            _sprite.Texture = WorldSprite;
        }

        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_collected)
            return;

        foreach (var body in GetOverlappingBodies())
        {
            if (body is Player player)
            {
                Collect(player);
                break;
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
            Collect(player);
    }

    private void Collect(Player player)
    {
        if (_collected || player == null || !GodotObject.IsInstanceValid(player))
            return;

        _collected = true;
        SetDeferred(Area2D.PropertyName.Monitoring, false);

        if (_collisionShape != null)
            _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

        ApplyTo(player);
        QueueFree();
    }

    protected virtual void ApplyTo(Player player) { }
}
