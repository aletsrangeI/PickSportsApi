using DTO.EquipoLiga;
using FluentValidation;

namespace Validator;

public class EquipoLigaDTOValidator : AbstractValidator<EquipoLigaDTO>
{
    public EquipoLigaDTOValidator()
    {
        RuleFor(x => x.EquipoId).NotNull().NotEmpty();
        RuleFor(x => x.LigaId).NotNull().NotEmpty();
    }
}