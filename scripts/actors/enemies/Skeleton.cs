using Godot;

[GlobalClass]
public partial class Skeleton : ActorBase, IAttackable, ITargetable, ISummonedUnit, IFactionAssignable
{
    [Export]
    public float Speed { get; set; } = 52.0f;

    [Export]
    public float AttackRange { get; set; } = 18.0f;

    [Export]
    public float AttackCooldown { get; set; } = 1.1f;

    [Export]
    public StringName AttackAnimation { get; set; } = "cross-punch";

    [Export]
    public int Health { get; set; } = 24;

    [Export]
    public int SummonedHealth { get; set; } = 20;

    [Export]
    public int MinAttackDamage { get; set; } = 1;

    [Export]
    public int MaxAttackDamage { get; set; } = 5;

    public bool CanBeTargeted => !IsDead;
    public override Faction Faction => _faction;
    public ISummoner Summoner => ResolveSummonState().Summoner;

    private Faction _faction = Factions.Enemies;
    private bool _sameFactionCollisionExceptionApplied;
    private SummonState _summon;

    public override void _Ready()
    {
        _summon = GetNode<SummonState>("SummonState");
        InitializeActor(
            GetNode<AnimatedSprite2D>("AnimatedSprite2D"),
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D"),
            GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D"));
        SetMovementSpeed(Speed);
        SetPrimaryActionController(new MeleeAttackController(AttackRange, AttackCooldown, AttackAnimation, MinAttackDamage, MaxAttackDamage));
        ConfigureBehaviors(CreateDefaultBehaviors());

        PlayIdleIfAvailable();
    }

    protected override void OnActorExitTree()
    {
        ClearSameFactionCollisionExceptions();
    }

    public void ApplyDamage(DamageInfo damageInfo)
    {
        if (!TryApplyIncomingDamage(damageInfo, out var damage, out var died))
            return;

        ShowFloatingDamageNumber(damage.ToString(), new Color(1.0f, 0.0f, 0.0f, 1.0f));
        if (died)
            StartDeath();
    }

    public void SetFaction(Faction faction)
    {
        _faction = faction ?? Factions.Enemies;
        if (!IsInsideTree())
            return;

        ClearSameFactionCollisionExceptions();
        ApplyFactionCombatGroup();
        if (ResolveSummonState().IsSummoned)
            ApplySameFactionCollisionExceptions();
        RefreshHealthLabel();
    }

    public void SetSummoner(ISummoner summoner)
    {
        var summonState = ResolveSummonState();
        var wasSummoned = summonState.IsSummoned;
        summonState.SetSummoner(summoner, SetFaction);
        if (wasSummoned && !summonState.IsSummoned)
        {
            ClearSameFactionCollisionExceptions();
            ApplyFactionCombatGroup();
            RefreshHealthLabel();
        }
    }

    public bool HasValidSummoner()
    {
        return ResolveSummonState().HasValidSummoner();
    }

    public bool IsOwnedBy(Node2D owner)
    {
        return ResolveSummonState().IsOwnedBy(owner);
    }

    private IActorBehavior[] CreateDefaultBehaviors()
    {
        var preset = ActorBehaviorPresets.CreateSceneBackedHostileMeleePreset();
        return preset.Behaviors;
    }

    private void StartDeath()
    {
        SetIsDead(true);
        ClearTarget();
        ClearSameFactionCollisionExceptions();
        SpawnCorpseAndFree();
    }

    private void ApplySameFactionCollisionExceptions()
    {
        if (_sameFactionCollisionExceptionApplied)
            return;

        if (!IsInsideTree() || GetTree() == null || this is not PhysicsBody2D skeletonPhysicsBody)
            return;

        var ownGroup = Factions.GetCombatGroup(Faction);
        if (string.IsNullOrEmpty(ownGroup))
            return;

        foreach (var node in GetTree().GetNodesInGroup(ownGroup))
        {
            if (node == this ||
                node is not PhysicsBody2D allyPhysicsBody ||
                !GodotObject.IsInstanceValid(allyPhysicsBody) ||
                !allyPhysicsBody.IsInsideTree())
            {
                continue;
            }

            skeletonPhysicsBody.AddCollisionExceptionWith(allyPhysicsBody);
            allyPhysicsBody.AddCollisionExceptionWith(skeletonPhysicsBody);
        }

        _sameFactionCollisionExceptionApplied = true;
    }

    private void ClearSameFactionCollisionExceptions()
    {
        if (this is not PhysicsBody2D skeletonPhysicsBody)
            return;

        var tree = GetTree();
        if (tree == null)
            return;

        var ownGroup = Factions.GetCombatGroup(Faction);
        if (string.IsNullOrEmpty(ownGroup))
            return;

        foreach (var node in tree.GetNodesInGroup(ownGroup))
        {
            if (node == this ||
                node is not PhysicsBody2D allyPhysicsBody ||
                !GodotObject.IsInstanceValid(allyPhysicsBody) ||
                !allyPhysicsBody.IsInsideTree())
            {
                continue;
            }

            skeletonPhysicsBody.RemoveCollisionExceptionWith(allyPhysicsBody);
            allyPhysicsBody.RemoveCollisionExceptionWith(skeletonPhysicsBody);
        }

        _sameFactionCollisionExceptionApplied = false;
    }

    protected override int MaxHealthValue => _summon?.IsSummoned == true ? SummonedHealth : Health;

    private SummonState ResolveSummonState()
    {
        _summon ??= GetNode<SummonState>("SummonState");
        return _summon;
    }
}
