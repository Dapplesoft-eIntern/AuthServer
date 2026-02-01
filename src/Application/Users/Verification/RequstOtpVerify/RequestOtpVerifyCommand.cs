using Application.Abstractions.Messaging;
using Domain.Otps;

namespace Application.Users.Verification.RequstOtpVerify;

public sealed record RequestOtpVerifyCommand(
    string Destination,
    OtpType OtpType,
    double? Delay = 2
) : ICommand<RequestOtpVerifyResponse>;
