using Application.Abstractions.Messaging;
using Domain.Otps;

namespace Application.Users.Verification.VerifyOtp;

public sealed record VerifyOtpCommand (
    string Destination,
    string OtpToken,
    OtpType OtpType
) : ICommand;
