using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Handlers.Reservations;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.GroupReservations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class OwnerGroupPreOrderHandlerTests
{
    private readonly Mock<IGroupReservationService> _groupReservationService = new();
    private readonly Mock<IGuestSessionService> _guestSessionService = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public OwnerGroupPreOrderHandlerTests()
    {
        _context.SetupGet(context => context.Logger).Returns(_logger.Object);
    }

    [Fact]
    public async Task SubmitGroupPreOrderHandler_OwnerReceivesConsolidatedDishes()
    {
        const string userId = "owner-id";
        const string groupReservationId = "group-id";
        const string reservationId = "reservation-id";
        var handler = new SubmitGroupPreOrderHandler(_groupReservationService.Object);
        _groupReservationService
            .Setup(service => service.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId))
            .ReturnsAsync([new GroupPreOrderItemResponse("dish-1", 3)]);

        var response = await handler.HandleRequest(
            Request(groupReservationId, userId, JsonSerializer.Serialize(new { reservationId })),
            _context.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("[{\"dishId\":\"dish-1\",\"quantity\":3}]", response.Body);
    }

    [Fact]
    public async Task SubmitGroupPreOrderHandler_MissingIdentityReturnsUnauthorized()
    {
        var handler = new SubmitGroupPreOrderHandler(_groupReservationService.Object);

        var response = await handler.HandleRequest(
            Request("group-id", null, "{\"reservationId\":\"reservation-id\"}"),
            _context.Object);

        Assert.Equal(401, response.StatusCode);
        _groupReservationService.Verify(
            service => service.SubmitGroupPreOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderHandler_MissingOrderReturnsBadRequest()
    {
        var handler = new SubmitGroupPreOrderHandler(_groupReservationService.Object);
        _groupReservationService
            .Setup(service => service.SubmitGroupPreOrderAsync("owner-id", "group-id", "reservation-id"))
            .ThrowsAsync(new ValidationException("Order for the reservation was not found."));

        var response = await handler.HandleRequest(
            Request("group-id", "owner-id", "{\"reservationId\":\"reservation-id\"}"),
            _context.Object);

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdHandler_ReturnsOwnerSessionId()
    {
        const string userId = "owner-id";
        const string reservationId = "reservation-id";
        var handler = new GetOwnerGuestSessionIdHandler(_guestSessionService.Object);
        _guestSessionService
            .Setup(service => service.GetOwnerGuestSessionIdAsync(userId, reservationId))
            .ReturnsAsync("owner-session-id");

        var response = await handler.HandleRequest(
            Request(reservationId, userId),
            _context.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{\"guestSessionId\":\"owner-session-id\"}", response.Body);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdHandler_MissingIdentityReturnsUnauthorized()
    {
        var handler = new GetOwnerGuestSessionIdHandler(_guestSessionService.Object);

        var response = await handler.HandleRequest(
            Request("reservation-id", null),
            _context.Object);

        Assert.Equal(401, response.StatusCode);
        _guestSessionService.Verify(
            service => service.GetOwnerGuestSessionIdAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdHandler_MissingReservationReturnsNotFound()
    {
        var handler = new GetOwnerGuestSessionIdHandler(_guestSessionService.Object);
        _guestSessionService
            .Setup(service => service.GetOwnerGuestSessionIdAsync("owner-id", "reservation-id"))
            .ThrowsAsync(new ReservationNotFoundException("Reservation was not found."));

        var response = await handler.HandleRequest(
            Request("reservation-id", "owner-id"),
            _context.Object);

        Assert.Equal(404, response.StatusCode);
    }

    private static APIGatewayProxyRequest Request(string pathId, string? userId, string? body = null)
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                ["groupReservationId"] = pathId,
                ["reservationId"] = pathId
            },
            Body = body
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
