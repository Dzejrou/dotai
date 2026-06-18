using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class CombatState : Node
{
    [Signal]
    public delegate void CombatStateChangedEventHandler(bool inCombat);

    public const float DefaultCombatTimeoutSeconds = 10.0f;

    private float _combatTimeRemaining;

    // Owner-scoped combat locks. While any lock is held the state stays in combat
    // regardless of the ordinary timeout, so a room encounter can hold both sides in
    // combat without periodically dealing damage. Keyed by owner instance id, so the
    // same owner acquiring repeatedly is idempotent.
    private readonly HashSet<ulong> _combatLockOwners = new();

    public Node2D Target { get; private set; }
    public bool HasCombatLock => _combatLockOwners.Count > 0;
    public bool InCombat => _combatTimeRemaining > 0.0f || HasCombatLock;
    public float CombatTimeRemaining => _combatTimeRemaining;

    public void Update(double delta)
    {
        if (!IsStructurallyValidTarget(Target))
            Target = null;

        if (_combatTimeRemaining <= 0.0f)
            return;

        var remaining = Math.Max(0.0f, _combatTimeRemaining - Math.Max(0.0f, (float)delta));
        if (remaining > 0.0f)
        {
            SetCombatTimeRemaining(remaining);
            return;
        }

        // The ordinary timeout elapsed. A held lock keeps the state in combat and
        // preserves the encounter target: only zero the timer so normal timeout/exit
        // behavior resumes once the final lock is released. Without a lock this is the
        // ordinary combat exit.
        if (HasCombatLock)
            SetCombatTimeRemaining(0.0f);
        else
            ExitCombat();
    }

    public void SetTarget(Node2D target)
    {
        Target = IsStructurallyValidTarget(target) ? target : null;
    }

    public void ClearTarget()
    {
        Target = null;
    }

    public void RefreshCombat(float durationSeconds = DefaultCombatTimeoutSeconds)
    {
        SetCombatTimeRemaining(Math.Max(0.0f, durationSeconds));
    }

    public void EnterCombat(Node2D target = null, float durationSeconds = DefaultCombatTimeoutSeconds)
    {
        if (target != null)
            SetTarget(target);

        RefreshCombat(durationSeconds);
    }

    public void RegisterOutgoingDamage(Node2D target, float durationSeconds = DefaultCombatTimeoutSeconds)
    {
        EnterCombat(target, durationSeconds);
    }

    public void RegisterIncomingDamage(Node2D source = null, bool setTargetToSource = false, float durationSeconds = DefaultCombatTimeoutSeconds)
    {
        if (setTargetToSource)
            EnterCombat(source, durationSeconds);
        else
            RefreshCombat(durationSeconds);
    }

    public void ExitCombat()
    {
        // Clear the target before emitting the leave transition so subscribers
        // observe a fully out-of-combat state.
        ClearTarget();
        SetCombatTimeRemaining(0.0f);
    }

    // Acquire an owner-scoped combat lock. While held, InCombat stays true regardless of
    // the ordinary timeout. An optional target is set/retained for the encounter (e.g.
    // pinning a boss onto the player). Emits CombatStateChanged only when this
    // acquisition actually transitions the state into combat.
    public void AcquireCombatLock(GodotObject owner, Node2D target = null)
    {
        if (owner == null)
            return;

        var wasInCombat = InCombat;
        _combatLockOwners.Add(owner.GetInstanceId());

        // Set the encounter target before emitting so a subscriber reacting to the
        // enter-combat transition already sees it.
        if (target != null)
            SetTarget(target);

        if (InCombat != wasInCombat)
            EmitSignal(SignalName.CombatStateChanged, InCombat);
    }

    // Release a previously acquired lock. Releasing a non-final lock, or releasing while
    // the ordinary timeout is still active, leaves the state in combat. Releasing the
    // final lock with no timeout remaining exits combat cleanly (clearing the target).
    // Emits CombatStateChanged only on a real state transition.
    public void ReleaseCombatLock(GodotObject owner)
    {
        if (owner == null)
            return;

        var wasInCombat = InCombat;
        if (!_combatLockOwners.Remove(owner.GetInstanceId()))
            return;

        // Other locks or an active timeout still hold the state in combat.
        if (HasCombatLock || _combatTimeRemaining > 0.0f)
            return;

        // Final lock gone and no timeout remaining: return cleanly to out-of-combat.
        ClearTarget();
        if (wasInCombat != InCombat)
            EmitSignal(SignalName.CombatStateChanged, InCombat);
    }

    // Single mutation point for the combat timer: the transition signal fires
    // exactly when InCombat flips, so refreshes (including zero-duration ones)
    // can never desync the public state from the emitted transitions.
    private void SetCombatTimeRemaining(float value)
    {
        var wasInCombat = InCombat;
        _combatTimeRemaining = value;
        if (InCombat != wasInCombat)
            EmitSignal(SignalName.CombatStateChanged, InCombat);
    }

    public static CombatState ResolveFor(Node node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return null;

        if (node is CombatState combatState)
            return combatState;

        return node.GetNodeOrNull<CombatState>("CombatState");
    }

    private static bool IsStructurallyValidTarget(Node2D target)
    {
        return target != null && GodotObject.IsInstanceValid(target) && target.IsInsideTree();
    }
}
