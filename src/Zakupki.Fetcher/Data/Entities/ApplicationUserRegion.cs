using System.ComponentModel.DataAnnotations;

namespace Zakupki.Fetcher.Data.Entities;

public class ApplicationUserRegion
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    public byte Region { get; set; }
}
