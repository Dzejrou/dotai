using Godot;

using System;
using System.Collections.Generic;

[GlobalClass]
public partial class SpellLoadout : Node
{
    [Signal]
    public delegate void LoadoutChangedEventHandler();

    public static readonly StringName[] SlotActions =
    {
        "cast_spell1",
        "cast_spell2",
        "cast_spell3",
        "cast_spell4",
        "cast_spell5",
        "cast_spell6",
        "cast_spell7",
    };

    public override void _Ready()
    {
        EnsureSlotNodes();
    }

    public IReadOnlyList<StringName> GetSlotActions()
    {
        return SlotActions;
    }

    public Spell GetEquippedSpell(StringName slotAction)
    {
        var slotNode = GetSlotNode(slotAction, createIfMissing: false);
        if (slotNode == null)
            return null;

        foreach (var child in slotNode.GetChildren())
        {
            if (child is Spell spell)
                return spell;
        }

        return null;
    }

    public string GetAssignedSpellId(StringName slotAction)
    {
        return GetEquippedSpell(slotAction)?.SpellId ?? string.Empty;
    }

    public bool TryFindAssignedSlotAction(string spellId, out StringName slotAction)
    {
        slotAction = default;
        if (string.IsNullOrWhiteSpace(spellId))
            return false;

        foreach (var candidateSlotAction in SlotActions)
        {
            var equippedSpell = GetEquippedSpell(candidateSlotAction);
            if (equippedSpell != null &&
                string.Equals(equippedSpell.SpellId, spellId, StringComparison.Ordinal))
            {
                slotAction = candidateSlotAction;
                return true;
            }
        }

        return false;
    }

    public void ApplyDefaultAssignments(SpellBook spellBook)
    {
        if (spellBook == null)
            return;

        ClearAllSlots(emitSignal: false);
        foreach (var spellTemplate in spellBook.GetSpellTemplates())
        {
            if (spellTemplate == null || !IsValidSlotAction(spellTemplate.CastAction))
                continue;

            AssignSpell(spellTemplate, spellTemplate.CastAction, emitSignal: false);
        }

        EmitSignal(SignalName.LoadoutChanged);
    }

    public bool AssignSpell(Spell spellTemplate, StringName slotAction)
    {
        return AssignSpell(spellTemplate, slotAction, emitSignal: true);
    }

    public void ClearSlot(StringName slotAction)
    {
        if (!IsValidSlotAction(slotAction))
            return;

        ClearSlotInternal(slotAction);
        EmitSignal(SignalName.LoadoutChanged);
    }

    private bool AssignSpell(Spell spellTemplate, StringName slotAction, bool emitSignal)
    {
        if (spellTemplate == null || !GodotObject.IsInstanceValid(spellTemplate))
            return false;

        if (!IsValidSlotAction(slotAction))
        {
            GD.PushWarning($"{GetPath()}: invalid loadout slot '{slotAction}'.");
            return false;
        }

        // Reassignment intentionally recreates the runtime instance to reset cooldown/state.
        if (TryFindAssignedSlotAction(spellTemplate.SpellId, out var existingSlotAction))
            ClearSlotInternal(existingSlotAction);

        ClearSlotInternal(slotAction);

        if (spellTemplate.Duplicate() is not Spell equippedSpell)
            return false;

        equippedSpell.CastAction = slotAction;
        var slotNode = GetSlotNode(slotAction, createIfMissing: true);
        slotNode.AddChild(equippedSpell);

        if (emitSignal)
            EmitSignal(SignalName.LoadoutChanged);

        return true;
    }

    private void ClearAllSlots(bool emitSignal)
    {
        foreach (var slotAction in SlotActions)
            ClearSlotInternal(slotAction);

        if (emitSignal)
            EmitSignal(SignalName.LoadoutChanged);
    }

    private void ClearSlotInternal(StringName slotAction)
    {
        var slotNode = GetSlotNode(slotAction, createIfMissing: false);
        if (slotNode == null)
            return;

        foreach (var child in slotNode.GetChildren())
        {
            if (child is Node childNode)
            {
                slotNode.RemoveChild(childNode);
                childNode.QueueFree();
            }
        }
    }

    private void EnsureSlotNodes()
    {
        var knownSlots = new HashSet<StringName>(SlotActions);
        foreach (var child in GetChildren())
        {
            if (child is Node childNode && !knownSlots.Contains(childNode.Name))
                GD.PushWarning($"{GetPath()}: unexpected SpellLoadout child slot '{childNode.Name}'.");
        }

        foreach (var slotAction in SlotActions)
            GetSlotNode(slotAction, createIfMissing: true);
    }

    private bool IsValidSlotAction(StringName slotAction)
    {
        foreach (var validSlotAction in SlotActions)
        {
            if (validSlotAction == slotAction)
                return true;
        }

        return false;
    }

    private Node GetSlotNode(StringName slotAction, bool createIfMissing)
    {
        foreach (var child in GetChildren())
        {
            if (child is Node childNode && childNode.Name == slotAction)
                return childNode;
        }

        if (!createIfMissing)
            return null;

        var slotNode = new Node
        {
            Name = slotAction,
        };
        AddChild(slotNode);
        return slotNode;
    }
}
