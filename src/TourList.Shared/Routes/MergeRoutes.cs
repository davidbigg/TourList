namespace TourList.Shared.Routes;

/// <summary>Guest routes uploaded for one-time migration into the authenticated account.</summary>
public sealed class MergeRoutesRequest
{
    public List<RouteDto> Routes { get; set; } = [];
}

public enum RouteMergeOutcome
{
    /// <summary>New content, name kept as-is.</summary>
    Created,

    /// <summary>Content already existed on the account (by fingerprint) — nothing created.</summary>
    DedupedExisting,

    /// <summary>New content whose name collided with an existing route — kept as a renamed copy.</summary>
    RenamedCopy,
}

public sealed class MergeRouteResult
{
    public Guid ClientId { get; set; }
    public RouteMergeOutcome Outcome { get; set; }
    public Guid ServerRouteId { get; set; }
    public required string FinalName { get; set; }
}

public sealed class MergeCounts
{
    public int Created { get; set; }
    public int Deduped { get; set; }
    public int Renamed { get; set; }
}

/// <summary>Outcome of the one-time guest→account merge (acceptance scenario 13).</summary>
public sealed class MergeRoutesResponse
{
    /// <summary>True when the account had already migrated — the request was a no-op.</summary>
    public bool AlreadyMigrated { get; set; }

    public List<MergeRouteResult> Results { get; set; } = [];

    public MergeCounts Counts { get; set; } = new();
}
