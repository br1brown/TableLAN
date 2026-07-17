namespace TableLAN.Server;

using System.Reflection;

/// <summary>
/// Quale build è questa: il tag da cui è stata pubblicata più il commit
/// esatto — "v0.3+a1b2c3d". Su un build locale vale "dev".
///
/// Serve a due cose, e la seconda non esiste ancora. La prima: quando il
/// Master dice "non funziona", si guarda la testata della console e si sa
/// di cosa sta parlando. La seconda: sapere la propria versione è il
/// prerequisito per confrontarla con l'ultima release e accorgersi che ce
/// n'è una nuova.
///
/// Il valore lo incide il workflow nell'assembly (-p:InformationalVersion),
/// così l'unica fonte è il pacchetto stesso: non c'è un file a fianco che
/// possa raccontare una versione diversa da quella che sta girando.
/// </summary>
public static class BuildInfo
{
    public static string Version { get; } =
        typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "dev";
}
