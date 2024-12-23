using DTO.FormField;
using FluentValidation;

namespace Validator;

public class FormFieldDTOValidator : AbstractValidator<FormFieldDTO>
{
    public FormFieldDTOValidator()
    {
        RuleFor(x => x.Type).NotNull().NotEmpty();
        RuleFor(x => x.Label).NotNull().NotEmpty();
        RuleFor(x => x.Order).NotNull().NotEmpty();
    }
}