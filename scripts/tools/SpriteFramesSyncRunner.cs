using Godot;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class SpriteFramesSyncRunner : Node
{
    private const string AssetsRoot = "res://assets";
    private const string AnimationsDirectoryName = "animations";
    private const string ResourcesRoot = "res://resources/animations";
    private const string DefaultAnimationName = "default";
    private const float DefaultAnimationSpeed = 5.0f;

    public override void _Ready()
    {
        var exitCode = 0;

        try
        {
            var result = RunSync();
            PrintSummary(result);
        }
        catch (Exception exception)
        {
            exitCode = 1;
            GD.PushError($"SpriteFrames sync failed: {exception.Message}");
            GD.PrintErr(exception.ToString());
        }

        GetTree().Quit(exitCode);
    }

    private SyncSummary RunSync()
    {
        var requestedCharacters = ParseRequestedCharacters(OS.GetCmdlineUserArgs());
        EnsureResourcesDirectoryExists();

        var characterNames = GetSubdirectories(AssetsRoot)
            .Where(characterName => DirExists($"{AssetsRoot}/{characterName}/{AnimationsDirectoryName}"))
            .OrderBy(characterName => characterName, StringComparer.Ordinal)
            .ToList();

        if (requestedCharacters.Count > 0)
        {
            var missingCharacters = requestedCharacters
                .Where(requestedCharacter => !characterNames.Contains(requestedCharacter, StringComparer.Ordinal))
                .OrderBy(characterName => characterName, StringComparer.Ordinal)
                .ToList();

            if (missingCharacters.Count > 0)
                throw new InvalidOperationException($"Requested character assets not found: {string.Join(", ", missingCharacters)}");

            characterNames = characterNames
                .Where(characterName => requestedCharacters.Contains(characterName))
                .ToList();
        }

        if (characterNames.Count == 0)
            return new SyncSummary();

        var summary = new SyncSummary();
        foreach (var characterName in characterNames)
        {
            var result = SyncCharacter(characterName);
            summary.Results.Add(result);
        }

        return summary;
    }

    private CharacterSyncResult SyncCharacter(string characterName)
    {
        var animationsRoot = $"{AssetsRoot}/{characterName}/{AnimationsDirectoryName}";
        var resourcePath = $"{ResourcesRoot}/{characterName}_spriteframes.tres";
        var manifest = BuildManifest(characterName, animationsRoot);

        if (manifest.Animations.Count == 0)
            return CharacterSyncResult.Skipped(characterName, resourcePath, "no animation frame folders found");

        var existingManifest = LoadExistingManifest(resourcePath);
        var desiredUid = ResolveDesiredResourceUid(resourcePath);
        var currentUid = GetKnownResourceUid(resourcePath);

        var manifestMatches = existingManifest != null && existingManifest.Equals(manifest);
        if (manifestMatches && currentUid == desiredUid)
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

    private SpriteFramesManifest BuildManifest(string characterName, string animationsRoot)
    {
        var animationSpecs = new List<AnimationSpec>();

        foreach (var animationName in GetSubdirectories(animationsRoot).OrderBy(name => name, StringComparer.Ordinal))
        {
            var animationRoot = $"{animationsRoot}/{animationName}";
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
                    animationName,
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

        var spriteFrames = ResourceLoader.Load<SpriteFrames>(resourcePath);
        if (spriteFrames == null)
            throw new InvalidOperationException($"Unable to load existing SpriteFrames resource at {resourcePath}.");

        var animationSpecs = new List<AnimationSpec>();
        foreach (var animationName in spriteFrames.GetAnimationNames().Select(name => name.ToString()).OrderBy(name => name, StringComparer.Ordinal))
        {
            if (string.Equals(animationName, DefaultAnimationName, StringComparison.Ordinal))
                continue;

            var separatorIndex = animationName.LastIndexOf('_');
            var animationBaseName = separatorIndex >= 0 ? animationName[..separatorIndex] : animationName;
            var framePaths = new string[spriteFrames.GetFrameCount(animationName)];
            for (var frameIndex = 0; frameIndex < framePaths.Length; frameIndex++)
            {
                var texture = spriteFrames.GetFrameTexture(animationName, frameIndex);
                framePaths[frameIndex] = texture?.ResourcePath ?? string.Empty;
            }

            animationSpecs.Add(new AnimationSpec(
                animationName,
                animationBaseName,
                spriteFrames.GetAnimationLoop(animationName),
                (float)spriteFrames.GetAnimationSpeed(animationName),
                framePaths));
        }

        return new SpriteFramesManifest(string.Empty, animationSpecs);
    }

    private void ApplyManifest(SpriteFrames spriteFrames, SpriteFramesManifest manifest)
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

    private void PrintSummary(SyncSummary summary)
    {
        var createdCount = summary.Results.Count(result => result.Status == SyncStatus.Created);
        var updatedCount = summary.Results.Count(result => result.Status == SyncStatus.Updated);
        var unchangedCount = summary.Results.Count(result => result.Status == SyncStatus.Unchanged);
        var skippedCount = summary.Results.Count(result => result.Status == SyncStatus.Skipped);

        if (summary.Results.Count == 0)
        {
            GD.Print("SpriteFrames sync complete: no character animation folders found.");
            return;
        }

        GD.Print($"SpriteFrames sync complete: {createdCount} created, {updatedCount} updated, {unchangedCount} unchanged, {skippedCount} skipped.");

        foreach (var result in summary.Results)
        {
            switch (result.Status)
            {
                case SyncStatus.Created:
                case SyncStatus.Updated:
                    GD.Print($" - {result.Status.ToString().ToLowerInvariant()}: {result.CharacterName} -> {result.ResourcePath} ({result.AnimationCount} animations, {result.FrameCount} frames)");
                    break;
                case SyncStatus.Unchanged:
                    GD.Print($" - unchanged: {result.CharacterName} ({result.AnimationCount} animations, {result.FrameCount} frames)");
                    break;
                case SyncStatus.Skipped:
                    GD.Print($" - skipped: {result.CharacterName} ({result.Message})");
                    break;
            }
        }
    }

    private static HashSet<string> ParseRequestedCharacters(IReadOnlyList<string> args)
    {
        var requestedCharacters = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.StartsWith("--character=", StringComparison.Ordinal))
            {
                AddRequestedCharacters(argument["--character=".Length..], requestedCharacters);
                continue;
            }

            if (string.Equals(argument, "--character", StringComparison.Ordinal) && index + 1 < args.Count)
            {
                index++;
                AddRequestedCharacters(args[index], requestedCharacters);
            }
        }

        return requestedCharacters;
    }

    private static void AddRequestedCharacters(string value, ISet<string> requestedCharacters)
    {
        foreach (var characterName in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            requestedCharacters.Add(characterName);
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

            var uidText = line.Substring(uidStart, uidEnd - uidStart);
            return ResourceUid.TextToId(uidText);
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

                var uidText = line.Substring(uidStart, uidEnd - uidStart);
                var uid = ResourceUid.TextToId(uidText);
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

            return ExtractFrameNumber(left).CompareTo(ExtractFrameNumber(right)) is var numericComparison && numericComparison != 0
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
        string BaseName,
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

    private enum SyncStatus
    {
        Created,
        Updated,
        Unchanged,
        Skipped,
    }

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
}
