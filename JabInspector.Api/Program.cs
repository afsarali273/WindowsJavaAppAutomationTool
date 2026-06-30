using JabInspector.Api.Services;
using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;
using JabInspector.Core.Services;
using JabInspector.Native;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 4 * 1024 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<InspectorLogger>();
builder.Services.AddSingleton<AccessBridgeService>();
builder.Services.AddSingleton<JavaDriverService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Java Access Bridge Inspector API",
        Version = "v1",
        Description = "Headless REST API for inspecting and automating Java Swing/AWT applications through Java Access Bridge."
    });
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<InspectorLogger>();
    var started = DateTime.UtcNow;
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        logger.Log($"API unhandled error. Method={context.Request.Method}, Path={context.Request.Path}, Error={ex.Message}");
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "The API server handled an internal error without terminating. Check the server log for details."
            });
        }
    }
    finally
    {
        var elapsed = DateTime.UtcNow - started;
        logger.Debug($"API request completed. Method={context.Request.Method}, Path={context.Request.Path}, Status={context.Response.StatusCode}, DurationMs={elapsed.TotalMilliseconds:0}.");
    }
});

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Java Access Bridge Inspector API v1");
    options.DocumentTitle = "JAB Inspector API";
});

app.MapGet("/", () => Results.Ok(new
{
    name = "Java Access Bridge Inspector API",
    engine = "java-access-bridge",
    status = "running",
    swagger = "/swagger",
    docs = new
    {
        health = "GET /api/health",
        settings = "GET /api/settings",
        windows = "GET /api/java/windows",
        openApplication = "POST /api/java/applications/open",
        attach = "POST /api/java/sessions",
        sessionWindows = "GET /api/java/sessions/{sessionId}/windows",
        switchWindow = "POST /api/java/sessions/{sessionId}/window",
        validate = "POST /api/java/sessions/{sessionId}/elements/validate",
        findElements = "POST /api/java/sessions/{sessionId}/elements/find",
        findChildElements = "POST /api/java/sessions/{sessionId}/elements/children",
        actions = "POST /api/java/sessions/{sessionId}/actions",
        oneShotAction = "POST /api/java/actions/run",
        oneShotValidate = "POST /api/java/validate/run",
        oneShotFindElements = "POST /api/java/elements/find",
        oneShotFindChildElements = "POST /api/java/elements/children"
    }
}));

app.MapGet("/api/health", (JavaDriverService driver) =>
{
    var initialized = driver.Initialize();
    return Results.Ok(new { ok = initialized, engine = "java-access-bridge" });
});

app.MapGet("/api/settings", () => Results.Ok(NativeEnvironment.GetSettingsMetadata()));
app.MapGet("/api/java/settings", () => Results.Ok(NativeEnvironment.GetSettingsMetadata()));

app.MapGet("/api/java/windows", (JavaDriverService driver) =>
{
    var windows = driver.GetWindows();
    return Results.Ok(windows.Select(JavaWindowDto.From));
});

app.MapPost("/api/java/applications/open", (LaunchApplicationRequest request, JavaDriverService driver) =>
{
    var result = driver.OpenApplication(request);
    return result.Success
        ? Results.Ok(result)
        : Results.BadRequest(result);
});

app.MapPost("/api/java/sessions", (CreateSessionRequest request, JavaDriverService driver) =>
{
    var result = driver.CreateSession(request);
    return result.Success
        ? Results.Ok(result)
        : Results.BadRequest(result);
});

app.MapGet("/api/java/sessions", (JavaDriverService driver) =>
    Results.Ok(driver.GetSessions()));

app.MapDelete("/api/java/sessions/{sessionId}", (string sessionId, JavaDriverService driver) =>
    driver.DeleteSession(sessionId)
        ? Results.Ok(new { ok = true, sessionId })
        : Results.NotFound(new { ok = false, message = $"Session '{sessionId}' was not found." }));

app.MapPost("/api/java/sessions/{sessionId}/refresh", (string sessionId, JavaDriverService driver) =>
{
    var result = driver.RefreshSession(sessionId);
    return result.Success ? Results.Ok(result) : Results.NotFound(result);
});

app.MapGet("/api/java/sessions/{sessionId}/windows", (string sessionId, JavaDriverService driver) =>
{
    var result = driver.GetSessionWindows(sessionId);
    return result.Success ? Results.Ok(result) : Results.NotFound(result);
});

app.MapPost("/api/java/sessions/{sessionId}/window", (string sessionId, SwitchWindowRequest request, JavaDriverService driver) =>
{
    var result = driver.SwitchSessionWindow(sessionId, request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapGet("/api/java/sessions/{sessionId}/tree", (string sessionId, JavaDriverService driver) =>
{
    var result = driver.GetTree(sessionId);
    return result.Success ? Results.Ok(result) : Results.NotFound(result);
});

app.MapPost("/api/java/sessions/{sessionId}/repository/load", (string sessionId, LoadRepositoryRequest request, JavaDriverService driver) =>
{
    var result = driver.LoadRepository(sessionId, request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapGet("/api/java/sessions/{sessionId}/repository", (string sessionId, JavaDriverService driver) =>
{
    var result = driver.GetRepository(sessionId);
    return result.Success ? Results.Ok(result) : Results.NotFound(result);
});

app.MapPost("/api/java/sessions/{sessionId}/elements/resolve", (string sessionId, ResolveElementRequest request, JavaDriverService driver) =>
{
    var result = driver.ResolveElement(sessionId, request);
    return result.Success ? Results.Ok(result) : Results.NotFound(result);
});

app.MapPost("/api/java/sessions/{sessionId}/elements/validate", (string sessionId, JavaValidationRequest request, JavaDriverService driver) =>
{
    var result = driver.ValidateElement(sessionId, request);
    return result.Success ? Results.Ok(result) : Results.NotFound(result);
});

app.MapPost("/api/java/sessions/{sessionId}/elements/find", (string sessionId, JavaFindElementsRequest request, JavaDriverService driver) =>
{
    var result = driver.FindElements(sessionId, request);
    return result.Success ? Results.Ok(result) : Results.NotFound(result);
});

app.MapPost("/api/java/sessions/{sessionId}/elements/children", (string sessionId, JavaFindChildElementsRequest request, JavaDriverService driver) =>
{
    var result = driver.FindChildElements(sessionId, request);
    return result.Success ? Results.Ok(result) : Results.NotFound(result);
});

app.MapPost("/api/java/sessions/{sessionId}/actions", (string sessionId, JavaActionRequest request, JavaDriverService driver) =>
{
    var result = driver.ExecuteAction(sessionId, request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/java/actions/run", (JavaOneShotActionRequest request, JavaDriverService driver) =>
{
    var result = driver.ExecuteOneShotAction(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/java/validate/run", (JavaValidationRequest request, JavaDriverService driver) =>
{
    var result = driver.ValidateOneShot(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/java/elements/find", (JavaFindElementsRequest request, JavaDriverService driver) =>
{
    var result = driver.FindElementsOneShot(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/java/elements/children", (JavaFindChildElementsRequest request, JavaDriverService driver) =>
{
    var result = driver.FindChildElementsOneShot(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.Run();
