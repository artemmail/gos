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
    [MaxLength(128)]
    public string Region { get; set; } = string.Empty;
}
