using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Lists;

public sealed class UpdateListRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}
