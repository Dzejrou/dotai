using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class SpellCastActionController : Node, ICombatActionController
{
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

    [Export]
    public float MinimumRange { get; set; } = 70.0f;

    [Export]
    public float PreferredRange { get; set; } = 120.0f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cast";

    [Export]
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public Godot.Collections.Array<AiSpellOption> SpellOptions { get; set; } = new();

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
    }

    public bool CanStartAction(Actor actor, Node2D target)
    {
        if (actor is not ISpellCaster caster || !IsValidHostileTarget(actor, target))
            return false;

        var request = CreateSpellCastRequest(actor, target);
        return TryResolveSpellOption(actor, caster, target, request, out _);
    }

    public void StartAction(Actor actor, Node2D target)
    {
        if (actor is not ISpellCaster caster)
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

        actor.SetState(CombatUnitState.Attacking);

        if (actor.TryPlayDirectionalAnimation(AttackAnimation.ToString(), AnimationSpeedMultiplier * Math.Max(0.0f, actor.CastSpeedMultiplier)))
        {
            _pendingOptionIndex = candidate.OptionIndex;
            _pendingSpell = candidate.Spell;
            _pendingRequest = request;
            return;
        }

        TryCast(actor, candidate.OptionIndex, candidate.Option, candidate.Spell, request);
        actor.FinishAttackState();
    }

    public bool HandleAnimationFinished(Actor actor, StringName animationName)
    {
        if (!animationName.ToString().StartsWith(AttackAnimation.ToString(), StringComparison.Ordinal))
            return false;

        if (_pendingOptionIndex >= 0 && _pendingSpell != null)
            TryCast(actor, _pendingOptionIndex, GetSpellOption(_pendingOptionIndex), _pendingSpell, _pendingRequest);

        ClearPendingCast();

        actor.FinishAttackState();
        return true;
    }

    public void Cancel(Actor actor)
    {
        ClearPendingCast();
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

        var totalWeight = 0.0f;
        foreach (var currentCandidate in EnumerateValidCandidates(actor, caster, target, request))
            totalWeight += currentCandidate.Option.Weight;

        if (!(totalWeight > 0.0f))
            return false;

        var roll = _random.Randf() * totalWeight;
        var cumulativeWeight = 0.0f;
        SpellOptionCandidate fallbackCandidate = default;
        var hasFallbackCandidate = false;
        foreach (var currentCandidate in EnumerateValidCandidates(actor, caster, target, request))
        {
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

        if (option.RequiresHostileTarget && !IsValidHostileTarget(actor, target))
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

    private static bool IsValidHostileTarget(Actor actor, Node2D target)
    {
        if (actor == null || !Actor.IsStructurallyValidTarget(target))
            return false;

        if (target is not ITargetable targetable || !targetable.CanBeTargeted)
            return false;

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
