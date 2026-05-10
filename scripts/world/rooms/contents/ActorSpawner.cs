using Godot;

using System.Collections.Generic;

public partial class ActorSpawner : ActorSpawnPoint
{
    private readonly RandomNumberGenerator _random = CreateRandom();
    private RandomActorSpawnOption _cachedOption;
    private bool? _cachedIsActive;

    [Export]
    public Godot.Collections.Array<RandomActorSpawnOption> Options { get; set; } = new();

    [Export]
    public bool RandomizeOnRespawn { get; set; } = true;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float InactiveChance { get; set; } = 0.0f;

    [Export]
    public bool RandomizeInactivity { get; set; } = true;

    protected override Node2D SpawnActor()
    {
        if (!ShouldSpawnActor())
            return null;

        var validOptions = GetValidOptions();
        if (validOptions.Count == 0)
        {
            GD.PushWarning($"{nameof(ActorSpawner)} '{Name}' has no valid options with actor scenes and weight > 0.");
            return null;
        }

        if (!RandomizeOnRespawn && TrySpawnCachedOption(validOptions, out var cachedActor))
            return cachedActor;

        while (validOptions.Count > 0)
        {
            var option = RollOption(validOptions);
            if (option == null)
                break;

            var actor = InstantiateActorScene(option.ActorScene, option.ClearLootTable, nameof(ActorSpawner));
            if (actor != null)
            {
                if (!RandomizeOnRespawn)
                    _cachedOption = option;

                return actor;
            }

            validOptions.Remove(option);
        }

        GD.PushWarning($"{nameof(ActorSpawner)} '{Name}' could not spawn any configured actor scenes.");
        return null;
    }

    private bool TrySpawnCachedOption(List<RandomActorSpawnOption> validOptions, out Node2D actor)
    {
        actor = null;
        if (_cachedOption == null)
            return false;

        if (!validOptions.Contains(_cachedOption))
        {
            GD.PushWarning($"{nameof(ActorSpawner)} '{Name}' cached option is no longer valid. Clearing cache and rolling again.");
            _cachedOption = null;
            return false;
        }

        actor = InstantiateActorScene(_cachedOption.ActorScene, _cachedOption.ClearLootTable, nameof(ActorSpawner));
        if (actor != null)
            return true;

        GD.PushWarning($"{nameof(ActorSpawner)} '{Name}' cached option '{DescribeOption(_cachedOption)}' is no longer spawnable. Clearing cache and rolling again.");
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

    private bool ShouldSpawnActor()
    {
        if (RandomizeInactivity)
            return RollIsActive();

        if (_cachedIsActive.HasValue)
            return _cachedIsActive.Value;

        _cachedIsActive = RollIsActive();
        return _cachedIsActive.Value;
    }

    private bool RollIsActive()
    {
        var inactiveChance = Mathf.Clamp(InactiveChance, 0.0f, 1.0f);
        if (inactiveChance <= 0.0f)
            return true;

        if (inactiveChance >= 1.0f)
            return false;

        return _random.Randf() >= inactiveChance;
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
