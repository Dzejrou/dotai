using Godot;

using System;

public enum StatusCategory
{
    Buff,
    Debuff,
}

[GlobalClass]
public abstract partial class StatusEffect : Node
{
    [Export]
    public float DurationSeconds { get; set; } = 5.0f;

    [Export]
    public float TickIntervalSeconds { get; set; } = 1.0f;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public StatusCategory Category { get; set; } = StatusCategory.Debuff;

    public Node2D OwnerNode { get; private set; }
    public Node2D Source { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public float NextTickSeconds { get; private set; }
    public bool IsActive { get; private set; }

    private float _expiresAtSeconds;

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
        var previousTickInterval = TickIntervalSeconds;
        var previousNextTickSeconds = NextTickSeconds;
        var currentTime = ElapsedSeconds;

        CopyConfigurationFrom(replacement);
        Source = source;

        _expiresAtSeconds = currentTime + Math.Max(0.0f, DurationSeconds);
        NextTickSeconds = CalculateRefreshedNextTickSeconds(
            currentTime,
            previousTickInterval,
            previousNextTickSeconds);
        OnRefreshed(replacement);
    }

    internal bool Tick(double delta)
    {
        if (!IsActive)
            return true;

        var deltaSeconds = Math.Max(0.0f, (float)delta);
        ElapsedSeconds += deltaSeconds;

        if (TickIntervalSeconds > 0.0f)
        {
            while (NextTickSeconds > 0.0f &&
                   ElapsedSeconds >= NextTickSeconds &&
                   NextTickSeconds <= _expiresAtSeconds)
            {
                OnTick();
                NextTickSeconds += TickIntervalSeconds;
            }
        }

        return _expiresAtSeconds <= 0.0f || ElapsedSeconds >= _expiresAtSeconds;
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
        DisplayName = replacement.DisplayName;
        Category = replacement.Category;
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
        _expiresAtSeconds = Math.Max(0.0f, DurationSeconds);
    }

    private float CalculateRefreshedNextTickSeconds(float currentTime, float previousTickInterval, float previousNextTickSeconds)
    {
        if (TickIntervalSeconds <= 0.0f)
            return float.PositiveInfinity;

        if (previousTickInterval > 0.0f && !float.IsInfinity(previousNextTickSeconds))
        {
            var previousRemainingSeconds = Math.Max(0.0f, previousNextTickSeconds - currentTime);
            var previousProgress = 1.0f - Math.Clamp(previousRemainingSeconds / previousTickInterval, 0.0f, 1.0f);
            return currentTime + Math.Max(0.0f, TickIntervalSeconds * (1.0f - previousProgress));
        }

        return currentTime + TickIntervalSeconds;
    }
}
