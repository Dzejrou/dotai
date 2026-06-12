using Godot;

using System;

[GlobalClass]
public partial class CombatState : Node
{
    [Signal]
    public delegate void CombatStateChangedEventHandler(bool inCombat);

    public const float DefaultCombatTimeoutSeconds = 10.0f;

    private float _combatTimeRemaining;

    public Node2D Target { get; private set; }
    public bool InCombat => _combatTimeRemaining > 0.0f;
    public float CombatTimeRemaining => _combatTimeRemaining;

    public void Update(double delta)
    {
        if (!IsStructurallyValidTarget(Target))
            Target = null;

        if (_combatTimeRemaining <= 0.0f)
            return;

        var remaining = Math.Max(0.0f, _combatTimeRemaining - Math.Max(0.0f, (float)delta));
        if (remaining <= 0.0f)
            ExitCombat();
        else
            SetCombatTimeRemaining(remaining);
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
