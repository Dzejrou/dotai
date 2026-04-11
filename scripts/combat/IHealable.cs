public interface IHealable
{
    int CurrentHealth { get; }
    int MaxHealableHealth { get; }
    bool CanReceiveHealing { get; }
    void ApplyHealing(Healing healing);
}
