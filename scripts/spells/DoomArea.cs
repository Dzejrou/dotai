using Godot;

[GlobalClass]
public partial class DoomArea : AreaOfEffect
{
    private static readonly StringName DefaultAnimationName = "default";

    private OmniSprite _omniSprite;
    private bool _hasResolved;

    protected override void OnAreaReady()
    {
        _omniSprite ??= GetNodeOrNull<OmniSprite>("OmniSprite");

        if (!IsPreviewMode)
            _omniSprite?.TryPlay(DefaultAnimationName);
    }

    protected override void OnBodyEntered(Node2D body)
    {
    }

    protected override void OnTick()
    {
    }

    protected override void OnAnimationFinished()
    {
        if (_hasResolved)
            return;

        _hasResolved = true;
        ResolveCurrentOverlaps();
        QueueFree();
    }

    private void ResolveCurrentOverlaps()
    {
        foreach (var body in GetOverlappingBodies())
        {
            if (body is not Node2D targetNode ||
                !GodotObject.IsInstanceValid(targetNode) ||
                !targetNode.IsInsideTree() ||
                targetNode == DamageSourceNode)
            {
                continue;
            }

            var targetFactionState = FactionState.ResolveFor(targetNode);
            if (targetFactionState == null || !targetFactionState.CanBeDamagedBy(SourceFaction))
                continue;

            if (targetNode is not IAttackable attackable)
                continue;

            if (Damage.DuplicateFrom(this) is not Damage damagePayload)
                continue;

            var damageSource = DamageSourceNode != null && GodotObject.IsInstanceValid(DamageSourceNode)
                ? (Node)DamageSourceNode
                : this;

            damagePayload.InitializeRuntime(damageSource);
            attackable.ApplyDamage(damagePayload);
        }
    }
}
