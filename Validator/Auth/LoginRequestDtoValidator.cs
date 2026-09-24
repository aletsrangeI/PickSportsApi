using DTO.Auth;
using FluentValidation;

namespace Validator.Auth;

public class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico o usuario es requerido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida.");
    }
}
