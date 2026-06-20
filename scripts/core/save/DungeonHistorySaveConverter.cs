using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// Robust reader for the optional dungeon-history array in a save file. History is secondary data,
// so a single structurally unreadable entry (e.g. a field with the wrong JSON type) must not fail
// the whole-save deserialization and lose player/inventory/equipment state.
//
// The value is parsed into a detached JsonDocument and each element is deserialized in isolation:
// a per-entry JSON error is caught and recorded as a null placeholder so the load step can count
// and skip it while keeping valid neighbors. A non-array (or null) history tolerates to empty. A
// syntactically invalid root file is still rejected by the existing whole-file corruption path.
public sealed class DungeonHistorySaveConverter : JsonConverter<List<DungeonRunRecordSaveData>>
{
    public override List<DungeonRunRecordSaveData> Read(
        ref Utf8JsonReader reader,
        System.Type typeToConvert,
        JsonSerializerOptions options)
    {
        var entries = new List<DungeonRunRecordSaveData>();

        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return entries;

        foreach (var element in document.RootElement.EnumerateArray())
        {
            try
            {
                // Null placeholder for a structurally unreadable entry; the load step treats it as
                // a skipped record so neighbors survive.
                entries.Add(element.Deserialize<DungeonRunRecordSaveData>(options));
            }
            catch (JsonException)
            {
                entries.Add(null);
            }
        }

        return entries;
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<DungeonRunRecordSaveData> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        if (value != null)
        {
            foreach (var entry in value)
                JsonSerializer.Serialize(writer, entry, options);
        }

        writer.WriteEndArray();
    }
}
