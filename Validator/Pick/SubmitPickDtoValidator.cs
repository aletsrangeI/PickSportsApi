using DTO.Pick;
using FluentValidation;

namespace Validator.Pick;

public class SubmitPickDtoValidator : AbstractValidator<SubmitPickRequestDto>
{
    public SubmitPickDtoValidator()
    {
        RuleFor(x => x.MatchId)
            .GreaterThan(0).WithMessage("El ID del partido debe ser mayor a 0.");

        RuleFor(x => x.PickAbbr)
            .NotEmpty().WithMessage("La selección del pronóstico es requerida.")
            .MaximumLength(10).WithMessage("La abreviatura de selección no puede exceder 10 caracteres.");
    }
}
