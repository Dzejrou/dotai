using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class SpellCastActionController : Node, ICombatActionController
{
    private enum CastPhase
    {
        // No cast in flight.
        None,

        // Counting down the haste-adjusted cast time before execution. The timer
        // here is the sole timing authority; the looping windup animation is
        // presentation only.
        Windup,

        // Spell already executed once; waiting for the optional release ('cast')
        // animation to finish before returning control to the AI.
        Release,
    }

    private readonly struct SpellOptionCandidate
    {
        public SpellOptionCandidate(int optionIndex, AiSpellOption option, Spell spell)
        {
            OptionIndex = optionIndex;
            Option = option;
            Spell = spell;
        }

        public int OptionIndex { get; }
        public AiSpellOption Option { get; }
        public Spell Spell { get; }
    }

    private readonly RandomNumberGenerator _random = new();
    private readonly HashSet<int> _warnedInvalidOptions = new();
    private readonly HashSet<StringName> _warnedMissingSpellIds = new();

    private int _pendingOptionIndex = -1;
    private Spell _pendingSpell;
    private SpellCastRequest _pendingRequest;
    private float[] _optionCooldownRemaining = Array.Empty<float>();

    private CastPhase _castPhase = CastPhase.None;
    private float _castElapsed;
    private float _castDuration;
    private string _activeReleaseAnimation;

    [Export]
    public float MinimumRange { get; set; } = 70.0f;

    [Export]
    public float PreferredRange { get; set; } = 120.0f;

    // Release/recovery animation requested once after the spell executes.
    [Export]
    public StringName AttackAnimation { get; set; } = "cast";

    // Looping windup animation played for the whole cast time. It is presentation
    // only: a missing animation shows the lazy fallback but never shortens or
    // skips the configured, timer-driven cast time.
    [Export]
    public StringName CastingAnimation { get; set; } = "casting";

    [Export]
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public Godot.Collections.Array<AiSpellOption> SpellOptions { get; set; } = new();

    // Busy from cast start until the release animation finishes (or until an
    // instant/missing-art cast finishes within the same frame). The actor stays
    // suppressed and a composite owner retains this controller while busy.
    public bool IsBusy => _castPhase != CastPhase.None;

    public override void _Ready()
    {
        MinimumRange = Math.Max(0.0f, MinimumRange);
        PreferredRange = Math.Max(MinimumRange, PreferredRange);
        AnimationSpeedMultiplier = Math.Max(0.0f, AnimationSpeedMultiplier);
        EnsureOptionCooldownCapacity();
    }

    public void Update(Actor actor, double delta)
    {
        EnsureOptionCooldownCapacity();

        for (var i = 0; i < _optionCooldownRemaining.Length; i++)
        {
            if (_optionCooldownRemaining[i] > 0.0f)
                _optionCooldownRemaining[i] = Math.Max(0.0f, _optionCooldownRemaining[i] - (float)delta);
        }

        if (_castPhase == CastPhase.Windup)
            AdvanceWindup(actor, delta);
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        // Refuse to begin another cast while one is already in flight.
        if (_castPhase != CastPhase.None)
            return false;

        if (actor is not ISpellCaster caster)
            return false;

        var request = CreateSpellCastRequest(actor, target);
        return TryResolveSpellOption(actor, caster, target, request, out _);
    }

    public void StartAction(Actor actor, Node2D target)
    {
        if (_castPhase != CastPhase.None || actor is not ISpellCaster caster)
            return;

        var request = CreateSpellCastRequest(actor, target);
        if (!TryResolveSpellOption(actor, caster, target, request, out var candidate))
        {
            if (target == null || !Actor.IsStructurallyValidTarget(target))
                actor.ClearTarget();
            return;
        }

        ClearPendingCast();

        var toTarget = target.GlobalPosition - actor.GlobalPosition;
        if (toTarget != Vector2.Zero)
            actor.SetFacingDirection(toTarget);

        // Lock the request (and its ground-target snapshot) at cast start so the
        // spell lands where the target was when the cast began, even if it moves.
        _pendingOptionIndex = candidate.OptionIndex;
        _pendingSpell = candidate.Spell;
        _pendingRequest = request;

        actor.SetState(CombatUnitState.Casting);
        actor.Velocity = Vector2.Zero;

        _castElapsed = 0.0f;
        _castDuration = Math.Max(0.0f, actor.ApplyHasteToDuration(candidate.Spell.CastTimeDuration));

        if (_castDuration <= 0.0f)
        {
            // Instant cast: still runs through the shared completion path so
            // revalidation/release/ownership behave identically.
            _castPhase = CastPhase.Windup;
            CompleteCast(actor);
            return;
        }

        _castPhase = CastPhase.Windup;
        actor.TryPlayDirectionalAnimation(CastingAnimation.ToString(), Math.Max(0.0f, actor.CastSpeedMultiplier));
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        // Animation completion is never the timing authority for execution. The
        // only thing a finished animation does is end the release presentation.
        if (_castPhase != CastPhase.Release ||
            _activeReleaseAnimation == null ||
            animationName.ToString() != _activeReleaseAnimation)
        {
            return false;
        }

        _castPhase = CastPhase.None;
        _activeReleaseAnimation = null;
        actor?.FinishAttackState();
        return true;
    }

    public void Cancel(Actor actor)
    {
        CancelCast();
    }

    private void AdvanceWindup(Actor actor, double delta)
    {
        if (actor == null || actor.IsDead)
        {
            CancelCast();
            return;
        }

        _castElapsed += Math.Max(0.0f, (float)delta);
        if (_castElapsed < _castDuration)
            return;

        CompleteCast(actor);
    }

    private void CompleteCast(Actor actor)
    {
        var optionIndex = _pendingOptionIndex;
        var spell = _pendingSpell;
        var request = _pendingRequest;
        var option = GetSpellOption(optionIndex);

        // Revalidate before executing so a stale pending cast cannot fire at a
        // dead/invalid target or without resources (closes #60). The captured
        // ground position survives, but caster/target validity and spell
        // availability must still hold.
        var executed = RevalidateForExecution(actor, option, spell, request) &&
                       TryCast(actor, optionIndex, option, spell, request);

        ClearPendingCast();

        // Present the release/recovery animation only after a real execution.
        // If the art is missing, the lazy fallback shows and we resume at once;
        // no animation callback is required to leave the cast.
        if (executed && TryPlayReleaseAnimation(actor))
        {
            _castPhase = CastPhase.Release;
            return;
        }

        _castPhase = CastPhase.None;
        _activeReleaseAnimation = null;
        actor?.FinishAttackState();
    }

    private bool RevalidateForExecution(Actor actor, AiSpellOption option, Spell spell, SpellCastRequest request)
    {
        if (actor == null || actor.IsDead || !actor.IsInsideTree())
            return false;

        if (actor is not ISpellCaster caster || !caster.CanCastSpells)
            return false;

        if (option == null || spell == null || !GodotObject.IsInstanceValid(spell))
            return false;

        // Range is intentionally not re-checked: a captured ground-target cast
        // keeps its snapshot even if the target left the original range. Relation
        // and structural validity must still hold, so a dead/removed target cancels.
        if (!IsTargetValidForRelation(actor, request?.TargetNode, option.TargetRelation))
            return false;

        return spell.CanCast(caster, request ?? SpellCastRequest.Empty);
    }

    private bool TryPlayReleaseAnimation(Actor actor)
    {
        if (actor == null)
            return false;

        // Resolve first so HandleAnimationFinished can match the exact playing
        // name; a missing release animation returns false (lazy fallback shown).
        var resolved = actor.ResolveDirectionalAnimationName(AttackAnimation.ToString());
        var speed = AnimationSpeedMultiplier * Math.Max(0.0f, actor.CastSpeedMultiplier);
        if (!actor.TryPlayDirectionalAnimation(AttackAnimation.ToString(), speed))
        {
            _activeReleaseAnimation = null;
            return false;
        }

        _activeReleaseAnimation = resolved;
        return true;
    }

    private void CancelCast()
    {
        ClearPendingCast();
        _castPhase = CastPhase.None;
        _castElapsed = 0.0f;
        _castDuration = 0.0f;
        _activeReleaseAnimation = null;
    }

    private void ClearPendingCast()
    {
        _pendingOptionIndex = -1;
        _pendingSpell = null;
        _pendingRequest = null;
    }

    private void EnsureOptionCooldownCapacity()
    {
        if (_optionCooldownRemaining.Length == SpellOptions.Count)
            return;

        var resized = new float[SpellOptions.Count];
        Array.Copy(_optionCooldownRemaining, resized, Math.Min(_optionCooldownRemaining.Length, resized.Length));
        _optionCooldownRemaining = resized;
    }

    private bool TryResolveSpellOption(
        Actor actor,
        ISpellCaster caster,
        Node2D target,
        SpellCastRequest request,
        out SpellOptionCandidate candidate)
    {
        candidate = default;
        if (actor == null || caster == null)
            return false;

        EnsureOptionCooldownCapacity();

        var highestPriority = int.MinValue;
        var totalWeight = 0.0f;
        foreach (var currentCandidate in EnumerateValidCandidates(actor, caster, target, request))
        {
            var currentPriority = currentCandidate.Option.Priority;
            if (currentPriority < highestPriority)
                continue;

            if (currentPriority > highestPriority)
            {
                highestPriority = currentPriority;
                totalWeight = 0.0f;
            }

            totalWeight += currentCandidate.Option.Weight;
        }

        if (!(totalWeight > 0.0f))
            return false;

        var roll = _random.Randf() * totalWeight;
        var cumulativeWeight = 0.0f;
        SpellOptionCandidate fallbackCandidate = default;
        var hasFallbackCandidate = false;
        foreach (var currentCandidate in EnumerateValidCandidates(actor, caster, target, request))
        {
            if (currentCandidate.Option.Priority != highestPriority)
                continue;

            cumulativeWeight += currentCandidate.Option.Weight;
            fallbackCandidate = currentCandidate;
            hasFallbackCandidate = true;
            if (roll < cumulativeWeight)
            {
                candidate = currentCandidate;
                return true;
            }
        }

        if (!hasFallbackCandidate)
            return false;

        candidate = fallbackCandidate;
        return true;
    }

    private IEnumerable<SpellOptionCandidate> EnumerateValidCandidates(
        Actor actor,
        ISpellCaster caster,
        Node2D target,
        SpellCastRequest request)
    {
        for (var i = 0; i < SpellOptions.Count; i++)
        {
            if (TryCreateCandidate(actor, caster, target, request, i, out var candidate))
                yield return candidate;
        }
    }

    private bool TryCreateCandidate(
        Actor actor,
        ISpellCaster caster,
        Node2D target,
        SpellCastRequest request,
        int optionIndex,
        out SpellOptionCandidate candidate)
    {
        candidate = default;
        var option = GetSpellOption(optionIndex);
        if (!IsValidOptionConfiguration(option, optionIndex))
            return false;

        if (!IsTargetValidForRelation(actor, target, option.TargetRelation))
            return false;

        if (!IsTargetInRange(caster, target, option))
            return false;

        if (IsOptionOnCooldown(optionIndex))
            return false;

        var spell = ResolveSpellById(actor, option.SpellId);
        if (spell == null)
            return false;

        if (!spell.CanCast(caster, request))
            return false;

        candidate = new SpellOptionCandidate(optionIndex, option, spell);
        return true;
    }

    private bool TryCast(Actor actor, int optionIndex, AiSpellOption option, Spell spell, SpellCastRequest request)
    {
        if (actor is not ISpellCaster caster || option == null || spell == null)
            return false;

        var didCast = spell.TryCast(caster, request ?? SpellCastRequest.Empty);
        if (didCast)
            StartOptionCooldown(optionIndex, option);

        return didCast;
    }

    private static SpellCastRequest CreateSpellCastRequest(Actor actor, Node2D target)
    {
        var request = new SpellCastRequest();
        if (Actor.IsStructurallyValidTarget(target))
        {
            request.TargetNode = target;
            request.TargetPosition = target.GlobalPosition;
            var toTarget = target.GlobalPosition - actor.GlobalPosition;
            if (toTarget != Vector2.Zero)
                request.Direction = toTarget.Normalized();
        }

        if (!request.Direction.HasValue || request.Direction.Value == Vector2.Zero)
            request.Direction = DirectionHelper.GetDirectionVector(actor.LastDirection);

        return request;
    }

    private Spell ResolveSpellById(Actor actor, StringName spellId)
    {
        if (actor == null || spellId.IsEmpty)
            return null;

        var spellLoadout = actor.GetNodeOrNull<SpellLoadout>("SpellLoadout");
        var equippedSpell = spellLoadout?.GetEquippedSpellById(spellId);
        if (equippedSpell != null)
            return equippedSpell;

        var spellBook = actor.GetNodeOrNull<SpellBook>("SpellBook");
        var spellTemplate = spellBook?.GetSpellTemplateById(spellId);
        if (spellTemplate != null)
            return spellTemplate;

        var spellsRoot = actor.GetNodeOrNull<Node>("Spells");
        var sceneSpell = FindSpellById(spellsRoot ?? actor, spellId.ToString());
        if (sceneSpell != null)
            return sceneSpell;

        if (_warnedMissingSpellIds.Add(spellId))
        {
            GD.PushWarning(
                $"{GetPath()}: AI spell option SpellId '{spellId}' could not be resolved from the actor spell loadout, spell book, or scene spells.");
        }

        return null;
    }

    private static Spell FindSpellById(Node node, string spellId)
    {
        if (node == null || string.IsNullOrWhiteSpace(spellId))
            return null;

        foreach (var child in node.GetChildren())
        {
            if (child is Spell spell &&
                string.Equals(spell.SpellId, spellId, StringComparison.Ordinal))
            {
                return spell;
            }

            if (child is Node childNode)
            {
                var nestedSpell = FindSpellById(childNode, spellId);
                if (nestedSpell != null)
                    return nestedSpell;
            }
        }

        return null;
    }

    private bool IsValidOptionConfiguration(AiSpellOption option, int optionIndex)
    {
        if (option == null)
            return false;

        if (option.SpellId.IsEmpty)
            return WarnInvalidOption(optionIndex, option, "SpellId is required.");

        if (!Enum.IsDefined(option.TargetRelation))
            return WarnInvalidOption(optionIndex, option, "TargetRelation must be a defined AiSpellTargetRelation value.");

        if (!float.IsFinite(option.Weight) || option.Weight <= 0.0f)
            return WarnInvalidOption(optionIndex, option, "Weight must be a finite value greater than 0.");

        if (!float.IsFinite(option.MinRange) ||
            !float.IsFinite(option.MaxRange) ||
            option.MinRange < 0.0f ||
            option.MaxRange < option.MinRange)
        {
            return WarnInvalidOption(optionIndex, option, "MinRange/MaxRange must define a finite non-negative range.");
        }

        if (!float.IsFinite(option.CooldownSeconds) || option.CooldownSeconds < 0.0f)
            return WarnInvalidOption(optionIndex, option, "CooldownSeconds must be a finite non-negative value.");

        return true;
    }

    private bool WarnInvalidOption(int optionIndex, AiSpellOption option, string message)
    {
        if (_warnedInvalidOptions.Add(optionIndex))
            GD.PushWarning($"{GetPath()}: AI spell option {GetOptionLabel(optionIndex, option)} is invalid. {message}");

        return false;
    }

    private static bool IsTargetValidForRelation(Actor actor, Node2D target, AiSpellTargetRelation targetRelation)
    {
        if (actor == null || !Actor.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

        return targetRelation switch
        {
            AiSpellTargetRelation.Hostile => IsValidHostileTarget(actor, target),
            AiSpellTargetRelation.Friendly => ReferenceEquals(actor, target) || actor.IsFriendlyTo(target),
            AiSpellTargetRelation.Self => ReferenceEquals(actor, target),
            AiSpellTargetRelation.Any => true,
            _ => false,
        };
    }

    private static bool IsValidHostileTarget(Actor actor, Node2D target)
    {
        var targetFactionState = FactionState.ResolveFor(target);
        return targetFactionState != null && targetFactionState.CanBeDamagedBy(actor.Faction);
    }

    private static bool IsTargetInRange(ISpellCaster caster, Node2D target, AiSpellOption option)
    {
        var spellOrigin = caster?.SpellOrigin;
        if (spellOrigin == null || !GodotObject.IsInstanceValid(spellOrigin) || !Actor.IsStructurallyValidTarget(target))
            return false;

        var distance = spellOrigin.GlobalPosition.DistanceTo(target.GlobalPosition);
        return distance >= option.MinRange && distance <= option.MaxRange;
    }

    private bool IsOptionOnCooldown(int optionIndex)
    {
        return optionIndex >= 0 &&
               optionIndex < _optionCooldownRemaining.Length &&
               _optionCooldownRemaining[optionIndex] > 0.0f;
    }

    private void StartOptionCooldown(int optionIndex, AiSpellOption option)
    {
        if (optionIndex < 0 ||
            optionIndex >= _optionCooldownRemaining.Length ||
            option == null ||
            option.CooldownSeconds <= 0.0f)
        {
            return;
        }

        _optionCooldownRemaining[optionIndex] = option.CooldownSeconds;
    }

    private AiSpellOption GetSpellOption(int optionIndex)
    {
        return optionIndex >= 0 && optionIndex < SpellOptions.Count
            ? SpellOptions[optionIndex]
            : null;
    }

    private static string GetOptionLabel(int optionIndex, AiSpellOption option)
    {
        if (option == null)
            return $"#{optionIndex}";

        return option.SpellId.IsEmpty ? $"#{optionIndex}" : $"#{optionIndex} ({option.SpellId})";
    }
}
