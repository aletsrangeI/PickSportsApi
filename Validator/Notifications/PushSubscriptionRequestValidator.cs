using DTO.Notifications;
using FluentValidation;

namespace Validator.Notifications;

public class PushSubscriptionRequestValidator : AbstractValidator<PushSubscriptionRequestDto>
{
    public PushSubscriptionRequestValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("El endpoint de notificación push es obligatorio.")
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("El endpoint de notificación push debe ser una URL válida.");

        RuleFor(x => x.GetP256dh())
            .NotEmpty().WithMessage("La clave criptográfica p256dh es requerida.");

        RuleFor(x => x.GetAuth())
            .NotEmpty().WithMessage("La clave de autenticación auth es requerida.");

        RuleFor(x => x.DeviceDescription)
            .MaximumLength(200).WithMessage("La descripción del dispositivo no puede exceder 200 caracteres.");
    }
}
