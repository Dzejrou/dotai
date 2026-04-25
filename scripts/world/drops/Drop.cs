using Godot;

using System;

[GlobalClass]
public partial class Drop : Area2D
{
    private const float SpawnMoveDurationSeconds = 0.16f;
    private const float SpawnHopPeakOffset = 8.0f;
    private const float SpawnHopUpDurationSeconds = 0.08f;
    private const float SpawnHopDownDurationSeconds = 0.1f;

    [Export]
    public Texture2D WorldSprite { get; set; }

    [Export(PropertyHint.Range, "0,1000,1")]
    public float AttractionSpeed { get; set; } = 240.0f;

    [Export(PropertyHint.Range, "0,4000,1")]
    public float AttractionAcceleration { get; set; } = 1400.0f;

    private Sprite2D _sprite;
    private CollisionShape2D _collisionShape;
    private Vector2 _baseSpritePosition;
    private Vector2 _attractionVelocity;
    private Player _attractionTarget;
    private bool _collected;
    private bool _hasSpawnMotion;
    private bool _isSpawnMotionPlaying;
    private Vector2 _spawnTargetPosition;

    public void ConfigureSpawnMotion(Vector2 startPosition, Vector2 targetPosition)
    {
        Position = startPosition;
        _spawnTargetPosition = targetPosition;
        _hasSpawnMotion = true;
    }

    public void BeginAttraction(Player player)
    {
        if (_collected || player == null || !GodotObject.IsInstanceValid(player))
            return;

        _attractionTarget = player;
    }

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
            _baseSpritePosition = _sprite.Position;
        }

        BodyEntered += OnBodyEntered;

        if (_hasSpawnMotion)
            PlaySpawnMotion();
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

        if (_collected || _isSpawnMotionPlaying)
            return;

        UpdateAttraction((float)delta);
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

        if (!TryApplyTo(player))
            return;

        _collected = true;
        SetDeferred(Area2D.PropertyName.Monitoring, false);

        if (_collisionShape != null)
            _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

        QueueFree();
    }

    private void PlaySpawnMotion()
    {
        _isSpawnMotionPlaying = true;
        var moveTween = CreateTween();
        moveTween.TweenProperty(this, "position", _spawnTargetPosition, SpawnMoveDurationSeconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        moveTween.Finished += OnSpawnMotionFinished;

        if (_sprite == null)
            return;

        var hopTween = CreateTween();
        hopTween.TweenProperty(_sprite, "position", _baseSpritePosition + new Vector2(0.0f, -SpawnHopPeakOffset), SpawnHopUpDurationSeconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        hopTween.TweenProperty(_sprite, "position", _baseSpritePosition, SpawnHopDownDurationSeconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
    }

    private void UpdateAttraction(float delta)
    {
        if (_attractionTarget == null || !GodotObject.IsInstanceValid(_attractionTarget) || !_attractionTarget.IsInsideTree())
        {
            _attractionTarget = null;
            _attractionVelocity = Vector2.Zero;
            return;
        }

        var toTarget = _attractionTarget.GlobalPosition - GlobalPosition;
        if (toTarget == Vector2.Zero)
            return;

        var desiredVelocity = toTarget.Normalized() * Math.Max(0.0f, AttractionSpeed);
        _attractionVelocity = _attractionVelocity.MoveToward(desiredVelocity, Math.Max(0.0f, AttractionAcceleration) * delta);

        var step = _attractionVelocity * delta;
        if (step.LengthSquared() >= toTarget.LengthSquared())
            GlobalPosition = _attractionTarget.GlobalPosition;
        else
            GlobalPosition += step;
    }

    private void OnSpawnMotionFinished()
    {
        _isSpawnMotionPlaying = false;
    }

    protected virtual bool TryApplyTo(Player player)
    {
        return true;
    }
}
