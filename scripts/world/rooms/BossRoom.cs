using Godot;

// Generic boss-room shell. It locks its forward (encounter) door the moment the player
// enters, drives the injected room encounter's lifecycle, and unlocks/completes the
// objective when the encounter reports completion. All boss- and summon-specific wiring
// lives in the injected encounter content, so the room stays reusable.
[GlobalClass]
public partial class BossRoom : Room
{
    [Export]
    public NodePath EncounterDoorPath { get; set; } = new("Scaled/Exits/NorthCenterDoor");

    private Door _encounterDoor;
    private bool _encounterDoorResolved;
    private IRoomEncounter _encounter;
    private Node _encounterNode;
    private bool _completionSubscribed;

    public override void _Ready()
    {
        base._Ready();
        SetEncounterDoorLocked(true);
    }

    public override void OnEnter()
    {
        base.OnEnter();

        // Lock the forward door immediately on every entry (also re-locks a persistent
        // instance that was completed/abandoned on a prior visit).
        SetEncounterDoorLocked(true);

        var encounter = ResolveEncounter();
        if (encounter == null)
            return;

        if (!_completionSubscribed)
        {
            encounter.EncounterCompleted += OnEncounterCompleted;
            _completionSubscribed = true;
        }

        encounter.BeginEncounter(this);
    }

    public override void OnExit()
    {
        ResolveEncounter()?.AbandonEncounter();

        // Reset the door visual/state for a clean re-entry of a persistent instance.
        SetEncounterDoorLocked(true);
        base.OnExit();
    }

    public override void _ExitTree()
    {
        if (_completionSubscribed && _encounter != null)
        {
            _encounter.EncounterCompleted -= OnEncounterCompleted;
            _completionSubscribed = false;
        }

        base._ExitTree();
    }

    private void OnEncounterCompleted()
    {
        // Unlock the objective regardless of any surviving encounter state.
        SetEncounterDoorLocked(false);
    }

    private IRoomEncounter ResolveEncounter()
    {
        if (_encounter != null && _encounterNode != null && GodotObject.IsInstanceValid(_encounterNode))
            return _encounter;

        _encounter = null;
        _encounterNode = null;
        if (GetInjectedContent() is IRoomEncounter encounter and Node node)
        {
            _encounter = encounter;
            _encounterNode = node;
        }

        return _encounter;
    }

    private void SetEncounterDoorLocked(bool isLocked)
    {
        var door = ResolveEncounterDoor();
        if (door != null)
            door.IsLocked = isLocked;
    }

    private Door ResolveEncounterDoor()
    {
        if (_encounterDoorResolved)
            return GodotObject.IsInstanceValid(_encounterDoor) ? _encounterDoor : null;

        _encounterDoorResolved = true;
        _encounterDoor = EncounterDoorPath.IsEmpty ? null : GetNodeOrNull<Door>(EncounterDoorPath);
        if (_encounterDoor == null)
            GD.PushError($"{nameof(BossRoom)} '{Name}' could not resolve encounter door at '{EncounterDoorPath}'.");

        return _encounterDoor;
    }
}
