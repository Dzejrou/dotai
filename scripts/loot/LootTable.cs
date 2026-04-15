using Godot;

[GlobalClass]
public partial class LootTable : Resource
{
    [Export]
    public Godot.Collections.Array<LootEntry> Entries { get; set; } = new();

    public Godot.Collections.Array<DropDefinition> Roll(RandomNumberGenerator random)
    {
        var definitions = new Godot.Collections.Array<DropDefinition>();

        foreach (var entry in Entries)
        {
            if (entry == null || !entry.ShouldDrop(random))
                continue;

            definitions.Add(entry.Definition);
        }

        return definitions;
    }
}
