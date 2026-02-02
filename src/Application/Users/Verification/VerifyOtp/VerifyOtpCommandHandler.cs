using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Otps;
using Application.Common;
using Application.Otps;
using Domain.Otps;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Users.Verification.VerifyOtp;

internal sealed class VerifyOtpCommandHandler(
    IApplicationDbContext context,
    IOtpProviderService otpProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<VerifyOtpCommandHandler> logger
) : ICommandHandler<VerifyOtpCommand>
{
    public async Task<Result> Handle(VerifyOtpCommand command, CancellationToken cancellationToken)
    {
        // Resolve destination type (email / phone)
        OtpDestinationType destinationType;

        try
        {
            destinationType = OtpDestinationResolver.Resolve(command.Destination);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }

        string normalizedDestination = destinationType switch
        {
            OtpDestinationType.Email => Normalizer.EmailAddressLowerCase(command.Destination),
            OtpDestinationType.Phone => Normalizer.PhoneNumber(command.Destination),
            _ => command.Destination
        };

        // Load user
        User? user = destinationType switch
        {
            OtpDestinationType.Email => await context.Users.FirstOrDefaultAsync(
                x => x.Email == normalizedDestination,
                cancellationToken
            ),

            OtpDestinationType.Phone => await context.Users.FirstOrDefaultAsync(
                x => x.Phone != null && 
                x.Phone == normalizedDestination,
                cancellationToken
            ),

            _ => null,
        };

        if (user is null)
        {
            return Result.Failure("Entered Email or Phone Number is not in use in ther user profile");
        }

        if (command.OtpType == OtpType.Verification && user.IsVerified)
        {
            return Result.Failure("User Already Verified");
        }

        // Verify OTP
        Result otpResult = await otpProvider.VerifyOtpAsync(
            command.Destination,
            command.OtpToken,
            command.OtpType,
            cancellationToken
        );

        if (otpResult.IsFailure)
        {
            logger.LogError("Otp Verification Failed : {Error}", otpResult.Error);
            return Result.Failure($"Otp Verification Failed : {otpResult.Error}");
        }

        if (command.OtpType == OtpType.Verification)
        {
            user.IsVerified = true;
            user.UpdatedAt = dateTimeProvider.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
