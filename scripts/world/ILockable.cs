using Godot;

public interface ILockable
{
    bool IsLocked { get; }
    bool TryUnlock(Node interactor);
    void UnlockExternal();
}
