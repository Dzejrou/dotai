using Godot;

[GlobalClass]
public abstract partial class GroundPlacedSpell : Spell, IPlacementSpell
{
    private AreaOfEffect _areaTemplate;
    private AreaOfEffect _previewArea;
    private Node2D _previewOrigin;

    public bool IsAwaitingPlacement { get; private set; }

    public override void _Ready()
    {
        _areaTemplate = FindAreaTemplate();
        if (_areaTemplate == null)
            GD.PushError($"{GetPath()}: {GetType().Name} requires an AreaOfEffect child template.");
    }

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
        return base.CanCast(caster) && ResolveAreaTemplate() != null && caster?.Faction != null;
    }

    public override bool TryCast(ISpellCaster caster)
    {
        if (TryResolvePlacementPosition(caster, out var worldPosition))
            return SpawnArea(caster, worldPosition);

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

        return SpawnArea(caster, worldPosition);
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

    protected virtual bool TryResolvePlacementPosition(ISpellCaster caster, out Vector2 worldPosition)
    {
        worldPosition = default;
        return false;
    }

    protected virtual void ConfigureArea(AreaOfEffect area, ISpellCaster caster)
    {
    }

    private bool SpawnArea(ISpellCaster caster, Vector2 worldPosition)
    {
        if (!CanCast(caster))
        {
            CancelPlacement();
            return false;
        }

        var parent = caster?.SpellOrigin?.GetParent();
        if (parent == null)
        {
            CancelPlacement();
            return false;
        }

        if (ResolveAreaTemplate()?.Duplicate() is not AreaOfEffect area)
        {
            CancelPlacement();
            return false;
        }

        if (!TrySpendCastMana(caster))
        {
            area.QueueFree();
            CancelPlacement();
            return false;
        }

        ConfigureArea(area, caster);
        area.GlobalPosition = worldPosition;
        area.InitializeRuntime(caster.SpellOrigin, caster.Faction);
        parent.AddChild(area);

        StartCooldown();
        CancelPlacement();
        return true;
    }

    private bool ShowPreview(ISpellCaster caster)
    {
        if (caster?.SpellOrigin == null || caster.SpellOrigin.GetParent() == null)
            return false;

        if (ResolveAreaTemplate()?.Duplicate() is not AreaOfEffect previewArea)
            return false;

        previewArea.InitializePreview();
        previewArea.GlobalPosition = caster.SpellOrigin.GetGlobalMousePosition();
        caster.SpellOrigin.GetParent().AddChild(previewArea);
        _previewOrigin = caster.SpellOrigin;
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

    private AreaOfEffect ResolveAreaTemplate()
    {
        if (_areaTemplate != null && GodotObject.IsInstanceValid(_areaTemplate))
            return _areaTemplate;

        _areaTemplate = FindAreaTemplate();
        return _areaTemplate;
    }

    private AreaOfEffect FindAreaTemplate()
    {
        foreach (var child in GetChildren())
        {
            if (child is AreaOfEffect areaTemplate)
                return areaTemplate;
        }

        return null;
    }
}
