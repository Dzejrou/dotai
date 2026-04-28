using Godot;

using System;

[GlobalClass]
public abstract partial class TimedRoom : RoomScreen
{
    [Export(PropertyHint.Range, "1,300,0.1")]
    public float DurationSeconds { get; set; } = 30.0f;

    private World _world;
    private CountdownHUD _countdownHud;

    protected bool IsCleared { get; private set; }
    protected bool IsTimerActive { get; private set; }
    protected float TimeRemainingSeconds { get; private set; }

    public override void OnEnter()
    {
        base.OnEnter();
        if (IsCleared)
        {
            HideCountdownHud();
            return;
        }

        StartTimer();
    }

    public override void OnExit()
    {
        StopTimer();
        HideCountdownHud();
        base.OnExit();
    }

    public override void _ExitTree()
    {
        HideCountdownHud();
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (!IsTimerActive || IsCleared)
            return;

        if (IsTimedObjectiveCleared())
        {
            CompleteRoom();
            return;
        }

        TimeRemainingSeconds = Math.Max(0.0f, TimeRemainingSeconds - (float)delta);
        UpdateCountdownHud();

        if (IsTimedObjectiveCleared())
        {
            CompleteRoom();
            return;
        }

        if (TimeRemainingSeconds > 0.0f)
            return;

        IsTimerActive = false;
        HideCountdownHud();
        OnTimerExpired();
    }

    protected void RestartTimer()
    {
        StartTimer();
    }

    protected void CompleteRoom()
    {
        if (IsCleared)
            return;

        IsCleared = true;
        StopTimer();
        HideCountdownHud();
        OnTimedRoomCleared();
    }

    protected virtual string GetCountdownTitle()
    {
        return !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : "Countdown";
    }

    protected abstract bool IsTimedObjectiveCleared();

    protected abstract void OnTimedRoomCleared();

    protected abstract void OnTimerExpired();

    private void StartTimer()
    {
        IsTimerActive = true;
        TimeRemainingSeconds = Math.Max(0.1f, DurationSeconds);
        UpdateCountdownHud();
    }

    private void StopTimer()
    {
        IsTimerActive = false;
        TimeRemainingSeconds = Math.Max(0.0f, TimeRemainingSeconds);
    }

    private void UpdateCountdownHud()
    {
        ResolveCountdownHud()?.ShowCountdown(GetCountdownTitle(), TimeRemainingSeconds);
    }

    private void HideCountdownHud()
    {
        ResolveCountdownHud()?.HideCountdown();
    }

    private CountdownHUD ResolveCountdownHud()
    {
        if (_countdownHud != null && GodotObject.IsInstanceValid(_countdownHud))
            return _countdownHud;

        _world ??= FindWorld();
        _countdownHud = _world?.ResolveCountdownHud();
        return _countdownHud;
    }

    private World FindWorld()
    {
        var current = GetParent();
        while (current != null)
        {
            if (current is World world)
                return world;

            current = current.GetParent();
        }

        return null;
    }
}
