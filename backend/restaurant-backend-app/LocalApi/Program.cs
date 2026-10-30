using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler;
using APIHandler.Helpers;
using ApiFunction = APIHandler.Function;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var function = new Lazy<ApiFunction>(() => new ApiFunction());
var logger = app.Logger;

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/", async () =>
{
    var indexPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
    var index = await File.ReadAllTextAsync(indexPath);
    return Results.Content(index, "text/html");
});
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/swagger", () => Results.Redirect("/"));
app.MapGet("/swagger-endpoints.yaml", async (HttpContext context) =>
{
    var specPath = Path.Combine(AppContext.BaseDirectory, "swagger-endpoints.yaml");
    var spec = await File.ReadAllTextAsync(specPath);
    var localServer = $"{context.Request.Scheme}://{context.Request.Host}";
    spec = new Regex(@"(?m)^  - url: .+$").Replace(spec, $"  - url: {localServer}", 1);
    return Results.Text(spec, "application/yaml");
});

app.MapMethods("/{**path}", ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"], async (HttpContext context) =>
{
    Dictionary<string, string>? claims;
    try
    {
        claims = await GetCognitoClaimsAsync(context.Request);
    }
    catch (InvalidCognitoTokenException)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid Cognito ID token." });
        return;
    }

    var request = new APIGatewayProxyRequest
    {
        HttpMethod = context.Request.Method,
        Path = context.Request.Path.Value ?? "/",
        Headers = context.Request.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToString(),
            StringComparer.OrdinalIgnoreCase),
        QueryStringParameters = context.Request.Query.ToDictionary(
            parameter => parameter.Key,
            parameter => parameter.Value.ToString(),
            StringComparer.OrdinalIgnoreCase),
        Body = await ReadBodyAsync(context.Request),
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = claims is null
                ? null
                : new APIGatewayCustomAuthorizerContext { Claims = claims }
        }
    };

    var response = await function.Value.FunctionHandler(request, new LocalLambdaContext(logger));
    context.Response.StatusCode = response.StatusCode;

    if (response.Headers is not null)
    {
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }
    }

    if (!string.IsNullOrEmpty(response.Body))
    {
        await context.Response.WriteAsync(response.Body);
    }
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    if (string.Equals(Environment.GetEnvironmentVariable("OPEN_SWAGGER"), "true", StringComparison.OrdinalIgnoreCase))
    {
        Process.Start(new ProcessStartInfo("http://127.0.0.1:5187/swagger") { UseShellExecute = true });
    }
});

await app.RunAsync();

static async Task<string?> ReadBodyAsync(HttpRequest request)
{
    if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || request.ContentLength == 0)
    {
        return null;
    }

    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    return await reader.ReadToEndAsync();
}

static async Task<Dictionary<string, string>?> GetCognitoClaimsAsync(HttpRequest request)
{
    var authorization = request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var token = authorization["Bearer ".Length..].Trim();
    if (token.Count(character => character == '.') != 2)
    {
        return null;
    }

    if (await CognitoIdTokenValidator.ValidateAsync(token) is null)
    {
        throw new InvalidCognitoTokenException();
    }

    var payload = token.Split('.')[1];
    var paddedPayload = payload.Replace('-', '+').Replace('_', '/');
    paddedPayload = paddedPayload.PadRight((paddedPayload.Length + 3) / 4 * 4, '=');

    using var document = JsonDocument.Parse(Convert.FromBase64String(paddedPayload));
    return document.RootElement.EnumerateObject().ToDictionary(
        property => property.Name,
        property => property.Value.ValueKind == JsonValueKind.String
            ? property.Value.GetString() ?? string.Empty
            : property.Value.GetRawText(),
        StringComparer.Ordinal);
}

sealed class InvalidCognitoTokenException : Exception
{
}

sealed class LocalLambdaContext(ILogger logger) : ILambdaContext
{
    public string AwsRequestId { get; } = Guid.NewGuid().ToString();
    public Amazon.Lambda.Core.IClientContext? ClientContext => null;
    public string FunctionName => "api-handler-local";
    public string FunctionVersion => "local";
    public Amazon.Lambda.Core.ICognitoIdentity? Identity => null;
    public string InvokedFunctionArn => "local";
    public ILambdaLogger Logger { get; } = new LocalLambdaLogger(logger);
    public string LogGroupName => "local";
    public string LogStreamName => "local";
    public int MemoryLimitInMB => 0;
    public TimeSpan RemainingTime => TimeSpan.FromMinutes(2);
}

sealed class LocalLambdaLogger(ILogger logger) : ILambdaLogger
{
    public void Log(string message) => logger.LogInformation("{LambdaMessage}", message);
    public void LogLine(string message) => logger.LogInformation("{LambdaMessage}", message);
    public void Log(string level, string message) => logger.Log(ParseLogLevel(level), "{LambdaMessage}", message);
    public void Log(Amazon.Lambda.Core.LogLevel level, string message) => logger.Log(ParseLogLevel(level.ToString()), "{LambdaMessage}", message);
    public void LogTrace(string message) => logger.LogTrace("{LambdaMessage}", message);
    public void LogDebug(string message) => logger.LogDebug("{LambdaMessage}", message);
    public void LogInformation(string message) => logger.LogInformation("{LambdaMessage}", message);
    public void LogWarning(string message) => logger.LogWarning("{LambdaMessage}", message);
    public void LogError(string message) => logger.LogError("{LambdaMessage}", message);
    public void LogCritical(string message) => logger.LogCritical("{LambdaMessage}", message);

    private static Microsoft.Extensions.Logging.LogLevel ParseLogLevel(string level)
        => Enum.TryParse(level, true, out Microsoft.Extensions.Logging.LogLevel parsed)
            ? parsed
            : Microsoft.Extensions.Logging.LogLevel.Information;
}
