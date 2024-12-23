using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public class UsuariosQuiniela : BaseAuditableEntity
{
    [ForeignKey("User")] public int UserId { get; set; }

    [ForeignKey("Quiniela")] public int QuinielaId { get; set; }

    public User User { get; set; }
    public Quiniela Quiniela { get; set; }
}