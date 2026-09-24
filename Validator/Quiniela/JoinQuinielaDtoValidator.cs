using DTO.Quiniela;
using FluentValidation;

namespace Validator.Quiniela;

public class JoinQuinielaDtoValidator : AbstractValidator<JoinQuinielaDto>
{
    public JoinQuinielaDtoValidator()
    {
        RuleFor(x => x.InviteCode)
            .NotEmpty().WithMessage("El código de invitación es requerido.")
            .MaximumLength(12).WithMessage("El código de invitación no puede exceder 12 caracteres.");

        RuleFor(x => x.Alias)
            .NotEmpty().WithMessage("El alias es requerido.")
            .Length(2, 50).WithMessage("El alias debe tener entre 2 y 50 caracteres.");
    }
}
