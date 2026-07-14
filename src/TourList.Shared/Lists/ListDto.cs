namespace TourList.Shared.Lists;

/// <summary>A content list in catalog/browse responses.</summary>
public sealed class ListDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int EntryCount { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
