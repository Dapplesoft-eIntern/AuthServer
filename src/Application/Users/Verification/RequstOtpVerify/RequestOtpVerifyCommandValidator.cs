using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using FluentValidation;

namespace Application.Users.Verification.RequstOtpVerify;

internal class RequestOtpVerifyCommandValidator : AbstractValidator<RequestOtpVerifyCommand>
{
    public RequestOtpVerifyCommandValidator()
    {
        RuleFor(c => c.Destination)
            .Cascade(CascadeMode.Stop) // ✅ STOP if NotEmpty fails (prevents null in Must)
            .NotEmpty()
            .WithMessage("Destination cannot be empty.")
            .Must(dist =>
                Regex.IsMatch(dist, @"^(?:\+?88)?01[3-9]\d{8}$") ||
                new EmailAddressAttribute().IsValid(dist)
            )
            .WithMessage("Must Input valid Email address or Bangladeshi Phone number");
    }
}
