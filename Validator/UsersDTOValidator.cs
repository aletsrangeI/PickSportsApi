using DTO.User;
using FluentValidation;

namespace Validator;

public class UsersDTOValidator : AbstractValidator<UserDTO>
{
    public UsersDTOValidator()
    {
        RuleFor(x => x.Email).NotNull().NotEmpty();
    }
}