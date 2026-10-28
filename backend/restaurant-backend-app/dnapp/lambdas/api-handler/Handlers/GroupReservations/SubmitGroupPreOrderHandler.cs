using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class SubmitGroupPreOrderHandler(IGroupReservationService groupReservationService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out string? groupReservationId))
            return ResponseBuilder.CreateErrorResponse(400, "Missing 'groupReservationId' in path parameters.", context.Logger);

        if (request.RequestContext?.Authorizer?.Claims is null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out string? userId) ||
            string.IsNullOrWhiteSpace(userId))
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);

        if (string.IsNullOrWhiteSpace(request.Body))
            return ResponseBuilder.CreateErrorResponse(400, "Request body is required.", context.Logger);

        SubmitGroupPreOrderRequest? submitRequest;
        try
        {
            submitRequest = JsonSerializer.Deserialize<SubmitGroupPreOrderRequest>(request.Body);
        }
        catch (JsonException ex)
        {
            return ResponseBuilder.CreateErrorResponse(400, "Request body is invalid.", context.Logger, ex);
        }

        if (string.IsNullOrWhiteSpace(submitRequest?.ReservationId))
            return ResponseBuilder.CreateErrorResponse(400, "The 'reservationId' field is required.", context.Logger);

        try
        {
            var dishes = await groupReservationService.SubmitGroupPreOrderAsync(
                userId,
                groupReservationId,
                submitRequest.ReservationId);
            return ResponseBuilder.CreateJsonResponse(200, dishes);
        }
        catch (GroupReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (ReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (ActionForbiddenException ex)
        {
            return ResponseBuilder.CreateErrorResponse(403, ex.Message, context.Logger);
        }
        catch (ValidationException ex)
        {
            return ResponseBuilder.CreateErrorResponse(400, ex.Message, context.Logger);
        }
    }
}
