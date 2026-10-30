using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using AutoFixture.Xunit2;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _orderRepositoryMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(0);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_ThrowsGuestLimitExceeded(GroupReservation groupReservation, Reservation reservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(reservation.GuestCount);

        await Assert.ThrowsAsync<GuestLimitExceededException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_GroupReservationMissing_ThrowsNotFound(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(
            () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_ConsolidatesOnlySubmittedSessionsAndUpdatesOrderAndGroup()
    {
        const string userId = "owner-id";
        const string reservationId = "reservation-id";
        const string groupReservationId = "group-id";
        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token",
            Status = GroupReservationStatus.Open
        };
        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            TableId = "table-id",
            WaiterId = "waiter-id",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            GuestCount = 3,
            Status = ReservationStatus.Reserved
        };
        var sessions = new List<GuestSession>
        {
            new()
            {
                Id = "owner-session",
                GroupReservationId = groupReservationId,
                GuestName = "Owner",
                IsOwnerSession = true,
                OwnerUserId = userId,
                Dishes = [],
                IsSubmitted = false
            },
            new()
            {
                Id = "submitted-1",
                GroupReservationId = groupReservationId,
                GuestName = "Guest 1",
                Dishes = [new OrderItem { DishId = "dish-1", Quantity = 1 }],
                IsSubmitted = true
            },
            new()
            {
                Id = "submitted-2",
                GroupReservationId = groupReservationId,
                GuestName = "Guest 2",
                Dishes =
                [
                    new OrderItem { DishId = "dish-1", Quantity = 2 },
                    new OrderItem { DishId = "dish-2", Quantity = 2 }
                ],
                IsSubmitted = true
            },
            new()
            {
                Id = "not-submitted",
                GroupReservationId = groupReservationId,
                GuestName = "Guest 3",
                Dishes = [new OrderItem { DishId = "dish-3", Quantity = 9 }],
                IsSubmitted = false
            }
        };
        var order = new Order
        {
            OrderId = "order-id",
            CustomerId = userId,
            ReservationId = reservationId,
            ReservationAddress = "Address",
            ReservationTime = "Time",
            Dishes = [new OrderItem { DishId = "owner-dish", Quantity = 1 }]
        };
        Order? savedOrder = null;
        GroupReservation? savedGroup = null;

        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync(sessions);
        _orderRepositoryMock
            .Setup(repository => repository.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(order);
        _orderRepositoryMock
            .Setup(repository => repository.SaveOrderAsync(It.IsAny<Order>()))
            .Callback<Order>(saved => savedOrder = saved);
        _groupReservationRepositoryMock
            .Setup(repository => repository.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(saved => savedGroup = saved);

        var result = await _service.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId);

        Assert.Collection(
            result,
            first =>
            {
                Assert.Equal("dish-1", first.DishId);
                Assert.Equal(3, first.Quantity);
            },
            second =>
            {
                Assert.Equal("dish-2", second.DishId);
                Assert.Equal(2, second.Quantity);
            },
            third =>
            {
                Assert.Equal("owner-dish", third.DishId);
                Assert.Equal(1, third.Quantity);
            });
        Assert.NotNull(savedOrder);
        Assert.Equal(OrderStatus.SubmittedByCustomer, savedOrder.Status);
        Assert.Equal(
            new[] { ("dish-1", 3), ("dish-2", 2), ("owner-dish", 1) },
            savedOrder.Dishes.Select(dish => (dish.DishId, dish.Quantity)));
        Assert.All(savedOrder.Dishes, dish => Assert.Equal(APIHandler.Constants.Orders.OrderItemState.Pending, dish.State));
        Assert.NotNull(savedGroup);
        Assert.Equal(GroupReservationStatus.Submitted, savedGroup.Status);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_NoSubmittedSessions_ThrowsValidationException()
    {
        const string userId = "owner-id";
        const string reservationId = "reservation-id";
        const string groupReservationId = "group-id";
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(new GroupReservation
            {
                GroupReservationId = groupReservationId,
                ReservationId = reservationId,
                ReservationToken = "token"
            });
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(new Reservation
            {
                Id = reservationId,
                CustomerId = userId,
                TableId = "table-id",
                WaiterId = "waiter-id"
            });
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId));

        _orderRepositoryMock.Verify(repository => repository.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_SubmittedSessionsWithoutDishes_ThrowsValidationException()
    {
        const string userId = "owner-id";
        const string reservationId = "reservation-id";
        const string groupReservationId = "group-id";
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(new GroupReservation
            {
                GroupReservationId = groupReservationId,
                ReservationId = reservationId,
                ReservationToken = "token"
            });
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(new Reservation
            {
                Id = reservationId,
                CustomerId = userId,
                TableId = "table-id",
                WaiterId = "waiter-id"
            });
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync(
            [
                new GuestSession
                {
                    Id = "submitted-session",
                    GroupReservationId = groupReservationId,
                    GuestName = "Guest",
                    Dishes = [],
                    IsSubmitted = true
                }
            ]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId));

        _orderRepositoryMock.Verify(repository => repository.GetOrderByReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_OrderNotFound_ThrowsValidationException()
    {
        const string userId = "owner-id";
        const string reservationId = "reservation-id";
        const string groupReservationId = "group-id";
        _groupReservationRepositoryMock
            .Setup(repository => repository.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(new GroupReservation
            {
                GroupReservationId = groupReservationId,
                ReservationId = reservationId,
                ReservationToken = "token"
            });
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(new Reservation
            {
                Id = reservationId,
                CustomerId = userId,
                TableId = "table-id",
                WaiterId = "waiter-id"
            });
        _guestSessionRepositoryMock
            .Setup(repository => repository.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync(
            [
                new GuestSession
                {
                    Id = "submitted-session",
                    GroupReservationId = groupReservationId,
                    GuestName = "Guest",
                    Dishes = [new OrderItem { DishId = "dish-1", Quantity = 1 }],
                    IsSubmitted = true
                }
            ]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId));

        _groupReservationRepositoryMock.Verify(
            repository => repository.SaveGroupReservationAsync(It.IsAny<GroupReservation>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateGroupReservationAsync_CreatesOwnerSessionForCustomerReservation()
    {
        const string reservationId = "reservation-id";
        const string userId = "owner-id";
        _reservationRepositoryMock
            .Setup(repository => repository.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(new Reservation
            {
                Id = reservationId,
                CustomerId = userId,
                TableId = "table-id",
                WaiterId = "waiter-id"
            });
        GuestSession? ownerSession = null;
        _guestSessionRepositoryMock
            .Setup(repository => repository.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(session => ownerSession = session);

        GroupReservation groupReservation = await _service.CreateGroupReservationAsync(reservationId);

        Assert.NotNull(ownerSession);
        Assert.Equal(groupReservation.GroupReservationId, ownerSession.GroupReservationId);
        Assert.Equal(userId, ownerSession.OwnerUserId);
        Assert.True(ownerSession.IsOwnerSession);
    }
}
