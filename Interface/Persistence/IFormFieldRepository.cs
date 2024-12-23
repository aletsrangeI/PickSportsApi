using Domain.Entities;

namespace Interface.Persistence;

public interface IFormFieldRepository : IGenericRepository<FormField>
{
    public List<FormField> GetFormFieldByFormCatId(int id);
}