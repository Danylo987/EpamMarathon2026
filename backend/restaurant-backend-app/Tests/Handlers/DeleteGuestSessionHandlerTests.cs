using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Handlers;

public class DeleteGuestSessionHandlerTests
{
    private readonly Mock<IGuestSessionService> _guestSessionService = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public DeleteGuestSessionHandlerTests()
    {
        _context.SetupGet(context => context.Logger).Returns(_logger.Object);
    }

    [Fact]
    public async Task HandleRequest_OwnerGetsUpdatedSnapshotWithCamelCaseFields()
    {
        const string userId = "owner-id";
        const string groupReservationId = "group-id";
        const string guestSessionId = "guest-session-id";
        _guestSessionService
            .Setup(service => service.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId))
            .ReturnsAsync(new GetAllSessionsResponse(
                [new Participant(guestSessionId, ParticipantType.Guest, "Olena", [], 220m, false)],
                220m));
        var handler = new DeleteGuestSessionHandler(_guestSessionService.Object);

        var response = await handler.HandleRequest(
            Request(groupReservationId, guestSessionId, userId),
            _context.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("\"participants\":[{\"id\":\"guest-session-id\",\"type\":\"Guest\"", response.Body);
        Assert.Contains("\"total\":220", response.Body);
    }

    [Fact]
    public async Task HandleRequest_MissingIdentityReturnsUnauthorized()
    {
        var handler = new DeleteGuestSessionHandler(_guestSessionService.Object);

        var response = await handler.HandleRequest(
            Request("group-id", "guest-session-id", null),
            _context.Object);

        Assert.Equal(401, response.StatusCode);
        _guestSessionService.Verify(
            service => service.DeleteGuestSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleRequest_NonOwnerReturnsForbidden()
    {
        _guestSessionService
            .Setup(service => service.DeleteGuestSessionAsync("other-user", "group-id", "session-id"))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));
        var handler = new DeleteGuestSessionHandler(_guestSessionService.Object);

        var response = await handler.HandleRequest(
            Request("group-id", "session-id", "other-user"),
            _context.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_MissingGuestSessionReturnsNotFound()
    {
        _guestSessionService
            .Setup(service => service.DeleteGuestSessionAsync("owner-id", "group-id", "missing-session"))
            .ThrowsAsync(new GuestSessionNotFoundException("Guest session was not found."));
        var handler = new DeleteGuestSessionHandler(_guestSessionService.Object);

        var response = await handler.HandleRequest(
            Request("group-id", "missing-session", "owner-id"),
            _context.Object);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_SessionFromAnotherGroupReturnsBadRequest()
    {
        _guestSessionService
            .Setup(service => service.DeleteGuestSessionAsync("owner-id", "group-id", "session-id"))
            .ThrowsAsync(new ValidationException("Guest session does not belong to this group reservation."));
        var handler = new DeleteGuestSessionHandler(_guestSessionService.Object);

        var response = await handler.HandleRequest(
            Request("group-id", "session-id", "owner-id"),
            _context.Object);

        Assert.Equal(400, response.StatusCode);
    }

    private static APIGatewayProxyRequest Request(string groupReservationId, string guestSessionId, string? userId)
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                ["groupReservationId"] = groupReservationId,
                ["guestSessionId"] = guestSessionId
            }
        };

        if (userId is not null)
        {
            request.RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>
                    {
                        ["sub"] = userId
                    }
                }
            };
        }

        return request;
    }
}
