using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public class Pronostico : BaseAuditableEntity
{
    [ForeignKey("User")] public int UserId { get; set; }

    [ForeignKey("Quiniela")] public int QuinielaId { get; set; }

    [ForeignKey("Partido")] public int PartidoId { get; set; }

    [ForeignKey("PronosticoCatalogo")] public int PronosticoUsuario { get; set; }

    public User User { get; set; }
    public Quiniela Quiniela { get; set; }
    public Partido Partido { get; set; }
    public ContenidoCatalogo PronosticoCatalogo { get; set; }
}