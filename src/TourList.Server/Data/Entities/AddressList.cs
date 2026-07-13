namespace TourList.Server.Data.Entities;

/// <summary>
/// An admin-curated, publicly-visible named collection of addresses (the spec's "List";
/// named <c>AddressList</c> in code to avoid colliding with <see cref="System.Collections.Generic.List{T}"/>).
/// Deleting a list cascades to its <see cref="Entry"/> rows — but never reaches saved routes,
/// which hold snapshots (acceptance scenario 11).
/// </summary>
public class AddressList
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The admin who created the list (provenance; null after that user is removed).</summary>
    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Optimistic-concurrency token (SQL Server <c>rowversion</c>).</summary>
    public byte[]? RowVersion { get; set; }

    public ICollection<Entry> Entries { get; set; } = new List<Entry>();
}
