using Photino.NET;
using TableLAN.Server;
using TableLAN.Server.Services;

/// <summary>
/// Eseguibile unico del Master (Capitolo 5): avvia il server LAN in-process e
/// apre la finestra nativa sulla console del Master (la stessa app Angular dei
/// giocatori, sulla rotta /master). Con --headless resta un server da
/// terminale, senza finestra. Le API /api/admin restano solo loopback.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main sincrono e [STAThread], non top-level statements con await.
    ///
    /// Photino (WebView2, su Windows) pretende che la finestra nasca e giri sul
    /// thread principale, in apartment STA. Un'app console non ha
    /// SynchronizationContext: dopo il primo await che va davvero in asincrono
    /// il codice riprende su un thread del pool, e la finestra finirebbe lì —
    /// si apre, col titolo giusto, ma il browser dentro non inizializza mai e
    /// resta nera. Da qui i GetAwaiter().GetResult(): tengono il thread.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        var headless = args.Contains("--headless");
        // --headless è un flag nostro, non configurazione dell'host: va tolto
        // prima che CreateBuilder provi a interpretarlo.
        var hostArgs = args.Where(a => a != "--headless").ToArray();

        var port = ServerBootstrap.ResolvePort();
        var app = ServerBootstrap.Build(hostArgs, port);
        ServerBootstrap.InitializeAsync(app).GetAwaiter().GetResult();

        var lan = app.Services.GetRequiredService<LanDiscoveryService>();
        var playerUrl = lan.GetPlayerUrl(port);

        if (headless)
        {
            // QR di join rigenerato a ogni avvio con l'IP LAN corrente (Capitolo 6).
            var qr = app.Services.GetRequiredService<QrCodeService>();
            Console.WriteLine();
            Console.WriteLine($"  TableLAN {BuildInfo.Version} avviato. I giocatori si uniscono su: {playerUrl}");
            Console.WriteLine($"  Console Master (solo questo PC): http://127.0.0.1:{port}/master");
            Console.WriteLine();
            Console.WriteLine(qr.GenerateAscii(playerUrl));
            app.Run();
            return;
        }

        // StartAsync (non RunAsync) perché ritorna quando Kestrel è già in
        // ascolto: la finestra naviga sul server solo a porta aperta.
        app.StartAsync().GetAwaiter().GetResult();

        var window = new PhotinoWindow()
            .SetTitle("TableLAN — Master")
            .SetUseOsDefaultSize(false)
            .SetSize(1280, 860)
            .Center();

        // Prima di Load: l'icona va decisa mentre la finestra si costruisce.
        // Se l'estrazione fallisce si va avanti senza — vedi WindowIcon.
        if (WindowIcon.EstraiPercorso() is string icona)
            window.SetIconFile(icona);

        window.Load($"http://127.0.0.1:{port}/master");

        window.WaitForClose();

        app.StopAsync().GetAwaiter().GetResult();
    }
}
