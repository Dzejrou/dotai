using Godot;

using System;

[GlobalClass]
public partial class RespawnTimer : Timer
{
    [Export(PropertyHint.Range, "0,3600,0.1,or_greater")]
    public float RespawnDelaySeconds { get; set; } = 10.0f;

    private ActorSpawnPoint _spawnPoint;
    private bool _timeoutBound;
    private bool _occupancyChangedBound;

    public override void _Ready()
    {
        OneShot = true;
    }

    public override void _EnterTree()
    {
        EnsureTimeoutConnected();

        _spawnPoint = GetParent() as ActorSpawnPoint;
        if (_spawnPoint == null)
        {
            GD.PushWarning($"{nameof(RespawnTimer)} '{Name}' must be a child of {nameof(ActorSpawnPoint)}. Disabling timer.");
            Stop();
            return;
        }

        EnsureOccupancyChangedConnected();

        if (!_spawnPoint.IsOccupied())
            RestartTimer();
    }

    public override void _ExitTree()
    {
        Stop();
        DisconnectOccupancyChanged();
        DisconnectTimeout();
        _spawnPoint = null;
    }

    private void EnsureTimeoutConnected()
    {
        if (_timeoutBound)
            return;

        Timeout += OnTimeout;
        _timeoutBound = true;
    }

    private void DisconnectTimeout()
    {
        if (!_timeoutBound)
            return;

        Timeout -= OnTimeout;
        _timeoutBound = false;
    }

    private void EnsureOccupancyChangedConnected()
    {
        if (_occupancyChangedBound || _spawnPoint == null || !GodotObject.IsInstanceValid(_spawnPoint))
            return;

        var occupancyChangedCallable = new Callable(this, nameof(OnParentOccupancyChanged));
        if (!_spawnPoint.IsConnected(ActorSpawnPoint.SignalName.OccupancyChanged, occupancyChangedCallable))
        {
            _spawnPoint.Connect(ActorSpawnPoint.SignalName.OccupancyChanged, occupancyChangedCallable);
            _occupancyChangedBound = true;
            return;
        }

        _occupancyChangedBound = true;
    }

    private void DisconnectOccupancyChanged()
    {
        if (!_occupancyChangedBound)
            return;

        if (_spawnPoint != null && GodotObject.IsInstanceValid(_spawnPoint))
        {
            var occupancyChangedCallable = new Callable(this, nameof(OnParentOccupancyChanged));
            if (_spawnPoint.IsConnected(ActorSpawnPoint.SignalName.OccupancyChanged, occupancyChangedCallable))
                _spawnPoint.Disconnect(ActorSpawnPoint.SignalName.OccupancyChanged, occupancyChangedCallable);
        }

        _occupancyChangedBound = false;
    }

    private void OnParentOccupancyChanged(bool occupied)
    {
        if (occupied)
        {
            Stop();
            return;
        }

        RestartTimer();
    }

    private void OnTimeout()
    {
        if (_spawnPoint == null || !GodotObject.IsInstanceValid(_spawnPoint))
            return;

        _spawnPoint.Respawn();
        if (!_spawnPoint.IsOccupied())
            RestartTimer();
    }

    private void RestartTimer()
    {
        WaitTime = Math.Max(0.01f, RespawnDelaySeconds);
        Stop();
        Start();
    }
}
