using System.Text.Json;
using TrainingBackend.Exceptions;

namespace TrainingBackend.Middleware;

/// <summary>
/// Service 層が投げた例外を HTTP ステータスへ変換する
/// これにより Controller は例外処理を書かずに済み、薄く保てる
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessRuleException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未処理の例外が発生しました。");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "サーバー内部でエラーが発生しました。");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new { status = statusCode, error = message });
        await context.Response.WriteAsync(payload);
    }
}
