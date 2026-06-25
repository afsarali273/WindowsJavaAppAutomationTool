using JabInspector.Api.Services;
using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;
using JabInspector.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<InspectorLogger>();
builder.Services.AddSingleton<AccessBridgeService>();
builder.Services.AddSingleton<JavaDriverService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "Java Access Bridge Inspector API",
    engine = "java-access-bridge",
    status = "running",
    docs = new
    {
        health = "GET /api/health",
        windows = "GET /api/java/windows",
        attach = "POST /api/java/sessions",
        actions = "POST /api/java/sessions/{sessionId}/actions"
    }
}));

app.MapGet("/api/health", (JavaDriverService driver) =>
{
    var initialized = driver.Initialize();
    return Results.Ok(new { ok = initialized, engine = "java-access-bridge" });
});

app.MapGet("/api/java/windows", (JavaDriverService driver) =>
{
    var windows = driver.GetWindows();
    return Results.Ok(windows.Select(JavaWindowDto.From));
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

app.MapPost("/api/java/sessions/{sessionId}/actions", (string sessionId, JavaActionRequest request, JavaDriverService driver) =>
{
    var result = driver.ExecuteAction(sessionId, request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.Run();

public sealed record JavaWindowDto(
    string Hwnd,
    string Title,
    string ClassName,
    int ProcessId,
    int VmId)
{
    public static JavaWindowDto From(JavaWindowInfo window) => new(
        window.HwndDisplay,
        window.Title,
        window.ClassName,
        window.ProcessId,
        window.VmId);
}
