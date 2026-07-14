namespace TourList.Shared.Routes;

/// <summary>Guest routes uploaded for one-time migration into the authenticated account.</summary>
public sealed class MergeRoutesRequest
{
    public List<RouteDto> Routes { get; init; } = [];
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
    public Guid ClientId { get; init; }
    public RouteMergeOutcome Outcome { get; init; }
    public Guid ServerRouteId { get; init; }
    public required string FinalName { get; init; }
}

public sealed class MergeCounts
{
    public int Created { get; init; }
    public int Deduped { get; init; }
    public int Renamed { get; init; }
}

/// <summary>Outcome of the one-time guest→account merge (acceptance scenario 13).</summary>
public sealed class MergeRoutesResponse
{
    /// <summary>True when the account had already migrated — the request was a no-op.</summary>
    public bool AlreadyMigrated { get; init; }

    public List<MergeRouteResult> Results { get; init; } = [];

    public MergeCounts Counts { get; init; } = new();
}
