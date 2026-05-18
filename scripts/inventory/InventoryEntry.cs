using Godot;

public abstract class InventoryEntry
{
    public abstract InventoryItemDefinition Definition { get; }

    public Texture2D Icon => Definition?.Icon;

    public string DisplayName => Definition?.DisplayName ?? string.Empty;

    public abstract int Quantity { get; }

    public abstract bool ShowQuantity { get; }

    public abstract bool CanAcceptMergeFrom(InventoryEntry other);

    public virtual string TooltipText => ShowQuantity ? $"{DisplayName} x{Quantity}" : DisplayName;
}
