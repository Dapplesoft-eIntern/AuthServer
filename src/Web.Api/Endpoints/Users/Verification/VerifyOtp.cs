using Application.Abstractions.Messaging;
using Application.Users.Verification.VerifyOtp;
using Domain.Otps;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users.Verification;

internal sealed class VerifyOtp : IEndpoint
{
    internal sealed class Request
    {
        public string Destination { get; set; } = null!;
        public string OtpCode { get; set; } = null!;
        public OtpType OtpType { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Create(Base.VerifyAccountOtp), async (
            Request request,
            ICommandHandler<VerifyOtpCommand> handler,
            CancellationToken ct
        ) =>
        {
            var command = new VerifyOtpCommand(
                request.Destination,
                request.OtpCode,
                request.OtpType
            );

            Result result = await handler.Handle(command, ct);

            return result.Match(
                // ✅ send JSON so Angular can read it
                () => Results.Ok(new { isValid = true }),
                CustomResults.Problem
            );
        })
        .WithTags(Tags.Users);
    }
}
