using Domain.Entities;
using Interface.Persistence;
using Persistence.Context;

namespace Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    public UnitOfWork(
        ApplicationDbContext dbContext,
        IUserRepository userRepository,
        ICatalogoRepository catalogoRepository,
        IContenidoCatalogoRepository contenidoCatalogoRepository,
        IEquipoLigaRepository equipoLigaRepository,
        IFormFieldRepository formFieldRepository
    )
    {
        _dbContext = dbContext;
        Users = userRepository;
        Catalogos = catalogoRepository;
        ContenidoCatalogos = contenidoCatalogoRepository;
        EquipoLigas = equipoLigaRepository;
        FormFields = formFieldRepository;
    }

    public IUserRepository Users { get; }
    public ICatalogoRepository Catalogos { get; }
    public IContenidoCatalogoRepository ContenidoCatalogos { get; }
    public IEquipoLigaRepository EquipoLigas { get; }
    public IFormFieldRepository FormFields { get; }

    public async Task<int> Save(CancellationToken cancellationToken)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}