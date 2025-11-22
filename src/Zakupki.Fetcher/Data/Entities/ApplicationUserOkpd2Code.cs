using System.ComponentModel.DataAnnotations;

namespace Zakupki.Fetcher.Data.Entities;

public class ApplicationUserOkpd2Code
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public int Okpd2CodeId { get; set; }

    public Okpd2Code? Okpd2Code { get; set; }
}
