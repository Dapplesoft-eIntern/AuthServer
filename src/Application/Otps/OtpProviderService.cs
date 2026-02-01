using Application.Otps.OtpTemplate;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Otps;
using Application.Abstractions.SMS;
using Application.Common;
using Domain.Otps;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Otps;

public sealed class OtpProviderService(
    ISmsService smsService,
    IEmailService emailService,
    IApplicationDbContext context,
    IOtpGenerator otpGenerator,
    IDateTimeProvider dateTimeProvider
) : IOtpProviderService
{
    private const int OtpExpiryMinutes = 3;
    private const double Delay = 2;
    private OtpDestinationType destinationType;

    public async Task<Result<Guid>> SendOtpAsync(
        string destination,
        OtpType? otpType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return Result.Failure<Guid>(
                "Either Email or Phone number must be provided.");
        }

        try
        {
            destinationType = OtpDestinationResolver.Resolve(destination);
        }
        catch (Exception ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        string normalizedDestination = destinationType switch
        {
            OtpDestinationType.Email => Normalizer.EmailAddressLowerCase(destination),
            OtpDestinationType.Phone => Normalizer.PhoneNumber(destination),
            _ => destination
        };

        string? userName = null;

        if (destinationType == OtpDestinationType.Email)
        {
            userName = await context.Users
                .Where(u => u.Email == normalizedDestination)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else if (destinationType == OtpDestinationType.Phone)
        {
            userName = await context.Users
                .Where(u => u.Phone == normalizedDestination)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }



        string otpValue = otpGenerator.GenerateOtp(4);

        // Send OTP first
        Result sendResult = destinationType switch
        {
            OtpDestinationType.Phone =>
                await SendSmsOtp(normalizedDestination, otpValue, userName ?? "User", cancellationToken),

            OtpDestinationType.Email =>
                await SendEmailOtp(normalizedDestination, otpValue, userName ?? "User", cancellationToken),

            _ => Result.Failure("Unsupported OTP destination")
        };

        if (sendResult.IsFailure)
        {
            return Result.Failure<Guid>(sendResult.Error);
        }

        // save OTP in Db after successfuly send
        var otp = new Otp
        {
            Id = Guid.NewGuid(),
            OtpToken = otpValue,
            Destination = normalizedDestination,
            Delay = TimeSpan.FromMinutes(Delay),
            OtpType = otpType ?? OtpType.Default,
            ExpiresAt = dateTimeProvider.UtcNow.AddMinutes(OtpExpiryMinutes),
            CreatedAt = dateTimeProvider.UtcNow,
            IsExpired = false,
            IsUsed = false,
        };

        context.Otp.Add(otp);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(otp.Id);
    }

    private async Task<Result> SendSmsOtp(
        string phone,
        string otp,
        string userName,
        CancellationToken ct)
    {
        string message =
            $"Dear {userName}, Your OTP is {otp}. It will expire in {OtpExpiryMinutes} minutes.";

        Result otpResult = await smsService.SendOtpAsync(Normalizer.PhoneNumber(phone), message, ct);

        if (otpResult.IsFailure)
        {
            return Result.Failure(otpResult.Error);
        }

        return Result.Success();
    }

    private async Task<Result> SendEmailOtp(
        string email,
        string otp,
        string userName,
        CancellationToken ct)
    {
        string templatePath = Path.Combine(AppContext.BaseDirectory, "OtpTemplate", "OtpHtml.html");
        if (!File.Exists(templatePath))
        {
            return Result.Failure($"Email template not found: {templatePath}");
        }
        string otpBoxesHtml = OtpEmailBuilder.BuildHorizontalOtpBoxes(otp);
        // Use the async overload that returns Task<string> by passing the CancellationToken (ct).
        string body = await EmailTemplateRenderer.RenderAsync(
            templatePath,
            new Dictionary<string, string>
            {
                ["NAME"] = userName, // or load from DB if you want
                ["OTP_BOXES"] = otpBoxesHtml,
                ["EXPIRY_MINUTES"] = "3",
            },
            ct
        );
        var message = new EmailMessage
        {
            To = email,
            Subject = "Your OTP Code",
            Body = body,
        };

        Result otpResult = await emailService.SendAsync(message, ct);

        if (otpResult.IsFailure)
        {
            return Result.Failure(otpResult.Error);
        }

        return Result.Success();
    }

    public async Task<Result> VerifyOtpAsync(
        string destination,
        string otpToken,
        OtpType? otpType,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(destination))
        {
            return Result.Failure("Destination must be provided.");
        }

        if (string.IsNullOrWhiteSpace(otpToken))
        {
            return Result.Failure("OTP token must be provided.");
        }

        try
        {
            destinationType = OtpDestinationResolver.Resolve(destination);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }

        string normalizedDestination = destinationType switch
        {
            OtpDestinationType.Email => Normalizer.EmailAddressLowerCase(destination),
            OtpDestinationType.Phone => Normalizer.PhoneNumber(destination),
            _ => destination
        };

        // Find the latest matching OTP for this destination and type
        Otp? otp = await context.Otp
            .Where(o =>
                o.Destination == normalizedDestination &&
                o.OtpType == (otpType ?? OtpType.Default) &&
                !o.IsUsed &&
                !o.IsExpired)
            .OrderByDescending(o => o.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
        {
            return Result.Failure("No valid OTP found. It may have expired or already been used.");
        }

        // Check token match
        if (otp.OtpToken != otpToken)
        {
            return Result.Failure("OTP token is invalid.");
        }

        // Check expiration
        if (otp.ExpiresAt <= dateTimeProvider.UtcNow)
        {
            otp.IsExpired = true;
            await context.SaveChangesAsync(cancellationToken);
            return Result.Failure("OTP token has expired.");
        }

        // Mark OTP as used
        otp.IsUsed = true;
        otp.IsExpired = true;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
