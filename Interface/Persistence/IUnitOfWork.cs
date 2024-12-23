namespace Interface.Persistence;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    ICatalogoRepository Catalogos { get; }
    IContenidoCatalogoRepository ContenidoCatalogos { get; }
    IEquipoLigaRepository EquipoLigas { get; }
    
    IFormFieldRepository FormFields { get; }

    // IIndiceCatalogoRepository IndiceCatalogos { get; }
    // IContenidoCatalogoApplication ContenidoCatalogos { get; }
    // IPacienteRepository Pacientes { get; }
    // IFormFieldRepository FormFields { get; }
    Task<int> Save(CancellationToken cancellationToken);
}