using Godot;

using System;

[GlobalClass]
public partial class RingOfFireSpell : Spell, IPlacementSpell
{
    private RingOfFireArea _previewArea;
    private Node2D _previewOrigin;

    public RingOfFireSpell()
    {
        ManaCost = 30;
        Cooldown = 6.0f;
    }

    [Export]
    public PackedScene AreaScene { get; set; }

    [Export]
    public float Radius { get; set; } = 56.0f;

    [Export]
    public float Duration { get; set; } = 5.0f;

    [Export]
    public float TickInterval { get; set; } = 1.0f;

    [Export]
    public int DamagePerTick { get; set; } = 8;

    public bool IsAwaitingPlacement { get; private set; }
    public override void _Process(double delta)
    {
        base._Process(delta);

        if (!IsAwaitingPlacement || _previewArea == null)
            return;

        if (_previewOrigin == null || !GodotObject.IsInstanceValid(_previewOrigin))
        {
            CancelPlacement();
            return;
        }

        _previewArea.GlobalPosition = _previewOrigin.GetGlobalMousePosition();
    }

    public override bool CanCast(ISpellCaster caster)
    {
        if (!base.CanCast(caster) || AreaScene == null)
            return false;

        return caster.Faction != null;
    }

    public override bool TryCast(ISpellCaster caster)
    {
        return TryBeginPlacement(caster);
    }

    public bool TryBeginPlacement(ISpellCaster caster)
    {
        if (!CanCast(caster))
            return false;

        CleanupPreview();
        if (!ShowPreview(caster))
            return false;

        IsAwaitingPlacement = true;
        return true;
    }

    public bool TryPlace(ISpellCaster caster, Vector2 worldPosition)
    {
        if (!IsAwaitingPlacement)
            return false;

        if (!CanCast(caster))
        {
            CancelPlacement();
            return false;
        }

        var parent = caster.SpellOrigin.GetParent();
        if (parent == null)
        {
            CancelPlacement();
            return false;
        }

        var ringArea = AreaScene.Instantiate<RingOfFireArea>();
        if (ringArea == null)
        {
            CancelPlacement();
            return false;
        }

        if (!TrySpendCastMana(caster))
        {
            ringArea.QueueFree();
            CancelPlacement();
            return false;
        }

        parent.AddChild(ringArea);
        ringArea.GlobalPosition = worldPosition;
        ringArea.Initialize(caster.SpellOrigin, caster.Faction, Radius, Duration, TickInterval, DamagePerTick);

        StartCooldown();
        CancelPlacement();
        return true;
    }

    public void CancelPlacement()
    {
        IsAwaitingPlacement = false;
        CleanupPreview();
    }

    public override void _ExitTree()
    {
        CleanupPreview();
    }

    private bool ShowPreview(ISpellCaster caster)
    {
        if (caster?.SpellOrigin == null || caster.SpellOrigin.GetParent() == null)
            return false;

        var previewArea = AreaScene.Instantiate<RingOfFireArea>();
        if (previewArea == null)
            return false;

        _previewOrigin = caster.SpellOrigin;
        caster.SpellOrigin.GetParent().AddChild(previewArea);
        previewArea.InitializePreview(Radius);
        previewArea.GlobalPosition = _previewOrigin.GetGlobalMousePosition();
        _previewArea = previewArea;
        return true;
    }

    private void CleanupPreview()
    {
        if (_previewArea != null && GodotObject.IsInstanceValid(_previewArea))
            _previewArea.QueueFree();

        _previewArea = null;
        _previewOrigin = null;
    }
}
