using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public class Quiniela : BaseAuditableEntity
{
    [ForeignKey("Liga")] public int IdLiga { get; set; }
    public ContenidoCatalogo Liga { get; set; }

    [ForeignKey("Owner")] public int OwnerId { get; set; }
    public User Owner { get; set; }

    public ICollection<UsuariosQuiniela> UsuariosQuinielas { get; set; }
    public ICollection<Pronostico> Pronosticos { get; set; }
}