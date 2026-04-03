using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class StatusEffectController : Node
{
    [Signal]
    public delegate void StatusVisualStateChangedEventHandler(StringName statusKey, bool active);

    [Signal]
    public delegate void StatusFloatingTextRequestedEventHandler(string text, Color color);

    private static readonly Color DebuffAppliedColor = new(0.92f, 0.28f, 0.28f, 1.0f);
    private static readonly Color DebuffRemovedColor = new(0.42f, 0.92f, 0.42f, 1.0f);
    private static readonly Color BuffAppliedColor = new(0.42f, 0.92f, 0.42f, 1.0f);
    private static readonly Color BuffRemovedColor = new(0.92f, 0.28f, 0.28f, 1.0f);

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
    }

    public override void _ExitTree()
    {
        ClearAllEffects();
    }

    public bool HasStatus(StringName statusKey)
    {
        return _activeStatusCounts.TryGetValue(statusKey, out var count) && count > 0;
    }

    public int GetStatusCount(StringName statusKey)
    {
        return _activeStatusCounts.TryGetValue(statusKey, out var count) ? count : 0;
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
    }

    public void ApplyStatusEffect(StatusEffect effect, Node2D source = null)
    {
        if (_owner == null || effect == null)
            return;

        var statusKey = effect.StatusKey;
        if (IsStatusImmune(statusKey))
        {
            effect.QueueFree();
            return;
        }

        var sourceId = source?.GetInstanceId() ?? 0UL;
        var effectKey = (StatusKey: statusKey, SourceId: sourceId);

        if (_activeEffects.TryGetValue(effectKey, out var existingEffect) &&
            existingEffect != null &&
            GodotObject.IsInstanceValid(existingEffect))
        {
            existingEffect.Refresh(effect, source);
            effect.QueueFree();
            return;
        }

        if (effect.GetParent() != null)
            effect.GetParent().RemoveChild(effect);

        AddChild(effect);
        effect.Start(_owner, source);
        _activeEffects[effectKey] = effect;
        AdjustStatusCount(statusKey, 1);
        EmitStatusFloatingText(effect, applied: true);
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

    private void RemoveEffect(StringName statusKey, ulong sourceId, StatusEffect effect, bool expired)
    {
        var effectKey = (StatusKey: statusKey, SourceId: sourceId);
        if (_activeEffects.Remove(effectKey))
            AdjustStatusCount(statusKey, -1);

        EmitStatusFloatingText(effect, applied: false);

        if (effect != null && GodotObject.IsInstanceValid(effect))
        {
            effect.Stop(expired);
            effect.QueueFree();
        }
    }

    private void AdjustStatusCount(StringName statusKey, int amount)
    {
        var currentCount = GetStatusCount(statusKey);
        var nextCount = Math.Max(0, currentCount + amount);

        if (nextCount == 0)
            _activeStatusCounts.Remove(statusKey);
        else
            _activeStatusCounts[statusKey] = nextCount;

        if (currentCount <= 0 && nextCount > 0)
            EmitSignal(SignalName.StatusVisualStateChanged, statusKey, true);
        else if (currentCount > 0 && nextCount <= 0)
            EmitSignal(SignalName.StatusVisualStateChanged, statusKey, false);
    }

    private void EmitStatusFloatingText(StatusEffect effect, bool applied)
    {
        if (effect == null)
            return;

        var displayName = string.IsNullOrWhiteSpace(effect.DisplayName)
            ? effect.StatusKey.ToString()
            : effect.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
            return;

        var prefix = applied ? "+" : "-";
        var color = ResolveStatusFloatingTextColor(effect.Category, applied);
        CallDeferred(nameof(EmitStatusFloatingTextDeferred), $"{prefix}{displayName.ToUpperInvariant()}", color);
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
