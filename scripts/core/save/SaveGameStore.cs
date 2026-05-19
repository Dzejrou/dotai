using Godot;

using System;
using System.Text.Json;

public sealed class SaveGameStore
{
    public const string SaveFilePath = "user://save_slot_0.json";
    public const string SchemaTag = "dotai.savegame";
    public const int CurrentVersion = 1;

    private const string TempFilePath = "user://save_slot_0.json.tmp";

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
    };

    public bool TrySave(SaveGameData data, out string message)
    {
        message = string.Empty;
        if (data == null)
        {
            message = "Save refused: data is null.";
            return false;
        }

        data.Schema = SchemaTag;
        data.Version = CurrentVersion;
        data.Timestamp = DateTime.UtcNow.ToString("o");

        string json;
        try
        {
            json = JsonSerializer.Serialize(data, JsonWriteOptions);
        }
        catch (Exception exception)
        {
            message = $"Failed to serialize save data: {exception.Message}";
            return false;
        }

        try
        {
            using var file = FileAccess.Open(TempFilePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                message = $"Failed to open {GetDisplayPath(TempFilePath)} for writing.";
                return false;
            }

            file.StoreString(json);
        }
        catch (Exception exception)
        {
            message = $"Failed to write {GetDisplayPath(TempFilePath)}: {exception.Message}";
            return false;
        }

        // POSIX rename() is atomic and overwrites; on platforms where the
        // target must not exist for rename, fall back to remove-then-rename.
        var renameError = DirAccess.RenameAbsolute(TempFilePath, SaveFilePath);
        if (renameError != Error.Ok && FileAccess.FileExists(SaveFilePath))
        {
            DirAccess.RemoveAbsolute(SaveFilePath);
            renameError = DirAccess.RenameAbsolute(TempFilePath, SaveFilePath);
        }

        if (renameError != Error.Ok)
        {
            message = $"Failed to publish {GetDisplayPath(SaveFilePath)} (rename error: {renameError}).";
            return false;
        }

        message = $"Saved game to {GetDisplayPath(SaveFilePath)}.";
        return true;
    }

    public bool TryLoad(out SaveGameData data)
    {
        data = null;
        if (!FileAccess.FileExists(SaveFilePath))
            return false;

        string json;
        try
        {
            using var file = FileAccess.Open(SaveFilePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushWarning($"Could not open {GetDisplayPath(SaveFilePath)} for reading; starting fresh.");
                return false;
            }

            json = file.GetAsText();
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not read {GetDisplayPath(SaveFilePath)}: {exception.Message}; starting fresh.");
            return false;
        }

        SaveGameData parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SaveGameData>(json);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not parse {GetDisplayPath(SaveFilePath)}: {exception.Message}; starting fresh.");
            return false;
        }

        if (parsed == null)
        {
            GD.PushWarning($"{GetDisplayPath(SaveFilePath)} parsed to null; starting fresh.");
            return false;
        }

        if (!string.Equals(parsed.Schema, SchemaTag, StringComparison.Ordinal))
        {
            GD.PushWarning(
                $"{GetDisplayPath(SaveFilePath)} schema '{parsed.Schema}' does not match '{SchemaTag}'; starting fresh.");
            return false;
        }

        if (parsed.Version != CurrentVersion)
        {
            GD.PushWarning(
                $"{GetDisplayPath(SaveFilePath)} version {parsed.Version} != current {CurrentVersion}; starting fresh.");
            return false;
        }

        data = parsed;
        return true;
    }

    private static string GetDisplayPath(string path)
    {
        return $"{path} ({ProjectSettings.GlobalizePath(path)})";
    }
}
