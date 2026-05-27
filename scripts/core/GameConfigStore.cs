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
    private const string SettingsFieldName = "settings";
    private const string ShowActorNamesFieldName = "showActorNames";
    private const string ShowFloatingTextFieldName = "showFloatingText";
    private const string ShowCombatLogDebugMessagesFieldName = "showCombatLogDebugMessages";
    private const string ShowCombatLogFieldName = "showCombatLog";
    private const string LockCombatLogPositionFieldName = "lockCombatLogPosition";
    private const string CombatLogPositionFieldName = "combatLogPosition";
    private const string CombatLogPositionXFieldName = "x";
    private const string CombatLogPositionYFieldName = "y";
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

    public void InitializeSpellLoadout(SpellBook defaultSpellBook, IReadOnlyList<SpellBook> spellBooks, SpellLoadout spellLoadout)
    {
        if (defaultSpellBook == null || !GodotObject.IsInstanceValid(defaultSpellBook) ||
            spellBooks == null ||
            spellLoadout == null || !GodotObject.IsInstanceValid(spellLoadout))
        {
            return;
        }

        spellLoadout.ApplyDefaultAssignments(defaultSpellBook);

        var status = TryLoadConfigRoot(out var root, out var message);
        switch (status)
        {
            case ConfigLoadStatus.Missing:
                if (!TrySaveSpellLoadout(spellLoadout, out var saveMessage))
                    GD.PushWarning(saveMessage);
                return;
            case ConfigLoadStatus.Loaded:
                ApplyConfiguredSpellLoadout(root, spellBooks, spellLoadout);
                return;
            case ConfigLoadStatus.Invalid:
                GD.PushWarning(message);
                return;
            default:
                return;
        }
    }

    public void LoadGameSettings()
    {
        var status = TryLoadConfigRoot(out var root, out var message);
        switch (status)
        {
            case ConfigLoadStatus.Missing:
                ApplyGameSettings(null);
                return;
            case ConfigLoadStatus.Loaded:
                ApplyGameSettings(root);
                return;
            case ConfigLoadStatus.Invalid:
                GD.PushWarning(message);
                ApplyGameSettings(null);
                return;
            default:
                ApplyGameSettings(null);
                return;
        }
    }

    public bool TrySaveGameSettings(out string message)
    {
        message = string.Empty;

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

        root[SettingsFieldName] = BuildGameSettingsSection();
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
            message = $"Saved game settings to {GetDisplayPath()}.";
            return true;
        }
        catch (Exception exception)
        {
            message = $"Failed to save {GetDisplayPath()}: {exception.Message}";
            return false;
        }
    }

    private static void ApplyGameSettings(JsonObject root)
    {
        var showActorNames = GameSettings.DefaultShowActorNames;
        var showFloatingText = GameSettings.DefaultShowFloatingText;
        var showCombatLogDebug = GameSettings.DefaultShowCombatLogDebugMessages;
        var showCombatLog = GameSettings.DefaultShowCombatLog;
        var lockCombatLogPosition = GameSettings.DefaultLockCombatLogPosition;
        var combatLogPosition = GameSettings.DefaultCombatLogPosition;
        var combatLogPositionCustomized = false;

        if (root != null &&
            root.TryGetPropertyValue(SettingsFieldName, out var settingsNode) &&
            settingsNode is JsonObject settingsObject)
        {
            showActorNames = ReadBoolSetting(settingsObject, ShowActorNamesFieldName, showActorNames);
            showFloatingText = ReadBoolSetting(settingsObject, ShowFloatingTextFieldName, showFloatingText);
            showCombatLogDebug = ReadBoolSetting(settingsObject, ShowCombatLogDebugMessagesFieldName, showCombatLogDebug);
            showCombatLog = ReadBoolSetting(settingsObject, ShowCombatLogFieldName, showCombatLog);
            lockCombatLogPosition = ReadBoolSetting(settingsObject, LockCombatLogPositionFieldName, lockCombatLogPosition);
            combatLogPositionCustomized = TryReadVector2Setting(
                settingsObject, CombatLogPositionFieldName, out var parsedPosition);
            if (combatLogPositionCustomized)
                combatLogPosition = parsedPosition;
        }
        else if (root != null && root.ContainsKey(SettingsFieldName))
        {
            GD.PushWarning(
                $"{GetDisplayPath()}: '{SettingsFieldName}' must be a JSON object. Using default game settings.");
        }

        GameSettings.SetShowActorNames(showActorNames);
        GameSettings.SetShowFloatingText(showFloatingText);
        GameSettings.SetShowCombatLogDebugMessages(showCombatLogDebug);
        GameSettings.SetShowCombatLog(showCombatLog);
        GameSettings.SetLockCombatLogPosition(lockCombatLogPosition);
        GameSettings.SetCombatLogPosition(combatLogPosition, combatLogPositionCustomized);
    }

    private static bool ReadBoolSetting(JsonObject settingsObject, string fieldName, bool defaultValue)
    {
        if (!settingsObject.TryGetPropertyValue(fieldName, out var node) || node == null)
            return defaultValue;

        if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var parsed))
            return parsed;

        GD.PushWarning(
            $"{GetDisplayPath()}: setting '{fieldName}' must be a boolean. Falling back to default '{defaultValue}'.");
        return defaultValue;
    }

    private static JsonObject BuildGameSettingsSection()
    {
        var section = new JsonObject
        {
            [ShowActorNamesFieldName] = GameSettings.ShowActorNames,
            [ShowFloatingTextFieldName] = GameSettings.ShowFloatingText,
            [ShowCombatLogDebugMessagesFieldName] = GameSettings.ShowCombatLogDebugMessages,
            [ShowCombatLogFieldName] = GameSettings.ShowCombatLog,
            [LockCombatLogPositionFieldName] = GameSettings.LockCombatLogPosition,
        };

        if (GameSettings.CombatLogPositionCustomized)
        {
            var position = GameSettings.CombatLogPosition;
            section[CombatLogPositionFieldName] = new JsonObject
            {
                [CombatLogPositionXFieldName] = position.X,
                [CombatLogPositionYFieldName] = position.Y,
            };
        }

        return section;
    }

    private static bool TryReadVector2Setting(JsonObject settingsObject, string fieldName, out Vector2 value)
    {
        value = Vector2.Zero;
        if (!settingsObject.TryGetPropertyValue(fieldName, out var node) || node == null)
            return false;

        if (node is not JsonObject vectorObject)
        {
            GD.PushWarning(
                $"{GetDisplayPath()}: setting '{fieldName}' must be an object with '{CombatLogPositionXFieldName}'/'{CombatLogPositionYFieldName}' numbers. Ignoring saved position.");
            return false;
        }

        if (!TryReadFloatField(vectorObject, fieldName, CombatLogPositionXFieldName, out var x) ||
            !TryReadFloatField(vectorObject, fieldName, CombatLogPositionYFieldName, out var y))
        {
            return false;
        }

        value = new Vector2(x, y);
        return true;
    }

    private static bool TryReadFloatField(JsonObject vectorObject, string parentName, string fieldName, out float value)
    {
        value = 0.0f;
        if (!vectorObject.TryGetPropertyValue(fieldName, out var node) || node == null)
        {
            GD.PushWarning(
                $"{GetDisplayPath()}: setting '{parentName}.{fieldName}' is missing. Ignoring saved position.");
            return false;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<float>(out var floatValue))
            {
                value = floatValue;
                return true;
            }

            if (jsonValue.TryGetValue<double>(out var doubleValue))
            {
                value = (float)doubleValue;
                return true;
            }
        }

        GD.PushWarning(
            $"{GetDisplayPath()}: setting '{parentName}.{fieldName}' must be a number. Ignoring saved position.");
        return false;
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

    private void ApplyConfiguredSpellLoadout(JsonObject root, IReadOnlyList<SpellBook> spellBooks, SpellLoadout spellLoadout)
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

        spellLoadout.ApplySpellIdAssignments(spellBooks, configuredAssignments);
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
