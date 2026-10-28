using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservation;

public class SubmitGroupPreOrderRequest
{
    [JsonPropertyName("reservationId")]
    public required string ReservationId { get; set; }
}
