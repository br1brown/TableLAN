namespace TableLAN.Server.Middleware;

using System.Net;

/// <summary>
/// Confine di sicurezza tra Master e giocatori (Capitolo 5/11): le API di
/// amministrazione (<c>/api/admin/*</c>) rispondono solo su loopback
/// (127.0.0.1). Un telefono in LAN che prova a raggiungerle riceve 403, non
/// per permessi applicativi ma a livello di rete.
///
/// L'<em>interfaccia</em> del Master (la console Angular su <c>/master</c>)
/// è invece servita in modo generico, come il resto dell'app: un telefono può
/// caricarla, ma senza le API resta una console vuota. Il muro è di rete, non
/// di pagina — coerente con "confine di rete, non di UI".
/// </summary>
public sealed class LoopbackOnlyMiddleware(RequestDelegate next)
{
    private static readonly string[] ProtectedPrefixes = ["/api/admin"];

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
