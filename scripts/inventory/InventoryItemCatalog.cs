using Godot;

using System;

[GlobalClass]
public partial class InventoryItemCatalog : Resource
{
    [Export]
    public Godot.Collections.Array<InventoryItemDefinition> Items { get; set; } = new();

    public InventoryItemDefinition Resolve(string id, string resourcePath)
    {
        if (!string.IsNullOrEmpty(id))
        {
            foreach (var item in Items)
            {
                if (item != null && string.Equals(item.Id, id, StringComparison.Ordinal))
                    return item;
            }
        }

        if (!string.IsNullOrEmpty(resourcePath))
        {
            foreach (var item in Items)
            {
                if (item != null && string.Equals(item.ResourcePath, resourcePath, StringComparison.Ordinal))
                    return item;
            }

            if (ResourceLoader.Exists(resourcePath))
                return ResourceLoader.Load<InventoryItemDefinition>(resourcePath);
        }

        return null;
    }
}
