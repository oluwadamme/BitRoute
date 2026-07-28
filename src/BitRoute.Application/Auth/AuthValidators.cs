using FluentValidation;

namespace BitRoute.Application.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(100);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Only presence checks: login must not leak the password policy or email shape.
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.Password).NotEmpty();
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(r => r.RefreshToken).NotEmpty();
    }
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(r => r.RefreshToken).NotEmpty();
    }
}

public sealed class ProvisionOperatorRequestValidator : AbstractValidator<ProvisionOperatorRequest>
{
    public ProvisionOperatorRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(100);
    }
}
