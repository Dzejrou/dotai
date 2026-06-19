// Result of a run-plan generation attempt. Generation either fully succeeds (Plan set) or
// fails with an actionable Error and no plan at all — it never returns a partial plan, so
// callers can rely on Succeeded before touching Plan.
public sealed class DungeonRunPlanResult
{
    private DungeonRunPlanResult(DungeonRunPlan plan, string error)
    {
        Plan = plan;
        Error = error;
    }

    public DungeonRunPlan Plan { get; }

    public string Error { get; }

    public bool Succeeded => Plan != null && string.IsNullOrEmpty(Error);

    public static DungeonRunPlanResult Success(DungeonRunPlan plan)
    {
        return new DungeonRunPlanResult(plan, null);
    }

    public static DungeonRunPlanResult Failure(string error)
    {
        return new DungeonRunPlanResult(null, string.IsNullOrEmpty(error) ? "Unknown dungeon run-plan generation error." : error);
    }
}
