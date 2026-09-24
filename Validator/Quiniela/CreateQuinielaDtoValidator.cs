using DTO.Quiniela;
using FluentValidation;

namespace Validator.Quiniela;

public class CreateQuinielaDtoValidator : AbstractValidator<CreateQuinielaDto>
{
    public CreateQuinielaDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la quiniela es requerido.")
            .Length(3, 100).WithMessage("El nombre debe tener entre 3 y 100 caracteres.");

        RuleFor(x => x.LeagueId)
            .GreaterThan(0).WithMessage("Debe seleccionar una liga válida.");

        RuleFor(x => x.EntryFee)
            .GreaterThanOrEqualTo(0).WithMessage("La cuota de inscripción no puede ser negativa.");

        RuleFor(x => x.FirstPlacePct)
            .InclusiveBetween(0, 100).WithMessage("El porcentaje de 1er lugar debe ser entre 0 y 100.");

        RuleFor(x => x.SecondPlacePct)
            .InclusiveBetween(0, 100).WithMessage("El porcentaje de 2do lugar debe ser entre 0 y 100.");

        RuleFor(x => x.ThirdPlacePct)
            .InclusiveBetween(0, 100).WithMessage("El porcentaje de 3er lugar debe ser entre 0 y 100.");

        RuleFor(x => x)
            .Must(x => (x.FirstPlacePct + x.SecondPlacePct + x.ThirdPlacePct) == 100.00m)
            .WithMessage("La suma de los porcentajes de premios debe ser exactamente 100%.");
    }
}
