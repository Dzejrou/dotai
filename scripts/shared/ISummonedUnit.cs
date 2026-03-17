public interface ISummonedUnit
{
    ISummoner Summoner { get; }
    void SetSummoner(ISummoner summoner);
    bool HasValidSummoner();
}
