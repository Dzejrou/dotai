using Godot;
using Godot.Collections;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class AssetManagerTool : Control
{
    private const string AssetsRoot = "res://assets";
    private const string ResourcesRoot = "res://resources/animations";
    private const string BaseDirectoryName = "base";
    private const string DefaultAnimationName = "default";
    private const float DefaultAnimationSpeed = 5.0f;
    private const string DefaultExternalSourceDirectory = "/Users/jjindrak/Projects/pixelart/characters";
    private const string ExporterWrapperPath = "/Users/jjindrak/Projects/pixelart/scripts/export";

    [Export]
    public string ExternalSourceDirectory { get; set; } = DefaultExternalSourceDirectory;

    private LineEdit _sourceDirectoryInput;
    private Label _sourceSummaryLabel;
    private Label _selectionLabel;
    private ItemList _asepriteFilesList;
    private Button _refreshButton;
    private Button _exportButton;
    private Button _syncButton;
    private TextEdit _statusOutput;
    private bool _isBusy;
    private List<string> _asepriteFiles = new();

    public override void _Ready()
    {
        CacheUiNodes();

        HeadlessCommand command;
        try
        {
            command = HeadlessCommand.Parse(OS.GetCmdlineUserArgs());
        }
        catch (Exception exception)
        {
            HandleStartupFailure(exception);
            return;
        }

        if (command.Action != ToolAction.None)
        {
            RunHeadlessCommand(command);
            return;
        }

        if (IsHeadlessRuntime())
        {
            HandleStartupFailure(new InvalidOperationException("No headless action specified. Use --sync [--character NAME] or --export FILE."));
            return;
        }

        WireUiSignals();
        PopulateUi();
    }

    private void CacheUiNodes()
    {
        _sourceDirectoryInput = GetNode<LineEdit>("Margin/Panel/VBox/SourceRow/SourceDirectoryInput");
        _sourceSummaryLabel = GetNode<Label>("Margin/Panel/VBox/SummaryRow/SourceSummaryLabel");
        _selectionLabel = GetNode<Label>("Margin/Panel/VBox/SummaryRow/SelectionLabel");
        _asepriteFilesList = GetNode<ItemList>("Margin/Panel/VBox/Body/FilesPanel/FilesVBox/AsepriteFiles");
        _refreshButton = GetNode<Button>("Margin/Panel/VBox/SourceRow/RefreshButton");
        _exportButton = GetNode<Button>("Margin/Panel/VBox/Actions/ExportButton");
        _syncButton = GetNode<Button>("Margin/Panel/VBox/Actions/SyncButton");
        _statusOutput = GetNode<TextEdit>("Margin/Panel/VBox/Body/StatusPanel/StatusVBox/StatusOutput");
    }

    private void WireUiSignals()
    {
        _refreshButton.Pressed += OnRefreshPressed;
        _exportButton.Pressed += OnExportPressed;
        _syncButton.Pressed += OnSyncPressed;
        _sourceDirectoryInput.TextSubmitted += OnSourceDirectorySubmitted;
        _asepriteFilesList.ItemSelected += OnFileSelected;
        _asepriteFilesList.EmptyClicked += OnEmptyListClicked;
    }

    private void PopulateUi()
    {
        ExternalSourceDirectory = NormalizeDirectoryPath(ExternalSourceDirectory);
        _sourceDirectoryInput.Text = ExternalSourceDirectory;
        _statusOutput.Text = string.Empty;
        RefreshAsepriteFiles();
        AppendStatus("Ready. Export and sync are separate actions.");
        AppendStatus("Export writes directly into this project's assets directory. Run Godot import separately before syncing.");
    }

    private void RunHeadlessCommand(HeadlessCommand command)
    {
        var exitCode = 0;

        try
        {
            switch (command.Action)
            {
                case ToolAction.Export:
                    var exportResult = ExportAsepriteFile(command.ExportFilePath);
                    foreach (var line in FormatExportResult(exportResult))
                        GD.Print(line);
                    break;
                case ToolAction.Sync:
                    var syncSummary = RunSync(command.RequestedCharacters);
                    foreach (var line in FormatSyncSummary(syncSummary))
                        GD.Print(line);
                    break;
            }
        }
        catch (Exception exception)
        {
            exitCode = 1;
            GD.PushError($"Asset manager failed: {exception.Message}");
            GD.PrintErr(exception.ToString());
        }

        GetTree().Quit(exitCode);
    }

    private void HandleStartupFailure(Exception exception)
    {
        GD.PushError($"Asset manager startup failed: {exception.Message}");
        GD.PrintErr(exception.ToString());
        GetTree().Quit(1);
    }

    private void OnRefreshPressed()
    {
        ApplySourceDirectoryInput();
        RefreshAsepriteFiles();
        AppendStatus($"Refreshed source files from {ExternalSourceDirectory}.");
    }

    private void OnExportPressed()
    {
        if (_isBusy)
            return;

        var sourceFilePath = GetSelectedSourceFilePath();
        if (sourceFilePath == null)
        {
            AppendStatus("Select a .aseprite file before exporting.");
            return;
        }

        ExecuteUiAction(() =>
        {
            var result = ExportAsepriteFile(sourceFilePath);
            foreach (var line in FormatExportResult(result))
                AppendStatus(line);
        });
    }

    private void OnSyncPressed()
    {
        if (_isBusy)
            return;

        var characterName = GetSelectedCharacterName();
        if (characterName == null)
        {
            AppendStatus("Select a .aseprite file before syncing.");
            return;
        }

        ExecuteUiAction(() =>
        {
            var requestedCharacters = new HashSet<string>(StringComparer.Ordinal)
            {
                characterName,
            };

            var summary = RunSync(requestedCharacters);
            foreach (var line in FormatSyncSummary(summary))
                AppendStatus(line);
        });
    }

    private void OnSourceDirectorySubmitted(string _submittedText)
    {
        OnRefreshPressed();
    }

    private void OnFileSelected(long _index)
    {
        UpdateSelectionLabel();
        UpdateActionState();
    }

    private void OnEmptyListClicked(Vector2 _position, long _mouseButtonIndex)
    {
        UpdateSelectionLabel();
        UpdateActionState();
    }

    private void ExecuteUiAction(Action action)
    {
        SetBusy(true);

        try
        {
            action();
        }
        catch (Exception exception)
        {
            AppendStatus($"Error: {exception.Message}");
            GD.PushError($"Asset manager action failed: {exception.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        UpdateActionState();
    }

    private void UpdateActionState()
    {
        var hasSelection = GetSelectedSourceFilePath() != null;
        _refreshButton.Disabled = _isBusy;
        _exportButton.Disabled = _isBusy || !hasSelection;
        _syncButton.Disabled = _isBusy || !hasSelection;
    }

    private void ApplySourceDirectoryInput()
    {
        ExternalSourceDirectory = NormalizeDirectoryPath(_sourceDirectoryInput.Text);
        _sourceDirectoryInput.Text = ExternalSourceDirectory;
    }

    private void RefreshAsepriteFiles()
    {
        var previousSelection = GetSelectedSourceFilePath();
        _asepriteFilesList.Clear();
        _asepriteFiles = new List<string>();

        if (!Directory.Exists(ExternalSourceDirectory))
        {
            _sourceSummaryLabel.Text = "Source directory not found.";
            _selectionLabel.Text = "No file selected.";
            UpdateActionState();
            return;
        }

        _asepriteFiles = Directory
            .EnumerateFiles(ExternalSourceDirectory, "*.aseprite", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < _asepriteFiles.Count; index++)
        {
            var path = _asepriteFiles[index];
            _asepriteFilesList.AddItem(Path.GetFileName(path));
            _asepriteFilesList.SetItemTooltip(index, path);
            _asepriteFilesList.SetItemMetadata(index, path);
        }

        _sourceSummaryLabel.Text = $"{_asepriteFiles.Count} .aseprite files in {ExternalSourceDirectory}";

        if (_asepriteFiles.Count == 0)
        {
            _selectionLabel.Text = "No file selected.";
            UpdateActionState();
            return;
        }

        var selectedIndex = previousSelection == null ? 0 : _asepriteFiles.FindIndex(path => string.Equals(path, previousSelection, StringComparison.Ordinal));
        if (selectedIndex < 0)
            selectedIndex = 0;

        _asepriteFilesList.Select(selectedIndex);
        _asepriteFilesList.GrabFocus();
        UpdateSelectionLabel();
        UpdateActionState();
    }

    private void UpdateSelectionLabel()
    {
        var sourceFilePath = GetSelectedSourceFilePath();
        if (sourceFilePath == null)
        {
            _selectionLabel.Text = "No file selected.";
            return;
        }

        var characterName = InferCharacterNameFromSourceFile(sourceFilePath);
        _selectionLabel.Text = $"Selected: {Path.GetFileName(sourceFilePath)} -> assets/{characterName}";
    }

    private string GetSelectedSourceFilePath()
    {
        var selectedItems = _asepriteFilesList.GetSelectedItems();
        if (selectedItems.Length == 0)
            return null;

        var index = selectedItems[0];
        if (index < 0 || index >= _asepriteFiles.Count)
            return null;

        return _asepriteFiles[index];
    }

    private string GetSelectedCharacterName()
    {
        var sourceFilePath = GetSelectedSourceFilePath();
        return sourceFilePath == null ? null : InferCharacterNameFromSourceFile(sourceFilePath);
    }

    private ExportResult ExportAsepriteFile(string sourceFilePath)
    {
        if (!File.Exists(ExporterWrapperPath))
            throw new InvalidOperationException($"Exporter wrapper not found at {ExporterWrapperPath}.");

        if (!File.Exists(sourceFilePath))
            throw new InvalidOperationException($"Source file not found at {sourceFilePath}.");

        var characterName = InferCharacterNameFromSourceFile(sourceFilePath);
        if (string.IsNullOrWhiteSpace(characterName))
            throw new InvalidOperationException($"Could not infer a character name from {sourceFilePath}.");

        var outputDirectory = ProjectSettings.GlobalizePath($"{AssetsRoot}/{characterName}");
        Directory.CreateDirectory(outputDirectory);

        Godot.Collections.Array output = new();
        var exitCode = OS.Execute(
            ExporterWrapperPath,
            new[] { "--in", sourceFilePath, "--out", outputDirectory, "--replace-all" },
            output,
            true,
            false);

        var outputLines = output
            .Select(item => item.ToString())
            .SelectMany(text => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();

        if (exitCode != 0)
        {
            var details = outputLines.Count == 0 ? string.Empty : $" Output: {string.Join(" | ", outputLines)}";
            throw new InvalidOperationException($"Exporter failed for {characterName} with exit code {exitCode}.{details}");
        }

        return new ExportResult(characterName, sourceFilePath, outputDirectory, outputLines);
    }

    private SyncSummary RunSync(ISet<string> requestedCharacters = null)
    {
        EnsureResourcesDirectoryExists();

        var characterNames = GetSubdirectories(AssetsRoot)
            .Where(characterName => HasManagedAnimationFrames($"{AssetsRoot}/{characterName}"))
            .OrderBy(characterName => characterName, StringComparer.Ordinal)
            .ToList();

        if (requestedCharacters != null && requestedCharacters.Count > 0)
        {
            var missingCharacters = requestedCharacters
                .Where(requestedCharacter => !characterNames.Contains(requestedCharacter, StringComparer.Ordinal))
                .OrderBy(characterName => characterName, StringComparer.Ordinal)
                .ToList();

            if (missingCharacters.Count > 0)
                throw new InvalidOperationException($"Requested character assets not found in the managed layout: {string.Join(", ", missingCharacters)}");

            characterNames = characterNames
                .Where(characterName => requestedCharacters.Contains(characterName))
                .ToList();
        }

        var summary = new SyncSummary();
        foreach (var characterName in characterNames)
            summary.Results.Add(SyncCharacter(characterName));

        return summary;
    }

    private CharacterSyncResult SyncCharacter(string characterName)
    {
        var characterRoot = $"{AssetsRoot}/{characterName}";
        var resourcePath = $"{ResourcesRoot}/{characterName}_spriteframes.tres";
        var manifest = BuildManifest(characterName, characterRoot);

        if (manifest.Animations.Count == 0)
            return CharacterSyncResult.Skipped(characterName, resourcePath, "no animation frame folders found");

        var existingManifest = LoadExistingManifest(resourcePath);
        var desiredUid = ResolveDesiredResourceUid(resourcePath);
        var currentUid = GetKnownResourceUid(resourcePath);

        if (existingManifest != null && existingManifest.Equals(manifest) && currentUid == desiredUid)
            return CharacterSyncResult.Unchanged(characterName, resourcePath, manifest.AnimationCount, manifest.TotalFrameCount);

        var spriteFrames = ResourceLoader.Exists(resourcePath)
            ? ResourceLoader.Load<SpriteFrames>(resourcePath)
            : new SpriteFrames();

        if (spriteFrames == null)
            throw new InvalidOperationException($"Unable to load SpriteFrames resource at {resourcePath}.");

        ApplyManifest(spriteFrames, manifest);

        var saveError = ResourceSaver.Save(spriteFrames, resourcePath);
        if (saveError != Error.Ok)
            throw new InvalidOperationException($"Failed to save {resourcePath}: {saveError}.");

        var uidError = ResourceSaver.SetUid(resourcePath, desiredUid);
        if (uidError != Error.Ok)
            throw new InvalidOperationException($"Failed to set UID for {resourcePath}: {uidError}.");

        return existingManifest == null
            ? CharacterSyncResult.Created(characterName, resourcePath, manifest.AnimationCount, manifest.TotalFrameCount)
            : CharacterSyncResult.Updated(characterName, resourcePath, manifest.AnimationCount, manifest.TotalFrameCount);
    }

    private SpriteFramesManifest BuildManifest(string characterName, string characterRoot)
    {
        var animationSpecs = new List<AnimationSpec>();

        foreach (var animationName in GetSubdirectories(characterRoot)
                     .Where(name => !string.Equals(name, BaseDirectoryName, StringComparison.Ordinal))
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            var animationRoot = $"{characterRoot}/{animationName}";
            foreach (var directionName in GetSubdirectories(animationRoot).OrderBy(name => name, StringComparer.Ordinal))
            {
                var directionRoot = $"{animationRoot}/{directionName}";
                var framePaths = GetFiles(directionRoot)
                    .Where(fileName => fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(fileName => fileName, new FrameFileNameComparer())
                    .Select(fileName => $"{directionRoot}/{fileName}")
                    .ToArray();

                if (framePaths.Length == 0)
                    continue;

                animationSpecs.Add(new AnimationSpec(
                    $"{animationName}_{directionName}",
                    characterName,
                    ResolveLoop(animationName),
                    ResolveSpeed(animationName),
                    framePaths));
            }
        }

        return new SpriteFramesManifest(characterName, animationSpecs);
    }

    private SpriteFramesManifest LoadExistingManifest(string resourcePath)
    {
        if (!ResourceLoader.Exists(resourcePath))
            return null;

        if (ReferencesDeprecatedAnimationLayout(resourcePath))
            return null;

        var spriteFrames = ResourceLoader.Load<SpriteFrames>(resourcePath);
        if (spriteFrames == null)
            throw new InvalidOperationException($"Unable to load existing SpriteFrames resource at {resourcePath}.");

        var animationSpecs = new List<AnimationSpec>();
        foreach (var animationName in spriteFrames.GetAnimationNames().Select(name => name.ToString()).OrderBy(name => name, StringComparer.Ordinal))
        {
            if (string.Equals(animationName, DefaultAnimationName, StringComparison.Ordinal))
                continue;

            var framePaths = new string[spriteFrames.GetFrameCount(animationName)];
            for (var frameIndex = 0; frameIndex < framePaths.Length; frameIndex++)
            {
                var texture = spriteFrames.GetFrameTexture(animationName, frameIndex);
                framePaths[frameIndex] = texture?.ResourcePath ?? string.Empty;
            }

            animationSpecs.Add(new AnimationSpec(
                animationName,
                string.Empty,
                spriteFrames.GetAnimationLoop(animationName),
                (float)spriteFrames.GetAnimationSpeed(animationName),
                framePaths));
        }

        return new SpriteFramesManifest(string.Empty, animationSpecs);
    }

    private static bool ReferencesDeprecatedAnimationLayout(string resourcePath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(resourcePath);
        return File.Exists(absolutePath) &&
               File.ReadLines(absolutePath).Any(line => line.Contains("/animations/", StringComparison.Ordinal));
    }

    private static void ApplyManifest(SpriteFrames spriteFrames, SpriteFramesManifest manifest)
    {
        foreach (var animationName in spriteFrames.GetAnimationNames().Select(name => name.ToString()).ToArray())
        {
            if (string.Equals(animationName, DefaultAnimationName, StringComparison.Ordinal))
            {
                spriteFrames.Clear(animationName);
                continue;
            }

            spriteFrames.RemoveAnimation(animationName);
        }

        if (!spriteFrames.HasAnimation(DefaultAnimationName))
            spriteFrames.AddAnimation(DefaultAnimationName);

        spriteFrames.SetAnimationLoop(DefaultAnimationName, true);
        spriteFrames.SetAnimationSpeed(DefaultAnimationName, DefaultAnimationSpeed);

        foreach (var animation in manifest.Animations)
        {
            if (spriteFrames.HasAnimation(animation.Name))
                spriteFrames.Clear(animation.Name);
            else
                spriteFrames.AddAnimation(animation.Name);

            spriteFrames.SetAnimationLoop(animation.Name, animation.IsLooping);
            spriteFrames.SetAnimationSpeed(animation.Name, animation.Speed);

            foreach (var framePath in animation.FramePaths)
            {
                var texture = ResourceLoader.Load<Texture2D>(framePath);
                if (texture == null)
                    throw new InvalidOperationException($"Unable to load animation frame at {framePath}.");

                spriteFrames.AddFrame(animation.Name, texture, 1.0f);
            }
        }
    }

    private void AppendStatus(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        GD.Print(line);

        if (_statusOutput == null)
            return;

        _statusOutput.Text = string.IsNullOrEmpty(_statusOutput.Text)
            ? line
            : $"{_statusOutput.Text}\n{line}";
        _statusOutput.ScrollVertical = _statusOutput.GetLineCount();
    }

    private static IEnumerable<string> FormatExportResult(ExportResult result)
    {
        yield return $"Export complete: {result.CharacterName} -> {result.OutputDirectory}";
        yield return $" - source: {result.SourceFilePath}";

        if (result.OutputLines.Count > 0)
            yield return $" - exporter: {result.OutputLines[0]}";
    }

    private static IEnumerable<string> FormatSyncSummary(SyncSummary summary)
    {
        var createdCount = summary.Results.Count(result => result.Status == SyncStatus.Created);
        var updatedCount = summary.Results.Count(result => result.Status == SyncStatus.Updated);
        var unchangedCount = summary.Results.Count(result => result.Status == SyncStatus.Unchanged);
        var skippedCount = summary.Results.Count(result => result.Status == SyncStatus.Skipped);

        if (summary.Results.Count == 0)
        {
            yield return "SpriteFrames sync complete: no managed character asset folders found.";
            yield break;
        }

        yield return $"SpriteFrames sync complete: {createdCount} created, {updatedCount} updated, {unchangedCount} unchanged, {skippedCount} skipped.";

        foreach (var result in summary.Results)
        {
            switch (result.Status)
            {
                case SyncStatus.Created:
                case SyncStatus.Updated:
                    yield return $" - {result.Status.ToString().ToLowerInvariant()}: {result.CharacterName} -> {result.ResourcePath} ({result.AnimationCount} animations, {result.FrameCount} frames)";
                    break;
                case SyncStatus.Unchanged:
                    yield return $" - unchanged: {result.CharacterName} ({result.AnimationCount} animations, {result.FrameCount} frames)";
                    break;
                case SyncStatus.Skipped:
                    yield return $" - skipped: {result.CharacterName} ({result.Message})";
                    break;
            }
        }
    }

    private static bool HasManagedAnimationFrames(string characterRoot)
    {
        foreach (var animationName in GetSubdirectories(characterRoot))
        {
            if (string.Equals(animationName, BaseDirectoryName, StringComparison.Ordinal))
                continue;

            var animationRoot = $"{characterRoot}/{animationName}";
            foreach (var directionName in GetSubdirectories(animationRoot))
            {
                var directionRoot = $"{animationRoot}/{directionName}";
                if (GetFiles(directionRoot).Any(fileName => fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }

        return false;
    }

    private static bool ResolveLoop(string animationName)
    {
        return animationName switch
        {
            "breathing-idle" => true,
            "walk" => true,
            _ => false,
        };
    }

    private static float ResolveSpeed(string animationName)
    {
        return animationName switch
        {
            "shooting-bow" => 10.0f,
            "bark" => 7.0f,
            _ => DefaultAnimationSpeed,
        };
    }

    private static bool IsHeadlessRuntime()
    {
        return string.Equals(DisplayServer.GetName().ToString(), "headless", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        path = ExpandHomeDirectory((path ?? string.Empty).Trim());
        return string.IsNullOrWhiteSpace(path) ? DefaultExternalSourceDirectory : path;
    }

    private static string ExpandHomeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("~", StringComparison.Ordinal))
            return path;

        var homeDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (path.Length == 1)
            return homeDirectory;

        return Path.Combine(homeDirectory, path[2..]);
    }

    private static string InferCharacterNameFromSourceFile(string sourceFilePath)
    {
        return Path.GetFileNameWithoutExtension(sourceFilePath);
    }

    private static bool DirExists(string path)
    {
        return DirAccess.Open(path) != null;
    }

    private static long ResolveDesiredResourceUid(string resourcePath)
    {
        var currentUid = GetKnownResourceUid(resourcePath);
        if (currentUid != ResourceUid.InvalidId)
            return currentUid;

        var referencedUid = FindReferencedUidInScenes(resourcePath);
        if (referencedUid != ResourceUid.InvalidId)
            return referencedUid;

        return ResourceUid.CreateIdForPath(resourcePath);
    }

    private static long GetKnownResourceUid(string resourcePath)
    {
        if (ResourceLoader.Exists(resourcePath))
        {
            var loaderUid = ResourceLoader.GetResourceUid(resourcePath);
            if (loaderUid != ResourceUid.InvalidId)
                return loaderUid;
        }

        var saverUid = ResourceSaver.GetResourceIdForPath(resourcePath, false);
        if (saverUid != ResourceUid.InvalidId)
            return saverUid;

        return GetUidFromResourceHeader(resourcePath);
    }

    private static long GetUidFromResourceHeader(string resourcePath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(resourcePath);
        if (!File.Exists(absolutePath))
            return ResourceUid.InvalidId;

        const string uidPrefix = "uid=\"";
        foreach (var line in File.ReadLines(absolutePath).Take(1))
        {
            var uidStart = line.IndexOf(uidPrefix, StringComparison.Ordinal);
            if (uidStart < 0)
                break;

            uidStart += uidPrefix.Length;
            var uidEnd = line.IndexOf('"', uidStart);
            if (uidEnd <= uidStart)
                break;

            return ResourceUid.TextToId(line.Substring(uidStart, uidEnd - uidStart));
        }

        return ResourceUid.InvalidId;
    }

    private static long FindReferencedUidInScenes(string resourcePath)
    {
        var scenesAbsolutePath = ProjectSettings.GlobalizePath("res://scenes");
        if (!Directory.Exists(scenesAbsolutePath))
            return ResourceUid.InvalidId;

        var expectedPathFragment = $"path=\"{resourcePath}\"";
        foreach (var scenePath in Directory.EnumerateFiles(scenesAbsolutePath, "*.tscn", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(scenePath))
            {
                if (!line.Contains(expectedPathFragment, StringComparison.Ordinal))
                    continue;

                const string uidPrefix = "uid=\"";
                var uidStart = line.IndexOf(uidPrefix, StringComparison.Ordinal);
                if (uidStart < 0)
                    continue;

                uidStart += uidPrefix.Length;
                var uidEnd = line.IndexOf('"', uidStart);
                if (uidEnd <= uidStart)
                    continue;

                var uid = ResourceUid.TextToId(line.Substring(uidStart, uidEnd - uidStart));
                if (uid != ResourceUid.InvalidId)
                    return uid;
            }
        }

        return ResourceUid.InvalidId;
    }

    private static void EnsureResourcesDirectoryExists()
    {
        var error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(ResourcesRoot));
        if (error != Error.Ok && error != Error.AlreadyExists)
            throw new InvalidOperationException($"Unable to create {ResourcesRoot}: {error}.");
    }

    private static List<string> GetSubdirectories(string path)
    {
        return ListDirectoryEntries(path, true);
    }

    private static List<string> GetFiles(string path)
    {
        return ListDirectoryEntries(path, false);
    }

    private static List<string> ListDirectoryEntries(string path, bool directories)
    {
        using var dirAccess = DirAccess.Open(path);
        if (dirAccess == null)
            return new List<string>();

        var entries = new List<string>();
        dirAccess.ListDirBegin();

        while (true)
        {
            var entryName = dirAccess.GetNext();
            if (string.IsNullOrEmpty(entryName))
                break;

            if (entryName.StartsWith(".", StringComparison.Ordinal))
                continue;

            if (dirAccess.CurrentIsDir() == directories)
                entries.Add(entryName);
        }

        dirAccess.ListDirEnd();
        return entries;
    }

    private sealed class FrameFileNameComparer : IComparer<string>
    {
        public int Compare(string left, string right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return -1;

            if (right == null)
                return 1;

            var numericComparison = ExtractFrameNumber(left).CompareTo(ExtractFrameNumber(right));
            return numericComparison != 0
                ? numericComparison
                : StringComparer.Ordinal.Compare(left, right);
        }

        private static int ExtractFrameNumber(string fileName)
        {
            var underscoreIndex = fileName.LastIndexOf('_');
            var dotIndex = fileName.LastIndexOf('.');
            if (underscoreIndex < 0 || dotIndex <= underscoreIndex + 1)
                return int.MaxValue;

            var digits = fileName.Substring(underscoreIndex + 1, dotIndex - underscoreIndex - 1);
            return int.TryParse(digits, out var frameNumber) ? frameNumber : int.MaxValue;
        }
    }

    private sealed record AnimationSpec(
        string Name,
        string CharacterName,
        bool IsLooping,
        float Speed,
        IReadOnlyList<string> FramePaths);

    private sealed class SpriteFramesManifest : IEquatable<SpriteFramesManifest>
    {
        public SpriteFramesManifest(string characterName, IReadOnlyList<AnimationSpec> animations)
        {
            CharacterName = characterName;
            Animations = animations;
        }

        public string CharacterName { get; }
        public IReadOnlyList<AnimationSpec> Animations { get; }
        public int AnimationCount => Animations.Count;
        public int TotalFrameCount => Animations.Sum(animation => animation.FramePaths.Count);

        public bool Equals(SpriteFramesManifest other)
        {
            if (other == null || Animations.Count != other.Animations.Count)
                return false;

            for (var index = 0; index < Animations.Count; index++)
            {
                var left = Animations[index];
                var right = other.Animations[index];

                if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal) ||
                    left.IsLooping != right.IsLooping ||
                    Math.Abs(left.Speed - right.Speed) > 0.001f ||
                    left.FramePaths.Count != right.FramePaths.Count)
                {
                    return false;
                }

                for (var frameIndex = 0; frameIndex < left.FramePaths.Count; frameIndex++)
                {
                    if (!string.Equals(left.FramePaths[frameIndex], right.FramePaths[frameIndex], StringComparison.Ordinal))
                        return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SpriteFramesManifest);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AnimationCount, TotalFrameCount);
        }
    }

    private enum ToolAction
    {
        None,
        Export,
        Sync,
    }

    private enum SyncStatus
    {
        Created,
        Updated,
        Unchanged,
        Skipped,
    }

    private sealed record ExportResult(
        string CharacterName,
        string SourceFilePath,
        string OutputDirectory,
        IReadOnlyList<string> OutputLines);

    private sealed record CharacterSyncResult(
        SyncStatus Status,
        string CharacterName,
        string ResourcePath,
        int AnimationCount,
        int FrameCount,
        string Message)
    {
        public static CharacterSyncResult Created(string characterName, string resourcePath, int animationCount, int frameCount)
        {
            return new CharacterSyncResult(SyncStatus.Created, characterName, resourcePath, animationCount, frameCount, string.Empty);
        }

        public static CharacterSyncResult Updated(string characterName, string resourcePath, int animationCount, int frameCount)
        {
            return new CharacterSyncResult(SyncStatus.Updated, characterName, resourcePath, animationCount, frameCount, string.Empty);
        }

        public static CharacterSyncResult Unchanged(string characterName, string resourcePath, int animationCount, int frameCount)
        {
            return new CharacterSyncResult(SyncStatus.Unchanged, characterName, resourcePath, animationCount, frameCount, string.Empty);
        }

        public static CharacterSyncResult Skipped(string characterName, string resourcePath, string message)
        {
            return new CharacterSyncResult(SyncStatus.Skipped, characterName, resourcePath, 0, 0, message);
        }
    }

    private sealed class SyncSummary
    {
        public List<CharacterSyncResult> Results { get; } = new();
    }

    private sealed record HeadlessCommand(ToolAction Action, string ExportFilePath, HashSet<string> RequestedCharacters)
    {
        public static HeadlessCommand Parse(IReadOnlyList<string> args)
        {
            var action = ToolAction.None;
            string exportFilePath = null;
            var requestedCharacters = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < args.Count; index++)
            {
                var argument = args[index];

                if (string.Equals(argument, "--sync", StringComparison.Ordinal))
                {
                    action = ResolveAction(action, ToolAction.Sync);
                    continue;
                }

                if (argument.StartsWith("--export=", StringComparison.Ordinal))
                {
                    action = ResolveAction(action, ToolAction.Export);
                    exportFilePath = ExpandHomeDirectory(argument["--export=".Length..]);
                    continue;
                }

                if (string.Equals(argument, "--export", StringComparison.Ordinal))
                {
                    if (index + 1 >= args.Count)
                        throw new InvalidOperationException("Missing value for --export.");

                    action = ResolveAction(action, ToolAction.Export);
                    index++;
                    exportFilePath = ExpandHomeDirectory(args[index]);
                    continue;
                }

                if (argument.StartsWith("--character=", StringComparison.Ordinal))
                {
                    foreach (var characterName in argument["--character=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        requestedCharacters.Add(characterName);
                    continue;
                }

                if (string.Equals(argument, "--character", StringComparison.Ordinal))
                {
                    if (index + 1 >= args.Count)
                        throw new InvalidOperationException("Missing value for --character.");

                    index++;
                    foreach (var characterName in args[index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        requestedCharacters.Add(characterName);
                }
            }

            return new HeadlessCommand(action, exportFilePath, requestedCharacters);
        }

        private static ToolAction ResolveAction(ToolAction currentAction, ToolAction requestedAction)
        {
            if (currentAction == ToolAction.None || currentAction == requestedAction)
                return requestedAction;

            throw new InvalidOperationException("Specify only one headless action at a time.");
        }
    }
}
