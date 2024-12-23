using System.ComponentModel.DataAnnotations;
using Domain.Common;

namespace Domain.Entities;

public class User : BaseAuditableEntity
{
    [Required] [MaxLength(50)] public string Username { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; }

    [Required] [MaxLength(100)] public string Password { get; set; }

    [Required] public int Role { get; set; }

    public string Token { get; set; }

    public ICollection<Quiniela> QuinielasOwned { get; set; }
    public ICollection<UsuariosQuiniela> UsuariosQuinielas { get; set; }
    public ICollection<Pronostico> Pronosticos { get; set; }
}