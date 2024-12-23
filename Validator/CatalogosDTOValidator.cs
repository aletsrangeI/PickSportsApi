using DTO.Catalogo;
using FluentValidation;

namespace Validator;

public class CatalogosDTOValidator : AbstractValidator<CatalogoDTO>
{
    public CatalogosDTOValidator()
    {
        RuleFor(x => x.Nombre).NotNull().NotEmpty();
        RuleFor(x => x.Descripcion).NotNull().NotEmpty();
    }
}