using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class StatusEffectController : Node
{
    [Signal]
    public delegate void ChangedEventHandler();

    [Signal]
    public delegate void StatusVisualStateChangedEventHandler(StringName statusKey, StatusEffect effect, bool active);

    [Signal]
    public delegate void StatusFloatingTextRequestedEventHandler(string text, Color color);

    private static readonly Color DebuffAppliedColor = new(0.92f, 0.28f, 0.28f, 1.0f);
    private static readonly Color DebuffRemovedColor = new(0.42f, 0.92f, 0.42f, 1.0f);
    private static readonly Color BuffAppliedColor = new(0.42f, 0.92f, 0.42f, 1.0f);
    private static readonly Color BuffRemovedColor = new(0.92f, 0.28f, 0.28f, 1.0f);

    private static readonly RandomNumberGenerator ApplyChanceRng = CreateApplyChanceRng();

    private readonly Dictionary<(StringName StatusKey, ulong SourceId), StatusEffect> _activeEffects = new();
    private readonly Dictionary<StringName, int> _activeStatusCounts = new();
    private readonly HashSet<StringName> _immuneStatusKeys = new();
    private Node2D _owner;

    [Export]
    public string[] ImmuneStatusKeys { get; set; } = Array.Empty<string>();

    public override void _Ready()
    {
        _owner = GetParent() as Node2D;
        if (_owner == null)
            GD.PushError($"{GetPath()}: StatusEffectController requires a Node2D parent.");

        _immuneStatusKeys.Clear();
        foreach (var immuneKey in ImmuneStatusKeys)
        {
            if (!string.IsNullOrWhiteSpace(immuneKey))
                _immuneStatusKeys.Add(immuneKey);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_activeEffects.Count == 0)
            return;

        var effectsToRemove = new List<(StringName StatusKey, ulong SourceId, StatusEffect Effect)>();

        foreach (var pair in _activeEffects)
        {
            var effect = pair.Value;
            if (effect == null || !GodotObject.IsInstanceValid(effect))
            {
                effectsToRemove.Add((pair.Key.StatusKey, pair.Key.SourceId, effect));
                continue;
            }

            if (effect.Tick(delta))
                effectsToRemove.Add((pair.Key.StatusKey, pair.Key.SourceId, effect));
        }

        foreach (var entry in effectsToRemove)
            RemoveEffect(entry.StatusKey, entry.SourceId, entry.Effect, expired: true);

        if (effectsToRemove.Count > 0)
            EmitSignal(SignalName.Changed);
    }

    public bool HasStatus(StringName statusKey)
    {
        return _activeStatusCounts.TryGetValue(statusKey, out var count) && count > 0;
    }

    public int GetStatusCount(StringName statusKey)
    {
        return _activeStatusCounts.TryGetValue(statusKey, out var count) ? count : 0;
    }

    public IEnumerable<StatusEffect> GetActiveStatusEffects()
    {
        var seenStatusKeys = new HashSet<StringName>();

        foreach (var effect in _activeEffects.Values)
        {
            if (effect == null || !GodotObject.IsInstanceValid(effect))
                continue;

            if (seenStatusKeys.Add(effect.StatusKey))
                yield return effect;
        }
    }

    public void RemoveStatus(StringName statusKey)
    {
        var effectsToRemove = new List<(StringName StatusKey, ulong SourceId, StatusEffect Effect)>();

        foreach (var pair in _activeEffects)
        {
            if (pair.Key.StatusKey == statusKey)
                effectsToRemove.Add((pair.Key.StatusKey, pair.Key.SourceId, pair.Value));
        }

        foreach (var entry in effectsToRemove)
            RemoveEffect(entry.StatusKey, entry.SourceId, entry.Effect, expired: false);

        if (effectsToRemove.Count > 0)
            EmitSignal(SignalName.Changed);
    }

    public void ClearAllEffects()
    {
        if (_activeEffects.Count == 0)
            return;

        var effectsToRemove = new List<(StringName StatusKey, ulong SourceId, StatusEffect Effect)>();
        foreach (var pair in _activeEffects)
            effectsToRemove.Add((pair.Key.StatusKey, pair.Key.SourceId, pair.Value));

        foreach (var entry in effectsToRemove)
            RemoveEffect(entry.StatusKey, entry.SourceId, entry.Effect, expired: false);

        EmitSignal(SignalName.Changed);
    }

    public void ApplyStatusEffect(StatusEffect effect, Node2D source = null, ulong sourceInstanceId = 0UL)
    {
        if (_owner == null || effect == null)
            return;

        if (GameSettings.GodMode && _owner is Player && effect.Category == StatusCategory.Debuff)
        {
            var statusName = string.IsNullOrWhiteSpace(effect.DisplayName)
                ? effect.StatusKey.ToString()
                : effect.DisplayName;
            CombatLog.Debug($"God mode blocks {statusName} on {ResolveOwnerDisplayName()}.");
            effect.QueueFree();
            return;
        }

        var applyChance = effect.ResolvedApplyChance;
        if (applyChance < 1.0f && (applyChance <= 0.0f || ApplyChanceRng.Randf() >= applyChance))
        {
            effect.QueueFree();
            return;
        }

        var statusKey = effect.StatusKey;
        if (IsStatusImmune(statusKey))
        {
            effect.QueueFree();
            return;
        }

        if (effect.IsUniqueByStatusKey && TryGetEffectByStatusKey(statusKey, out var existingUniqueEffect))
        {
            existingUniqueEffect.Effect.Refresh(effect, source, ResolveSourceInstanceId(source, sourceInstanceId));
            effect.QueueFree();
            EmitSignal(SignalName.Changed);
            return;
        }

        var sourceId = ResolveSourceInstanceId(source, sourceInstanceId);
        var effectKey = (StatusKey: statusKey, SourceId: sourceId);

        if (_activeEffects.TryGetValue(effectKey, out var existingEffect) &&
            existingEffect != null &&
            GodotObject.IsInstanceValid(existingEffect))
        {
            existingEffect.Refresh(effect, source, sourceId);
            effect.QueueFree();
            EmitSignal(SignalName.Changed);
            return;
        }

        if (effect.GetParent() != null)
            effect.GetParent().RemoveChild(effect);

        AddChild(effect);
        effect.Start(_owner, source, sourceId);
        _activeEffects[effectKey] = effect;
        AdjustStatusCount(statusKey, 1, effect);
        EmitStatusFloatingText(effect, applied: true);
        EmitSignal(SignalName.Changed);
    }

    public bool IsStatusImmune(StringName statusKey)
    {
        return _immuneStatusKeys.Contains(statusKey);
    }

    public void AddStatusImmunity(StringName statusKey)
    {
        if (statusKey == default)
            return;

        // TODO: a future shield spell can call this to grant poison immunity and other status blocks.
        _immuneStatusKeys.Add(statusKey);
    }

    public void RemoveStatusImmunity(StringName statusKey)
    {
        if (statusKey == default)
            return;

        _immuneStatusKeys.Remove(statusKey);
    }

    public float GetMovementSpeedMultiplier()
    {
        return ResolveStatusSpeedMultiplier(effect => effect.MovementSpeedMultiplier);
    }

    public bool CanMove()
    {
        foreach (var effect in _activeEffects.Values)
        {
            if (effect == null || !GodotObject.IsInstanceValid(effect))
                continue;

            if (effect.PreventsMovement)
                return false;
        }

        return true;
    }

    public float GetAttackSpeedMultiplier()
    {
        return ResolveStatusSpeedMultiplier(effect => effect.AttackSpeedMultiplier);
    }

    public float GetCastSpeedMultiplier()
    {
        return ResolveStatusSpeedMultiplier(effect => effect.CastSpeedMultiplier);
    }

    private void RemoveEffect(StringName statusKey, ulong sourceId, StatusEffect effect, bool expired)
    {
        var effectKey = (StatusKey: statusKey, SourceId: sourceId);
        if (_activeEffects.Remove(effectKey))
            AdjustStatusCount(statusKey, -1, effect);

        EmitStatusFloatingText(effect, applied: false);

        if (effect != null && GodotObject.IsInstanceValid(effect))
        {
            effect.Stop(expired);
            effect.QueueFree();
        }
    }

    private void AdjustStatusCount(StringName statusKey, int amount, StatusEffect effect)
    {
        var currentCount = GetStatusCount(statusKey);
        var nextCount = Math.Max(0, currentCount + amount);

        if (nextCount == 0)
            _activeStatusCounts.Remove(statusKey);
        else
            _activeStatusCounts[statusKey] = nextCount;

        if (currentCount <= 0 && nextCount > 0)
            EmitSignal(SignalName.StatusVisualStateChanged, statusKey, effect, true);
        else if (currentCount > 0 && nextCount <= 0)
            EmitSignal(SignalName.StatusVisualStateChanged, statusKey, effect, false);
    }

    private bool TryGetEffectByStatusKey(StringName statusKey, out (StringName StatusKey, ulong SourceId, StatusEffect Effect) effectEntry)
    {
        foreach (var pair in _activeEffects)
        {
            if (pair.Key.StatusKey != statusKey)
                continue;

            effectEntry = (pair.Key.StatusKey, pair.Key.SourceId, pair.Value);
            return true;
        }

        effectEntry = default;
        return false;
    }

    private float ResolveStatusSpeedMultiplier(Func<StatusEffect, float> selector)
    {
        var multiplier = 1.0f;

        foreach (var effect in _activeEffects.Values)
        {
            if (effect == null || !GodotObject.IsInstanceValid(effect))
                continue;

            multiplier *= Math.Max(0.0f, selector(effect));
        }

        return multiplier;
    }

    private static RandomNumberGenerator CreateApplyChanceRng()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return rng;
    }

    private static ulong ResolveSourceInstanceId(Node2D source, ulong sourceInstanceId)
    {
        if (sourceInstanceId != 0UL)
            return sourceInstanceId;

        if (source != null && GodotObject.IsInstanceValid(source))
            return source.GetInstanceId();

        return 0UL;
    }

    private void EmitStatusFloatingText(StatusEffect effect, bool applied)
    {
        if (effect == null)
            return;

        var displayName = effect.FloatingTextLabel?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(displayName))
            return;

        var prefix = applied ? "+" : "-";
        var color = ResolveStatusFloatingTextColor(effect.Category, applied);
        CallDeferred(nameof(EmitStatusFloatingTextDeferred), $"{prefix}{displayName}", color);

        CombatLog.Info(BuildStatusCombatLogText(displayName, applied));
    }

    private string BuildStatusCombatLogText(string displayName, bool applied)
    {
        var ownerName = ResolveOwnerDisplayName();
        var verb = applied ? "gains" : "loses";

        if (string.IsNullOrEmpty(ownerName))
            return $"{(applied ? "+" : "-")}{displayName}";

        return $"{ownerName} {verb} {displayName}.";
    }

    private string ResolveOwnerDisplayName()
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_owner))
            return string.Empty;

        var name = _owner.Name.ToString();
        return string.IsNullOrEmpty(name) ? string.Empty : name;
    }

    private void EmitStatusFloatingTextDeferred(string text, Color color)
    {
        if (!IsInsideTree())
            return;

        EmitSignal(SignalName.StatusFloatingTextRequested, text, color);
    }

    private static Color ResolveStatusFloatingTextColor(StatusCategory category, bool applied)
    {
        return category switch
        {
            StatusCategory.Buff => applied ? BuffAppliedColor : BuffRemovedColor,
            _ => applied ? DebuffAppliedColor : DebuffRemovedColor,
        };
    }
}
