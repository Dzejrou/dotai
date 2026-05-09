using Godot;

using System;

[GlobalClass]
public partial class InventoryWorldDropZone : Control
{
    public Action<int> WorldDropReceived { get; set; }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Int;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        WorldDropReceived?.Invoke(data.AsInt32());
    }
}
