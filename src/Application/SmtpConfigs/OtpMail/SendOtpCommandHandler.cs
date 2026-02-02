using Application.Abstractions.Messaging;
using Application.Abstractions.Otps;
using Domain.Otps;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.SmtpConfigs.OtpMail;

internal sealed class SendOtpCommandHandler(
    IOtpProviderService otpProviderService,
    ILogger<SendOtpCommandHandler> logger)
    : ICommandHandler<SendOtpCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        SendOtpCommand command,
        CancellationToken cancellationToken
    )
    {
        Result<Guid> otpResult = await otpProviderService.SendOtpAsync(
            command.RecipientEmail,
            OtpType.Default,
            
            cancellationToken
        );

        if (otpResult.IsFailure)
        {
            logger.LogWarning("Email failed: {Error}", otpResult.Error);

            return Result.Failure<Guid>(otpResult.Error);
        }

        return Result.Success(otpResult.Value);
    }
}
