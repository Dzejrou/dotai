using Godot;

using System;

[GlobalClass]
public partial class CombatState : Node
{
    public const float DefaultCombatTimeoutSeconds = 5.0f;

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

        _combatTimeRemaining = Math.Max(0.0f, _combatTimeRemaining - Math.Max(0.0f, (float)delta));
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
        _combatTimeRemaining = Math.Max(0.0f, durationSeconds);
    }

    public void RegisterOutgoingDamage(Node2D target, float durationSeconds = DefaultCombatTimeoutSeconds)
    {
        SetTarget(target);
        RefreshCombat(durationSeconds);
    }

    public void RegisterIncomingDamage(Node2D source = null, bool setTargetToSource = false, float durationSeconds = DefaultCombatTimeoutSeconds)
    {
        if (setTargetToSource)
            SetTarget(source);

        RefreshCombat(durationSeconds);
    }

    public void ExitCombat()
    {
        _combatTimeRemaining = 0.0f;
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
