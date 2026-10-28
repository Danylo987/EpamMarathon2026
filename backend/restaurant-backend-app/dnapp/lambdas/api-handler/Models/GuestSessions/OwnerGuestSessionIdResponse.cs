using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

public record OwnerGuestSessionIdResponse(
    [property: JsonPropertyName("guestSessionId")] string GuestSessionId);
