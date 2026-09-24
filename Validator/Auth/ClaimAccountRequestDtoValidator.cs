using DTO.Auth;
using FluentValidation;

namespace Validator.Auth;

public class ClaimAccountRequestDtoValidator : AbstractValidator<ClaimAccountRequestDto>
{
    public ClaimAccountRequestDtoValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El token de activación es requerido.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es requerido.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
            .MaximumLength(150).WithMessage("El correo electrónico no puede exceder 150 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida.")
            .MinimumLength(6).WithMessage("La contraseña debe tener al menos 6 caracteres.");

        RuleFor(x => x.DisplayName)
            .MaximumLength(50).WithMessage("El nombre visible no puede exceder 50 caracteres.");
    }
}
