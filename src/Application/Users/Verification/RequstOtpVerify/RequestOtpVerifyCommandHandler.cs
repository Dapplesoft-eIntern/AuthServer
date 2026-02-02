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

namespace Application.Users.Verification.RequstOtpVerify;

internal sealed class RequestOtpVerifyCommandHandler(
    IApplicationDbContext context,
    IOtpProviderService otpProviderService,
    IDateTimeProvider dateTimeProvider,
    ILogger<RequestOtpVerifyCommandHandler> logger
) : ICommandHandler<RequestOtpVerifyCommand, RequestOtpVerifyResponse>
{
    public async Task<Result<RequestOtpVerifyResponse>> Handle(
        RequestOtpVerifyCommand command,
        CancellationToken cancellationToken
    )
    {
        // Resolve destination type
        OtpDestinationType destinationType;

        try
        {
            destinationType = OtpDestinationResolver.Resolve(command.Destination);
        }
        catch (Exception ex)
        {
            return Result.Failure<RequestOtpVerifyResponse>(ex.Message);
        }

        // Normalize destination
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
                x => x.Email != null && x.Email == normalizedDestination,
                cancellationToken
            ),

            OtpDestinationType.Phone => await context.Users.FirstOrDefaultAsync(
                x => x.Phone != null && x.Phone == normalizedDestination,
                cancellationToken
            ),

            _ => null,
        };

        if (user is null)
        {
            return Result.Failure<RequestOtpVerifyResponse>("User not found.");
        }

        // Check verification status
        if (command.OtpType == OtpType.Verification && user.IsVerified)
        {
            return Result.Failure<RequestOtpVerifyResponse>("User is already verified.");
        }

        // Find the latest matching OTP for this destination and type
        Otp? otp = await context.Otp
            .Where(o =>
                o.Destination == normalizedDestination &&
                o.OtpType == command.OtpType &&
                !o.IsUsed &&
                !o.IsExpired)
            .OrderByDescending(o => o.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp != null && otp.CreatedAt.Add(otp.Delay) > dateTimeProvider.UtcNow)
        {
            var earlyResponse = new RequestOtpVerifyResponse(
                Id: otp.Id,
                Destination: normalizedDestination,
                Message: "An OTP has already been sent recently. Please wait before requesting a new one.",
                CreatedAt: otp.CreatedAt,
                ExpiresAt: otp.ExpiresAt,
                Delay: otp.Delay.TotalSeconds,
                WaitForSeconds: (int)(otp.CreatedAt.AddSeconds(otp.Delay.TotalSeconds) - dateTimeProvider.UtcNow).TotalSeconds
            );
            return earlyResponse;
        }

        // Send OTP (this internally creates and sends OTP)
        Result<Guid> otpResult = await otpProviderService.SendOtpAsync(
            command.Destination,
            command.OtpType,
            cancellationToken
        );

        if (otpResult.IsFailure)
        {
            logger.LogWarning("SMS failed: {Error}", otpResult.Error);

            return Result.Failure<RequestOtpVerifyResponse>(otpResult.Error);
        }
        
        var response = new RequestOtpVerifyResponse(
            Id: otpResult.Value,
            Destination: normalizedDestination
        );
        
        return response;
    }
}
