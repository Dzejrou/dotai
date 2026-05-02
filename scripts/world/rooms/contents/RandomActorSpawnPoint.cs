using Godot;

using System.Collections.Generic;

public partial class RandomActorSpawnPoint : ActorSpawnPoint
{
    private readonly RandomNumberGenerator _random = CreateRandom();
    private RandomActorSpawnOption _cachedOption;

    [Export]
    public Godot.Collections.Array<RandomActorSpawnOption> Options { get; set; } = new();

    [Export]
    public bool RandomizeOnRespawn { get; set; } = true;

    protected override Node2D SpawnActor()
    {
        var validOptions = GetValidOptions();
        if (validOptions.Count == 0)
        {
            GD.PushWarning($"{nameof(RandomActorSpawnPoint)} '{Name}' has no valid options with actor scenes and weight > 0.");
            return null;
        }

        if (!RandomizeOnRespawn && TrySpawnCachedOption(validOptions, out var cachedActor))
            return cachedActor;

        while (validOptions.Count > 0)
        {
            var option = RollOption(validOptions);
            if (option == null)
                break;

            var actor = InstantiateActorScene(option.ActorScene, option.ClearLootTable, nameof(RandomActorSpawnPoint));
            if (actor != null)
            {
                if (!RandomizeOnRespawn)
                    _cachedOption = option;

                return actor;
            }

            validOptions.Remove(option);
        }

        GD.PushWarning($"{nameof(RandomActorSpawnPoint)} '{Name}' could not spawn any configured actor scenes.");
        return null;
    }

    private bool TrySpawnCachedOption(List<RandomActorSpawnOption> validOptions, out Node2D actor)
    {
        actor = null;
        if (_cachedOption == null)
            return false;

        if (!validOptions.Contains(_cachedOption))
        {
            GD.PushWarning($"{nameof(RandomActorSpawnPoint)} '{Name}' cached option is no longer valid. Clearing cache and rolling again.");
            _cachedOption = null;
            return false;
        }

        actor = InstantiateActorScene(_cachedOption.ActorScene, _cachedOption.ClearLootTable, nameof(RandomActorSpawnPoint));
        if (actor != null)
            return true;

        GD.PushWarning($"{nameof(RandomActorSpawnPoint)} '{Name}' cached option '{DescribeOption(_cachedOption)}' is no longer spawnable. Clearing cache and rolling again.");
        validOptions.Remove(_cachedOption);
        _cachedOption = null;
        actor = null;
        return false;
    }

    private List<RandomActorSpawnOption> GetValidOptions()
    {
        var validOptions = new List<RandomActorSpawnOption>();
        if (Options == null)
            return validOptions;

        foreach (var option in Options)
        {
            if (option?.IsConfigured == true)
                validOptions.Add(option);
        }

        return validOptions;
    }

    private RandomActorSpawnOption RollOption(List<RandomActorSpawnOption> validOptions)
    {
        var totalWeight = 0.0f;
        foreach (var option in validOptions)
            totalWeight += option.Weight;

        if (totalWeight <= 0.0f)
            return null;

        var roll = _random.RandfRange(0.0f, totalWeight);
        var cumulativeWeight = 0.0f;
        RandomActorSpawnOption lastOption = null;
        foreach (var option in validOptions)
        {
            cumulativeWeight += option.Weight;
            lastOption = option;
            if (roll <= cumulativeWeight)
                return option;
        }

        return lastOption;
    }

    private static RandomNumberGenerator CreateRandom()
    {
        var random = new RandomNumberGenerator();
        random.Randomize();
        return random;
    }

    private static string DescribeOption(RandomActorSpawnOption option)
    {
        return option?.ActorScene?.ResourcePath ?? "<missing actor scene>";
    }
}
