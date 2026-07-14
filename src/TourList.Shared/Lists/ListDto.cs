namespace TourList.Shared.Lists;

/// <summary>A content list in catalog/browse responses.</summary>
public sealed class ListDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public int EntryCount { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}
