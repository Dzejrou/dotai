using Godot;

using System;

[GlobalClass]
public partial class InventoryItemDefinition : Resource
{
    [Export]
    public string Id { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public Texture2D Icon { get; set; }

    [Export(PropertyHint.Range, "1,999,1")]
    public int MaxStackSize
    {
        get => _maxStackSize;
        set => _maxStackSize = Math.Max(1, value);
    }

    [Export]
    public InventoryKeyKind KeyKind { get; set; } = InventoryKeyKind.None;

    private int _maxStackSize = 99;
}
