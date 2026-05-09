using Godot;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class GameConfigStore
{
    private const string ConfigFilePath = "user://config.json";
    private const string VersionFieldName = "version";
    private const string SpellLoadoutFieldName = "spellLoadout";
    private const int CurrentConfigVersion = 1;
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
    };

    private enum ConfigLoadStatus
    {
        Missing,
        Loaded,
        Invalid,
    }

    public void InitializeSpellLoadout(SpellBook spellBook, SpellLoadout spellLoadout)
    {
        if (spellBook == null || !GodotObject.IsInstanceValid(spellBook) ||
            spellLoadout == null || !GodotObject.IsInstanceValid(spellLoadout))
        {
            return;
        }

        spellLoadout.ApplyDefaultAssignments(spellBook);

        var status = TryLoadConfigRoot(out var root, out var message);
        switch (status)
        {
            case ConfigLoadStatus.Missing:
                if (!TrySaveSpellLoadout(spellLoadout, out var saveMessage))
                    GD.PushWarning(saveMessage);
                return;
            case ConfigLoadStatus.Loaded:
                ApplyConfiguredSpellLoadout(root, spellBook, spellLoadout);
                return;
            case ConfigLoadStatus.Invalid:
                GD.PushWarning(message);
                return;
            default:
                return;
        }
    }

    public bool TrySaveSpellLoadout(SpellLoadout spellLoadout, out string message)
    {
        message = string.Empty;
        if (spellLoadout == null || !GodotObject.IsInstanceValid(spellLoadout))
        {
            message = $"Cannot save spell loadout because {nameof(SpellLoadout)} is unavailable.";
            return false;
        }

        JsonObject root;
        var status = TryLoadConfigRoot(out root, out var loadMessage);
        switch (status)
        {
            case ConfigLoadStatus.Missing:
                root = new JsonObject();
                break;
            case ConfigLoadStatus.Loaded:
                break;
            case ConfigLoadStatus.Invalid:
                message = $"{loadMessage} Refusing to overwrite malformed config so unknown fields are not destroyed.";
                return false;
            default:
                message = "Unknown configuration load status.";
                return false;
        }

        root[SpellLoadoutFieldName] = BuildSpellLoadoutSection(spellLoadout);
        if (!root.ContainsKey(VersionFieldName))
            root[VersionFieldName] = CurrentConfigVersion;

        try
        {
            using var file = FileAccess.Open(ConfigFilePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                message = $"Failed to open {GetDisplayPath()} for writing.";
                return false;
            }

            file.StoreString(root.ToJsonString(JsonWriteOptions));
            message = $"Saved spell loadout to {GetDisplayPath()}.";
            return true;
        }
        catch (Exception exception)
        {
            message = $"Failed to save {GetDisplayPath()}: {exception.Message}";
            return false;
        }
    }

    private void ApplyConfiguredSpellLoadout(JsonObject root, SpellBook spellBook, SpellLoadout spellLoadout)
    {
        if (!root.TryGetPropertyValue(SpellLoadoutFieldName, out var spellLoadoutNode) || spellLoadoutNode == null)
            return;

        if (spellLoadoutNode is not JsonObject spellLoadoutObject)
        {
            GD.PushWarning(
                $"{GetDisplayPath()}: '{SpellLoadoutFieldName}' must be a JSON object. Keeping authored spell defaults.");
            return;
        }

        var configuredAssignments = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in spellLoadoutObject)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            if (pair.Value == null)
            {
                configuredAssignments[pair.Key] = string.Empty;
                continue;
            }

            if (pair.Value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var spellId))
            {
                configuredAssignments[pair.Key] = spellId ?? string.Empty;
                continue;
            }

            GD.PushWarning(
                $"{GetDisplayPath()}: spell loadout slot '{pair.Key}' must contain a spell id string. Leaving the slot empty.");
            configuredAssignments[pair.Key] = string.Empty;
        }

        spellLoadout.ApplySpellIdAssignments(spellBook, configuredAssignments);
    }

    private static JsonObject BuildSpellLoadoutSection(SpellLoadout spellLoadout)
    {
        var spellLoadoutSection = new JsonObject();
        foreach (var assignment in spellLoadout.BuildSpellIdAssignments())
            spellLoadoutSection[assignment.Key] = assignment.Value;

        return spellLoadoutSection;
    }

    private static ConfigLoadStatus TryLoadConfigRoot(out JsonObject root, out string message)
    {
        root = null;
        message = string.Empty;

        if (!FileAccess.FileExists(ConfigFilePath))
            return ConfigLoadStatus.Missing;

        using var file = FileAccess.Open(ConfigFilePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            message = $"Failed to open {GetDisplayPath()} for reading. Startup is using authored spell defaults.";
            return ConfigLoadStatus.Invalid;
        }

        try
        {
            var parsedNode = JsonNode.Parse(file.GetAsText());
            if (parsedNode is not JsonObject rootObject)
            {
                message = $"Failed to parse {GetDisplayPath()}: root JSON value must be an object. Startup is using authored spell defaults.";
                return ConfigLoadStatus.Invalid;
            }

            root = rootObject;
            return ConfigLoadStatus.Loaded;
        }
        catch (JsonException exception)
        {
            message = $"Failed to parse {GetDisplayPath()}: {exception.Message}. Startup is using authored spell defaults.";
            return ConfigLoadStatus.Invalid;
        }
    }

    private static string GetDisplayPath()
    {
        return $"{ConfigFilePath} ({ProjectSettings.GlobalizePath(ConfigFilePath)})";
    }
}
