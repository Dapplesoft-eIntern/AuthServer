using Application.Abstractions.Messaging;
using Application.Users.Verification.RequstOtpVerify;
using Domain.Otps;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users.Verification;

internal sealed class RequestOtpVerify : IEndpoint
{

    internal sealed class Request
    {
        public string Destination { get; set; }
        public OtpType OtpType { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Create(Base.ReqOtpVerify), async (
            Request request,
            ICommandHandler<RequestOtpVerifyCommand, RequestOtpVerifyResponse> handler,
            CancellationToken ct
        ) =>
        {
            var command = new RequestOtpVerifyCommand(
                request.Destination,
                request.OtpType
            );
            Result<RequestOtpVerifyResponse> result = await handler.Handle(command, ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users);
    }
}
