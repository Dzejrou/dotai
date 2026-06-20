using System;
using System.Collections.Generic;

// Converts between the runtime newest-first finalized history and its save representation. Keeps
// the latest-100 cap enforced on both ends and validates each loaded entry independently so a
// malformed record is skipped without affecting valid neighbors or the rest of the save.
public static class DungeonHistorySaveSerializer
{
    public const int MaxRecords = 100;

    // Builds the save snapshot from newest-first history, capped at the newest MaxRecords.
    public static List<DungeonRunRecordSaveData> CreateSnapshot(IReadOnlyList<DungeonRunRecord> history)
    {
        var snapshot = new List<DungeonRunRecordSaveData>();
        if (history == null)
            return snapshot;

        var count = Math.Min(history.Count, MaxRecords);
        for (var i = 0; i < count; i++)
        {
            var record = history[i];
            if (record != null)
                snapshot.Add(DungeonRunRecordSaveData.FromRecord(record));
        }

        return snapshot;
    }

    // Rebuilds newest-first runtime records from a save snapshot. Skips null placeholders (entries
    // the tolerant converter could not read) and entries that fail independent validation, counting
    // them in skippedCount, and clamps the surviving valid collection to the newest MaxRecords.
    public static List<DungeonRunRecord> FromSnapshot(
        IReadOnlyList<DungeonRunRecordSaveData> snapshot,
        out int skippedCount)
    {
        skippedCount = 0;
        var records = new List<DungeonRunRecord>();
        if (snapshot == null)
            return records;

        foreach (var entry in snapshot)
        {
            if (records.Count >= MaxRecords)
                break;

            if (entry == null || !entry.TryToRecord(out var record))
            {
                skippedCount++;
                continue;
            }

            records.Add(record);
        }

        return records;
    }
}
