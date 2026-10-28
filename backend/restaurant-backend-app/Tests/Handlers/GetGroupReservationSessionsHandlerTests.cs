using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Handlers;

public class GetGroupReservationSessionsHandlerTests
{
    private readonly Mock<IGuestSessionService> _guestSessionService = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();
    private readonly GetGroupReservationSessionsHandler _handler;

    public GetGroupReservationSessionsHandlerTests()
    {
        _context.SetupGet(c => c.Logger).Returns(_logger.Object);
        _handler = new GetGroupReservationSessionsHandler(_guestSessionService.Object);
    }

    [Fact]
    public async Task HandleRequest_MissingIdentity_ReturnsUnauthorized()
    {
        var request = RequestWithGroupId();

        var response = await _handler.HandleRequest(request, _context.Object);

        Assert.Equal(401, response.StatusCode);
        _guestSessionService.Verify(
            s => s.GetGroupReservationSessionsAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleRequest_ServiceRejectsNonOwner_ReturnsForbidden()
    {
        const string userId = "user-id";
        const string groupReservationId = "group-id";
        var request = RequestWithGroupIdAndIdentity(groupReservationId, userId);
        _guestSessionService
            .Setup(s => s.GetGroupReservationSessionsAsync(userId, groupReservationId))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await _handler.HandleRequest(request, _context.Object);

        Assert.Equal(403, response.StatusCode);
        _guestSessionService.Verify(
            s => s.GetGroupReservationSessionsAsync(userId, groupReservationId),
            Times.Once);
    }

    [Fact]
    public async Task HandleRequest_OwnerIdentity_ReturnsSessions()
    {
        const string userId = "owner-id";
        const string groupReservationId = "group-id";
        var sessions = new GetAllSessionsResponse([], 0m);
        _guestSessionService
            .Setup(s => s.GetGroupReservationSessionsAsync(userId, groupReservationId))
            .ReturnsAsync(sessions);

        var response = await _handler.HandleRequest(
            RequestWithGroupIdAndIdentity(groupReservationId, userId),
            _context.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("\"participants\":[]", response.Body);
        Assert.Contains("\"total\":0", response.Body);
        _guestSessionService.Verify(
            s => s.GetGroupReservationSessionsAsync(userId, groupReservationId),
            Times.Once);
    }

    private static APIGatewayProxyRequest RequestWithGroupId()
        => new()
        {
            PathParameters = new Dictionary<string, string>
            {
                ["groupReservationId"] = "group-id"
            }
        };

    private static APIGatewayProxyRequest RequestWithGroupIdAndIdentity(string groupReservationId, string userId)
        => new()
        {
            PathParameters = new Dictionary<string, string>
            {
                ["groupReservationId"] = groupReservationId
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>
                    {
                        ["sub"] = userId
                    }
                }
            }
        };
}
