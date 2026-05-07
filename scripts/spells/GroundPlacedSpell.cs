using Godot;

[GlobalClass]
public abstract partial class GroundPlacedSpell : Spell, IPlacementSpell
{
    private AreaOfEffect _areaTemplate;
    private AreaOfEffect _previewArea;

    public bool IsAwaitingPlacement { get; private set; }

    public override void _Ready()
    {
        _areaTemplate = FindAreaTemplate();
        if (_areaTemplate == null)
            GD.PushError($"{GetPath()}: {GetType().Name} requires an AreaOfEffect child template.");
    }

    public override bool CanCast(ISpellCaster caster, SpellCastRequest request)
    {
        return base.CanCast(caster, request) && ResolveAreaTemplate() != null && caster?.Faction != null;
    }

    public override bool TryCast(ISpellCaster caster, SpellCastRequest request)
    {
        return TryCast(caster, request, out _);
    }

    public override bool TryCast(ISpellCaster caster, SpellCastRequest request, out SpellCastResult result)
    {
        result = null;
        if (TryResolvePlacementPosition(caster, request, out var worldPosition))
            return SpawnArea(caster, worldPosition, request?.OwnRuntimeNodesForChannel ?? false, out result);

        return LogMissingCastRequestData("Ground-placed spell requires a target position or target node.");
    }

    public bool TryBeginPlacement(ISpellCaster caster, SpellCastRequest request)
    {
        if (!CanCast(caster, request))
            return false;

        CleanupPreview();
        if (!ShowPreview(caster, request))
            return false;

        IsAwaitingPlacement = true;
        return true;
    }

    public bool TryPlace(ISpellCaster caster, SpellCastRequest request)
    {
        if (!IsAwaitingPlacement)
            return false;

        if (!TryResolvePlacementPosition(caster, request, out var worldPosition))
            return LogMissingCastRequestData("Ground-placed spell requires a target position or target node.");

        return SpawnArea(caster, worldPosition, ownRuntimeNodesForChannel: false, out _);
    }

    public void UpdatePlacementPreview(SpellCastRequest request)
    {
        if (!IsAwaitingPlacement)
            return;

        if (_previewArea == null || !GodotObject.IsInstanceValid(_previewArea))
        {
            _previewArea = null;
            return;
        }

        if (request == null || !request.TryResolveTargetPosition(out var worldPosition))
            return;

        _previewArea.GlobalPosition = worldPosition;
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

    protected virtual bool TryResolvePlacementPosition(
        ISpellCaster caster,
        SpellCastRequest request,
        out Vector2 worldPosition)
    {
        if (request != null && request.TryResolveTargetPosition(out worldPosition))
            return true;

        worldPosition = default;
        return false;
    }

    protected virtual void ConfigureArea(AreaOfEffect area, ISpellCaster caster)
    {
    }

    private bool SpawnArea(
        ISpellCaster caster,
        Vector2 worldPosition,
        bool ownRuntimeNodesForChannel,
        out SpellCastResult result)
    {
        result = null;
        if (!CanCast(caster, SpellCastRequest.Empty))
        {
            CancelPlacement();
            return false;
        }

        var fallbackParent = caster?.SpellOrigin?.GetParent();
        if (fallbackParent == null)
        {
            CancelPlacement();
            return false;
        }

        var parent = ResolveAreaSpawnParent(caster, fallbackParent);

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

        if (ownRuntimeNodesForChannel)
        {
            result = new SpellCastResult();
            result.AddChannelOwnedNode(area);
        }

        StartCooldown();
        CancelPlacement();
        return true;
    }

    private bool ShowPreview(ISpellCaster caster, SpellCastRequest request)
    {
        if (caster?.SpellOrigin == null)
            return false;

        var fallbackParent = caster.SpellOrigin.GetParent();
        if (fallbackParent == null)
            return false;

        if (ResolveAreaTemplate()?.Duplicate() is not AreaOfEffect previewArea)
            return false;

        if (request == null || !request.TryResolveTargetPosition(out var previewPosition))
        {
            previewArea.QueueFree();
            return LogMissingCastRequestData("Ground-placed spell preview requires a target position.");
        }

        previewArea.InitializePreview();
        previewArea.GlobalPosition = previewPosition;
        ResolveAreaSpawnParent(caster, fallbackParent).AddChild(previewArea);
        _previewArea = previewArea;
        return true;
    }

    private void CleanupPreview()
    {
        if (_previewArea != null && GodotObject.IsInstanceValid(_previewArea))
            _previewArea.QueueFree();

        _previewArea = null;
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

    private Node ResolveAreaSpawnParent(ISpellCaster caster, Node fallbackParent)
    {
        var world = FindWorld(caster?.SpellOrigin) ?? FindWorld(this);
        if (world?.ActiveRoom == null)
        {
            GD.PushWarning($"{GetPath()}: {GetType().Name} could not resolve an active room for AreaOfEffect parenting. Falling back to the spell origin parent.");
            return fallbackParent;
        }

        var ephemeralRoot = world.ActiveRoom.GetUnscaledEphemeralRoot();
        if (ephemeralRoot != null)
            return ephemeralRoot;

        GD.PushWarning($"{GetPath()}: {GetType().Name} could not resolve '{world.ActiveRoom.Name}' unscaled ephemeral root. Falling back to the spell origin parent.");
        return fallbackParent;
    }

    private static World FindWorld(Node node)
    {
        var current = node;
        while (current != null)
        {
            if (current is World world)
                return world;

            current = current.GetParent();
        }

        return null;
    }
}
