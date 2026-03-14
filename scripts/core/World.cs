using Godot;

using System;

[GlobalClass]
public partial class World : Node2D
{
    private const float DefaultNavigationBlockerPadding = 2.0f;
    private const float DefaultNavigationAgentRadius = 5.0f;

    [Export]
    public NodePath PlayerPath { get; set; } = new NodePath("Player");

    [Export]
    public NodePath WorldNavigationPath { get; set; } = new NodePath("WorldNavigation");

    [Export]
    public Rect2 NavigationBounds { get; set; } = new Rect2(0.0f, 0.0f, 640.0f, 360.0f);

    [Export]
    public float NavigationBlockerPadding { get; set; } = DefaultNavigationBlockerPadding;

    [Export]
    public float NavigationAgentRadius { get; set; } = DefaultNavigationAgentRadius;

    [Export]
    public Godot.Collections.Array<NodePath> NavigationBlockerPaths { get; set; } = new();

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void PlayerHealthChangedEventHandler(int health, int maxHealth);

    private Player _player;
    private NavigationRegion2D _worldNavigation;
    private bool _isGameOver;

    public override void _Ready()
    {
        _worldNavigation = GetNodeOrNull<NavigationRegion2D>(WorldNavigationPath);
        BuildWorldNavigation();

        _player = GetNodeOrNull<Player>(PlayerPath);
        if (_player != null)
        {
            _player.Connect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
            _player.Connect(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
            EmitSignal(SignalName.PlayerHealthChanged, _player.CurrentHealth, _player.MaxHealth);
        }
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_player) &&
            _player.IsConnected(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged))))
        {
            _player.Disconnect(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
        }

        if (GodotObject.IsInstanceValid(_player) &&
            _player.IsConnected(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied))))
        {
            _player.Disconnect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
        }
    }

    private void OnPlayerDied()
    {
        if (_isGameOver)
            return;

        _isGameOver = true;
        EmitSignal(SignalName.PlayerDied);
    }

    private void OnPlayerHealthChanged(int health, int maxHealth)
    {
        EmitSignal(SignalName.PlayerHealthChanged, health, maxHealth);
    }

    private void BuildWorldNavigation()
    {
        if (_worldNavigation == null)
            return;

        var navigationPolygon = new NavigationPolygon();
        navigationPolygon.AgentRadius = Math.Max(0.0f, NavigationAgentRadius);
        var sourceGeometryData = new NavigationMeshSourceGeometryData2D();
        sourceGeometryData.AddTraversableOutline(CreateOuterOutline());

        foreach (var blockerPath in NavigationBlockerPaths)
        {
            var blocker = GetNodeOrNull<Node2D>(blockerPath);
            if (blocker == null)
                continue;

            foreach (var collisionShape in FindCollisionShapes(blocker))
            {
                var blockerOutline = CreateBlockerOutline(collisionShape);
                if (blockerOutline.Length < 3)
                    continue;

                sourceGeometryData.AddObstructionOutline(blockerOutline);
            }
        }

        NavigationServer2D.BakeFromSourceGeometryData(navigationPolygon, sourceGeometryData);
        _worldNavigation.NavigationPolygon = navigationPolygon;
    }

    private Vector2[] CreateOuterOutline()
    {
        var bounds = NavigationBounds;
        return
        [
            new Vector2(bounds.Position.X, bounds.Position.Y),
            new Vector2(bounds.End.X, bounds.Position.Y),
            new Vector2(bounds.End.X, bounds.End.Y),
            new Vector2(bounds.Position.X, bounds.End.Y),
        ];
    }

    private Vector2[] CreateBlockerOutline(CollisionShape2D collisionShape)
    {
        if (collisionShape?.Shape is not RectangleShape2D rectangleShape)
            return [];

        var padding = Math.Max(0.0f, NavigationBlockerPadding);
        var halfExtents = (rectangleShape.Size * 0.5f) + new Vector2(padding, padding);
        var transform = collisionShape.GlobalTransform;

        var topLeft = _worldNavigation.ToLocal(transform * new Vector2(-halfExtents.X, -halfExtents.Y));
        var topRight = _worldNavigation.ToLocal(transform * new Vector2(halfExtents.X, -halfExtents.Y));
        var bottomRight = _worldNavigation.ToLocal(transform * new Vector2(halfExtents.X, halfExtents.Y));
        var bottomLeft = _worldNavigation.ToLocal(transform * new Vector2(-halfExtents.X, halfExtents.Y));

        return [topLeft, bottomLeft, bottomRight, topRight];
    }

    private static Godot.Collections.Array<CollisionShape2D> FindCollisionShapes(Node root)
    {
        var collisionShapes = new Godot.Collections.Array<CollisionShape2D>();
        CollectCollisionShapes(root, collisionShapes);
        return collisionShapes;
    }

    private static void CollectCollisionShapes(Node node, Godot.Collections.Array<CollisionShape2D> collisionShapes)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is CollisionShape2D collisionShape)
                collisionShapes.Add(collisionShape);

            CollectCollisionShapes(child, collisionShapes);
        }
    }

}
