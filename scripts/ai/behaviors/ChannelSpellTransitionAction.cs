using Godot;

using System;

// Transition step: stand still and channel a configured child Spell for a fixed
// duration, casting it on a fixed tick interval. Each cast goes through the normal
// spell API (ProjectileSpell etc.) aimed at the actor's current target at that tick,
// so no projectile-spawning logic is duplicated here. The actor loops its casting
// animation and stays put for the whole channel.
//
// This is deliberately self-contained channel support for transitions; it does not
// add channel handling to SpellCastActionController.
[GlobalClass]
public partial class ChannelSpellTransitionAction : BossTransitionAction
{
    [Export]
    public float Duration { get; set; } = 10.0f;

    [Export]
    public float TickInterval { get; set; } = 0.5f;

    // Child Spell cast each tick. Falls back to the first child Spell when unset.
    [Export]
    public NodePath SpellPath { get; set; } = new NodePath();

    // Looping animation played for the whole channel (presentation only).
    [Export]
    public StringName CastingAnimation { get; set; } = "casting";

    // Optional label (e.g. "Fire Barrage") for later log/telegraph use.
    [Export]
    public string ChannelName { get; set; } = string.Empty;

    private Spell _spell;
    private bool _spellResolved;
    private float _elapsed;
    private float _tickTimer;
    private string _activeCastingAnimation;

    protected override void OnBegin(Actor actor)
    {
        _elapsed = 0.0f;
        // First volley fires after one interval rather than instantly on arrival.
        _tickTimer = ResolveTickInterval();
        _activeCastingAnimation = null;
        FaceTargetAndChannel(actor);
    }

    public override void Update(Actor actor, double delta)
    {
        if (actor == null)
            return;

        var step = Math.Max(0.0f, (float)delta);
        _elapsed += step;

        FaceTargetAndChannel(actor);

        _tickTimer -= step;
        if (_tickTimer <= 0.0f)
        {
            CastTick(actor);
            _tickTimer += ResolveTickInterval();
        }

        if (_elapsed >= Math.Max(0.0f, Duration))
            IsComplete = true;
    }

    public override ActorIntent BuildIntent(Actor actor)
    {
        // Stand still. The Channeling state stops Actor.ExecuteIntent from overriding
        // the casting loop with an idle animation; facing/animation are driven by
        // FaceTargetAndChannel in Update.
        return ActorIntent.Hold(CombatUnitState.Channeling);
    }

    protected override void OnCancel(Actor actor)
    {
        _elapsed = 0.0f;
        _tickTimer = 0.0f;
        _activeCastingAnimation = null;
    }

    private float ResolveTickInterval()
    {
        // Floor the interval so a misconfigured 0 cannot fire every frame.
        return Math.Max(0.05f, TickInterval);
    }

    private void CastTick(Actor actor)
    {
        if (actor is not ISpellCaster caster)
            return;

        var spell = ResolveSpell();
        spell?.TryCast(caster, BuildCastRequest(actor));
    }

    private SpellCastRequest BuildCastRequest(Actor actor)
    {
        var request = new SpellCastRequest();
        var target = actor.Target;
        if (Actor.IsStructurallyValidTarget(target))
        {
            request.TargetNode = target;
            request.TargetPosition = target.GlobalPosition;
            var toTarget = target.GlobalPosition - actor.GlobalPosition;
            if (toTarget != Vector2.Zero)
                request.Direction = toTarget.Normalized();
        }

        // Fallback so a missing/lost target still fires forward instead of failing.
        if (!request.Direction.HasValue || request.Direction.Value == Vector2.Zero)
            request.Direction = DirectionHelper.GetDirectionVector(actor.LastDirection);

        return request;
    }

    private void FaceTargetAndChannel(Actor actor)
    {
        var facing = ResolveFacing(actor);
        if (facing.HasValue)
            actor.SetFacingDirection(facing.Value);

        // (Re)start the casting loop only when the resolved directional animation
        // changes (e.g. the boss turned to track the target), so a looping animation
        // is never reset to its first frame every tick.
        var animationName = CastingAnimation.ToString();
        var resolved = actor.ResolveDirectionalAnimationName(animationName) ?? $"{animationName}_{actor.LastDirection}";
        if (_activeCastingAnimation == resolved)
            return;

        actor.TryPlayDirectionalAnimation(animationName, Math.Max(0.0f, actor.CastSpeedMultiplier));
        _activeCastingAnimation = resolved;
    }

    private Vector2? ResolveFacing(Actor actor)
    {
        var target = actor.Target;
        if (!Actor.IsStructurallyValidTarget(target))
            return null;

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        return toTarget != Vector2.Zero ? toTarget : (Vector2?)null;
    }

    private Spell ResolveSpell()
    {
        if (_spellResolved)
            return _spell;

        _spellResolved = true;
        if (SpellPath != null && !SpellPath.IsEmpty)
            _spell = GetNodeOrNull<Spell>(SpellPath);

        _spell ??= FindFirstChildSpell(this);
        if (_spell == null)
            GD.PushWarning($"{GetPath()}: ChannelSpellTransitionAction has no Spell to cast (set SpellPath or add a child Spell).");

        return _spell;
    }

    private static Spell FindFirstChildSpell(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Spell spell)
                return spell;
        }

        return null;
    }
}
