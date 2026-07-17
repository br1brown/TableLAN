namespace TableLAN.Server.Middleware;

using System.Net;

/// <summary>
/// Confine di sicurezza tra Master e giocatori (Capitolo 5/11): le rotte
/// di amministrazione rispondono solo su loopback (127.0.0.1). Un telefono
/// in LAN che prova a raggiungere /admin o /api/admin riceve 403, non per
/// permessi applicativi ma a livello di rete.
/// </summary>
public sealed class LoopbackOnlyMiddleware(RequestDelegate next)
{
    private static readonly string[] ProtectedPrefixes = ["/admin", "/api/admin"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var isProtected = ProtectedPrefixes.Any(p =>
            path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

        if (isProtected && !IsLoopback(context.Connection.RemoteIpAddress))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Rotta riservata al Master (solo loopback).");
            return;
        }

        await next(context);
    }

    private static bool IsLoopback(IPAddress? address) =>
        address is not null
        && (IPAddress.IsLoopback(address)
            || (address.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(address.MapToIPv4())));
}
