using Godot;

[GlobalClass]
public partial class LootTable : Resource
{
    [Export]
    public Godot.Collections.Array<LootEntry> Entries { get; set; } = new();

    public Godot.Collections.Array<LootEntry> Roll(RandomNumberGenerator random)
    {
        var rolledEntries = new Godot.Collections.Array<LootEntry>();

        foreach (var entry in Entries)
        {
            if (entry == null || !entry.ShouldDrop(random))
                continue;

            rolledEntries.Add(entry);
        }

        return rolledEntries;
    }
}
