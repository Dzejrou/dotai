using Godot;

using System;

[GlobalClass]
public abstract partial class StatusEffect : Node
{
    [Export]
    public float DurationSeconds { get; set; } = 5.0f;

    [Export]
    public float TickIntervalSeconds { get; set; } = 1.0f;

    public Node2D OwnerNode { get; private set; }
    public Node2D Source { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public float NextTickSeconds { get; private set; }
    public bool IsActive { get; private set; }

    public abstract StringName StatusKey { get; }

    internal void Start(Node2D owner, Node2D source)
    {
        OwnerNode = owner;
        Source = source;
        IsActive = true;
        ResetTiming();
        OnApplied();
    }

    internal void Refresh(StatusEffect replacement, Node2D source)
    {
        CopyConfigurationFrom(replacement);
        Source = source;
        ResetTiming();
        OnRefreshed(replacement);
    }

    internal bool Tick(double delta)
    {
        if (!IsActive)
            return true;

        var durationSeconds = Math.Max(0.0f, DurationSeconds);
        ElapsedSeconds += (float)delta;

        if (TickIntervalSeconds > 0.0f)
        {
            while (NextTickSeconds > 0.0f &&
                   ElapsedSeconds >= NextTickSeconds &&
                   NextTickSeconds <= durationSeconds)
            {
                OnTick();
                NextTickSeconds += TickIntervalSeconds;
            }
        }

        return durationSeconds <= 0.0f || ElapsedSeconds >= durationSeconds;
    }

    internal void Stop(bool expired)
    {
        if (!IsActive)
            return;

        IsActive = false;
        OnRemoved(expired);
        OwnerNode = null;
        Source = null;
    }

    protected virtual void CopyConfigurationFrom(StatusEffect replacement)
    {
        DurationSeconds = replacement.DurationSeconds;
        TickIntervalSeconds = replacement.TickIntervalSeconds;
    }

    protected virtual void OnApplied()
    {
    }

    protected virtual void OnRefreshed(StatusEffect replacement)
    {
    }

    protected virtual void OnTick()
    {
    }

    protected virtual void OnRemoved(bool expired)
    {
    }

    private void ResetTiming()
    {
        ElapsedSeconds = 0.0f;
        NextTickSeconds = TickIntervalSeconds > 0.0f ? TickIntervalSeconds : float.PositiveInfinity;
    }
}
