using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class FormFieldRepository : IFormFieldRepository
    {
        protected readonly ApplicationDbContext _dbContext;

        public FormFieldRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        #region Metodos sincronos

        public bool Insert(FormField entity)
        {
            if (entity.Type != "select")
            {
                entity.CatalogoId = null;  // Asegúrate de que CatalogoId sea null si no es un select
            }
            _dbContext.FormFields.Add(entity);
            return _dbContext.SaveChanges() > 0;
        }

        public bool Update(FormField entity)
        {
            _dbContext.FormFields.Update(entity);
            return _dbContext.SaveChanges() > 0;
        }

        public bool Delete(int id)
        {
            FormField FormField = Get(id);
            if (FormField == null) return false;
            _dbContext.FormFields.Remove(FormField);
            return _dbContext.SaveChanges() > 0;
        }

        public FormField Get(int id)
        {
            return _dbContext.FormFields.Find(id);
        }

        public IEnumerable<FormField> GetAll()
        {
            return _dbContext.FormFields;
        }

        public IEnumerable<FormField> GetAllWithPagination(int page, int pageSize)
        {
            IEnumerable<FormField> FormFields =
                _dbContext.FormFields.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return FormFields;
        }

        public int Count()
        {
            return _dbContext.FormFields.Count();
        }

        #endregion

        #region Metodos asincronos

        public async Task<bool> InsertAsync(FormField entity)
        {
            if (entity.Type != "select")
            {
                entity.CatalogoId = null;  // Asegúrate de que CatalogoId sea null si no es un select
            }
            await _dbContext.FormFields.AddAsync(entity);
            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateAsync(FormField entity)
        {
            _dbContext.FormFields.Update(entity);
            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            FormField FormField = await GetAsync(id);
            if (FormField == null) return false;
            _dbContext.FormFields.Remove(FormField);
            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task<FormField> GetAsync(int id)
        {
            return await _dbContext.FormFields.FindAsync(id);
        }

        public async Task<IEnumerable<FormField>> GetAllAsync()
        {
            return await _dbContext.FormFields.ToListAsync();
        }

        public async Task<IEnumerable<FormField>> GetAllWithPaginationAsync(int page, int pageSize)
        {
            IEnumerable<FormField> FormFields =
                await _dbContext.FormFields.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return FormFields;
        }

        public async Task<int> CountAsync()
        {
            return await _dbContext.FormFields.CountAsync();
        }

        #endregion

        public List<FormField> GetFormFieldByFormCatId(int id)
        {
            var result = (from ff in _dbContext.FormFields
                          join cc in _dbContext.ContenidoCatalogos
                          on ff.FormularioId equals cc.Id
                          where ff.FormularioId == id
                          select ff).OrderBy(a => a.Order).ToList();

            foreach (var formField in result)
            {
                if (formField.Type == "select" && formField.CatalogoId.HasValue)
                {
                    formField.Options = FillOptions(formField.Id, formField.CatalogoId.Value);
                }
            }

            return result;
        }


        private List<SelectFormOption> FillOptions(int formFieldId, int catalogoId)
        {
            List<SelectFormOption> options = _dbContext.ContenidoCatalogos
                              .Where(cc => cc.CatalogoId == catalogoId)
                              .Select(cc => new SelectFormOption
                              {
                                  Id = cc.Id,
                                  Nombre = cc.Nombre
                              }).ToList();
            return options;
        }
    }