using Godot;

using System;
using System.Collections.Generic;

// Room-owned boss encounter content. It explicitly owns the boss's combat lifecycle
// instead of relying on ordinary aggro/timeout: it spawns a fresh boss on begin, pins
// both the boss and the player in combat via owner-scoped combat locks, activates summon
// spawners on phase entry, and tears everything down on boss death or room abandonment.
//
// The boss and summons are spawned on demand (boss on begin, summons on phase 2), so the
// base Content auto-respawn is intentionally bypassed.
[GlobalClass]
public partial class BossEncounter : Content, IRoomEncounter
{
    [Export]
    public NodePath BossSpawnerPath { get; set; } = new("BossSpawner");

    [Export]
    public NodePath SummonSpawnerRootPath { get; set; } = new("Summons");

    // When true the boss attacks and targets the player the instant the encounter begins.
    // When false the forward door still locks on entry, but the boss only engages once
    // the player first damages it.
    [Export]
    public bool AggressiveOnStart { get; set; } = true;

    // Distinct summon spawners activated when the boss enters phase 2.
    [Export]
    public int Phase2SummonCount { get; set; } = 2;

    public event Action EncounterCompleted;

    private readonly RandomNumberGenerator _random = new();
    private readonly List<ActorSpawner> _summonSpawners = new();
    private readonly HashSet<ActorSpawner> _activatedSpawners = new();

    private ActorSpawner _bossSpawner;
    private Actor _boss;
    private BossPhaseController _bossPhaseController;
    private CombatState _playerCombat;
    private bool _refsResolved;
    private bool _encounterActive;
    private bool _engaged;
    private bool _completed;

    public override void _Ready()
    {
        // Deliberately not calling base._Ready(): boss and summons are spawned on demand,
        // never auto-respawned from the actor root.
        _random.Randomize();
        ResolveReferences();
    }

    public override void _Process(double delta)
    {
        if (!_encounterActive || _engaged || _boss == null || !GodotObject.IsInstanceValid(_boss))
            return;

        // Aggressive: engage as soon as the player is resolvable. Non-aggressive: engage
        // once the player first damages the boss.
        if (AggressiveOnStart || _boss.CurrentHealth < _boss.MaxHealthValue)
            Engage();
    }

    public override void _ExitTree()
    {
        // Safety net: a freed encounter must never leave the (persistent) player pinned
        // in combat.
        TeardownEncounter();
        base._ExitTree();
    }

    public void BeginEncounter(Room room)
    {
        ResolveReferences();

        // Always start from a clean slate so a re-entered persistent room produces a
        // fresh phase-1 boss at full health with clean spawners.
        TeardownEncounter();

        _completed = false;
        _encounterActive = true;

        SpawnBoss();
        if (_boss != null && AggressiveOnStart)
            Engage();
    }

    public void AbandonEncounter()
    {
        TeardownEncounter();
    }

    // Activates up to `count` not-yet-activated summon spawners chosen at random, each
    // spawning its configured summon once. Exposed (with EnsureAllSpawnersOccupied) so the
    // phase-3 slice can fill the arena without reworking this content.
    public void ActivateRandomSpawners(int count)
    {
        if (count <= 0)
            return;

        var available = new List<ActorSpawner>();
        foreach (var spawner in _summonSpawners)
        {
            if (spawner != null && GodotObject.IsInstanceValid(spawner) && !_activatedSpawners.Contains(spawner))
                available.Add(spawner);
        }

        var activations = Math.Min(count, available.Count);
        for (var i = 0; i < activations; i++)
        {
            var index = _random.RandiRange(0, available.Count - 1);
            var spawner = available[index];
            available.RemoveAt(index);
            ActivateSpawner(spawner);
        }
    }

    // Ensures every configured summon spawner has one living summon. Spawners that already
    // hold a living summon are left untouched (never duplicated); every empty spawner -
    // whether it was never activated, or its summon has since died - (re)spawns one summon.
    // Used on phase-3 entry to bring the arena up to one summon per spawner. Generic and
    // spawner-name agnostic so other encounters/phases can reuse it.
    public void EnsureAllSpawnersOccupied()
    {
        foreach (var spawner in _summonSpawners)
        {
            if (spawner == null || !GodotObject.IsInstanceValid(spawner) || spawner.IsOccupied())
                continue;

            _activatedSpawners.Add(spawner);
            spawner.Respawn();
        }
    }

    private void Engage()
    {
        if (_engaged || _boss == null || !GodotObject.IsInstanceValid(_boss))
            return;

        var player = ResolvePlayer();
        if (player == null)
            return;

        _engaged = true;

        // Pin the boss onto the player and hold both sides in combat for the whole
        // encounter. The locks outlast the ordinary timeout, so dodging every attack
        // never drops either side out of combat or clears the boss's target.
        _boss.Combat?.AcquireCombatLock(this, player);

        _playerCombat = CombatState.ResolveFor(player);
        _playerCombat?.AcquireCombatLock(this);
    }

    private void SpawnBoss()
    {
        if (_bossSpawner == null)
        {
            GD.PushError($"{nameof(BossEncounter)} '{Name}' has no boss spawner; cannot start encounter.");
            return;
        }

        // Re-instantiating the boss guarantees a clean phase-1 state at full health.
        _bossSpawner.Respawn();
        _boss = _bossSpawner.CurrentSpawnedActor as Actor;
        if (_boss == null)
        {
            GD.PushError($"{nameof(BossEncounter)} '{Name}' boss spawner did not produce an {nameof(Actor)}.");
            return;
        }

        // The encounter drives targeting/combat: suppress ordinary aggro, wander and
        // leash on the boss.
        _boss.SetEncounterControlled(true);

        _bossPhaseController = FindBossPhaseController(_boss);
        ConnectBoss();
    }

    private void OnBossDied()
    {
        if (_completed)
            return;

        _completed = true;
        _encounterActive = false;
        _engaged = false;

        // Remove every surviving summon and release the combat locks; the objective
        // completes regardless of summon state.
        ClearSummons();
        ReleaseAllLocks();

        // The boss frees itself (corpse + queue free); just drop our references.
        DisconnectBoss();
        _boss = null;
        _bossPhaseController = null;

        EncounterCompleted?.Invoke();
    }

    private void OnBossPhaseEntered(int phase)
    {
        // Phase 2 seeds the arena with a random subset of spawners; phase 3 (Enrage) tops
        // it up so every spawner ends with a living summon, refilling any that died.
        if (phase == 2)
            ActivateRandomSpawners(Phase2SummonCount);
        else if (phase == 3)
            EnsureAllSpawnersOccupied();
    }

    private void TeardownEncounter()
    {
        ReleaseAllLocks();
        CancelAndClearBoss();
        ClearSummons();
        _activatedSpawners.Clear();
        _engaged = false;
        _encounterActive = false;
    }

    private void CancelAndClearBoss()
    {
        DisconnectBoss();

        var boss = _boss;
        _boss = null;
        _bossPhaseController = null;

        // Cancel any in-flight action/transition before the boss is freed so a queued
        // cast cannot fire on the way out.
        if (boss != null && GodotObject.IsInstanceValid(boss))
            boss.PrimaryActionController?.Cancel(boss);

        _bossSpawner?.ClearSpawnedActor();
    }

    private void ClearSummons()
    {
        foreach (var spawner in _summonSpawners)
        {
            if (spawner != null && GodotObject.IsInstanceValid(spawner))
                spawner.ClearSpawnedActor();
        }
    }

    private void ReleaseAllLocks()
    {
        // Encounter teardown: discard any residual timeout so boss death/abandonment ends
        // combat immediately instead of lingering for the timeout the killing blow just
        // refreshed. Independent lock owners (none here today) are still respected.
        if (_boss != null && GodotObject.IsInstanceValid(_boss))
            _boss.Combat?.ReleaseCombatLock(this, exitCombatWhenLast: true);

        if (_playerCombat != null && GodotObject.IsInstanceValid(_playerCombat))
            _playerCombat.ReleaseCombatLock(this, exitCombatWhenLast: true);

        _playerCombat = null;
    }

    private void ActivateSpawner(ActorSpawner spawner)
    {
        if (spawner == null || !GodotObject.IsInstanceValid(spawner) || _activatedSpawners.Contains(spawner))
            return;

        _activatedSpawners.Add(spawner);
        spawner.Respawn();
    }

    private void ConnectBoss()
    {
        if (_boss != null)
        {
            var diedCallable = new Callable(this, nameof(OnBossDied));
            if (!_boss.IsConnected(CombatCharacter.SignalName.Died, diedCallable))
                _boss.Connect(CombatCharacter.SignalName.Died, diedCallable, (uint)ConnectFlags.OneShot);
        }

        if (_bossPhaseController != null)
        {
            var phaseCallable = new Callable(this, nameof(OnBossPhaseEntered));
            if (!_bossPhaseController.IsConnected(BossPhaseController.SignalName.PhaseEntered, phaseCallable))
                _bossPhaseController.Connect(BossPhaseController.SignalName.PhaseEntered, phaseCallable);
        }
    }

    private void DisconnectBoss()
    {
        if (_boss != null && GodotObject.IsInstanceValid(_boss))
        {
            var diedCallable = new Callable(this, nameof(OnBossDied));
            if (_boss.IsConnected(CombatCharacter.SignalName.Died, diedCallable))
                _boss.Disconnect(CombatCharacter.SignalName.Died, diedCallable);
        }

        if (_bossPhaseController != null && GodotObject.IsInstanceValid(_bossPhaseController))
        {
            var phaseCallable = new Callable(this, nameof(OnBossPhaseEntered));
            if (_bossPhaseController.IsConnected(BossPhaseController.SignalName.PhaseEntered, phaseCallable))
                _bossPhaseController.Disconnect(BossPhaseController.SignalName.PhaseEntered, phaseCallable);
        }
    }

    private void ResolveReferences()
    {
        if (_refsResolved)
            return;

        _refsResolved = true;

        _bossSpawner = BossSpawnerPath.IsEmpty ? null : GetNodeOrNull<ActorSpawner>(BossSpawnerPath);
        if (_bossSpawner == null)
            GD.PushError($"{nameof(BossEncounter)} '{Name}' could not resolve boss spawner at '{BossSpawnerPath}'.");

        _summonSpawners.Clear();
        var summonRoot = SummonSpawnerRootPath.IsEmpty ? null : GetNodeOrNull<Node>(SummonSpawnerRootPath);
        if (summonRoot != null)
        {
            foreach (var child in summonRoot.GetChildren())
            {
                if (child is ActorSpawner spawner)
                    _summonSpawners.Add(spawner);
            }
        }

        if (_summonSpawners.Count == 0)
            GD.PushWarning($"{nameof(BossEncounter)} '{Name}' found no summon spawners under '{SummonSpawnerRootPath}'.");
    }

    private Player ResolvePlayer()
    {
        foreach (var node in GetTree().GetNodesInGroup(CombatGroups.Actors))
        {
            if (node is Player player && GodotObject.IsInstanceValid(player))
                return player;
        }

        return null;
    }

    private static BossPhaseController FindBossPhaseController(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is BossPhaseController controller)
                return controller;

            var nested = FindBossPhaseController(child);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
