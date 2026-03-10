using Godot;

public static class RuntimeSpriteLoader
{
    public static void AddAnimationFrames(
        SpriteFrames spriteFrames,
        string animationName,
        string actorAnimationRoot,
        string assetFolder,
        string direction,
        bool loops,
        string actorLabel,
        bool removeAnimationIfNoFrames)
    {
        spriteFrames.AddAnimation(animationName);
        spriteFrames.SetAnimationLoop(animationName, loops);
        var frameLoaded = 0;

        var frame = 0;
        while (frame <= 999)
        {
            var path = BuildFramePath(actorAnimationRoot, assetFolder, direction, frame);
            var absolutePath = ProjectSettings.GlobalizePath(path);
            if (!FileAccess.FileExists(absolutePath))
                break;

            var image = Image.LoadFromFile(absolutePath);
            if (image == null)
            {
                GD.PrintErr($"{actorLabel} failed to load frame image at '{path}'.");
                frame++;
                continue;
            }

            var texture = ImageTexture.CreateFromImage(image);
            if (texture != null)
            {
                spriteFrames.AddFrame(animationName, texture);
                frameLoaded++;
            }

            frame++;
        }

        if (frameLoaded == 0)
        {
            GD.PrintErr(
                $"{actorLabel} animation '{animationName}' has no frames at {actorAnimationRoot}/{assetFolder}/{direction}/");

            if (removeAnimationIfNoFrames)
                spriteFrames.RemoveAnimation(animationName);
        }
    }

    public static bool HasFrame(string actorAnimationRoot, string assetFolder, string direction, int frame)
    {
        var path = BuildFramePath(actorAnimationRoot, assetFolder, direction, frame);
        var absolutePath = ProjectSettings.GlobalizePath(path);
        return FileAccess.FileExists(absolutePath);
    }

    private static string BuildFramePath(string actorAnimationRoot, string assetFolder, string direction, int frame)
    {
        return $"res://{actorAnimationRoot}/{assetFolder}/{direction}/frame_{frame:000}.png";
    }
}
