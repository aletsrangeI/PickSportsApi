using DTO.Catalogo;
using FluentValidation;

namespace Validator;

public class ContenidoCatalogosDTOValidator : AbstractValidator<CatalogoDTO>
{
    public ContenidoCatalogosDTOValidator()
    {
        RuleFor(x => x.Nombre).NotNull().NotEmpty();
        RuleFor(x => x.Descripcion).NotNull().NotEmpty();
    }
}