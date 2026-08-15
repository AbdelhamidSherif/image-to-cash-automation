namespace ImageToCash.Flow;

public enum StepStatus
{
    Ok,
    Warning,
    ManualReview,
    Skipped,
    Error
}

public sealed class StepRecord
{
    public int Index { get; set; }
    public required string Stage { get; init; }
    public required string Action { get; init; }
    public StepStatus Status { get; set; } = StepStatus.Ok;
    public string? Detail { get; set; }
    public string? Screenshot { get; set; }
}

public sealed class FlowResult
{
    public List<StepRecord> Steps { get; } = new();
    public bool Completed { get; set; }
    public string? Error { get; set; }

    public StepRecord Add(string stage, string action, StepStatus status = StepStatus.Ok, string? detail = null)
    {
        var rec = new StepRecord
        {
            Index = Steps.Count + 1,
            Stage = stage,
            Action = action,
            Status = status,
            Detail = detail
        };
        Steps.Add(rec);
        return rec;
    }
}
