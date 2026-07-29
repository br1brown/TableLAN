namespace TableLAN.Server;

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using TableLAN.Core.Characters;
using TableLAN.Core.Dice;
using TableLAN.Core.Engine;
using TableLAN.Core.Profile;
using TableLAN.Server.Data;
using TableLAN.Server.Hubs;
using TableLAN.Server.Import;
using TableLAN.Server.Middleware;
using TableLAN.Server.Services;

/// <summary>
/// Costruzione dell'app server, separata da Program.cs perché quest'ultimo
/// decide solo come presentarla: finestra nativa Photino (il caso normale)
/// oppure terminale con QR ASCII (--headless). In entrambi i casi il server
/// gira in-process, senza processi o finestre separate.
/// </summary>
public static class ServerBootstrap
{
    public const int DefaultPort = 5000;

    public static int ResolvePort() =>
        int.TryParse(Environment.GetEnvironmentVariable("TABLELAN_PORT"), out var p) ? p : DefaultPort;

    /// <summary>
    /// Cartella dell'eseguibile vero, anche quando è un bundle single-file.
    /// Non si usa AppContext.BaseDirectory: in un single-file col contenuto
    /// auto-estratto quello punta alla cartella temporanea di estrazione, non
    /// a dove sta l'exe — e la base dati finirebbe in un posto invisibile e
    /// cancellabile. Environment.ProcessPath resta il percorso reale.
    /// </summary>
    private static string AppDirectory() =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>
    /// La campagna (file .db) da aprire all'avvio.
    ///
    /// Con <c>TABLELAN_DB</c> il Master punta a un file preciso, e quello vince
    /// sempre. Altrimenti si riapre l'<em>ultima campagna che era attiva</em> —
    /// ricordata da un puntatore accanto ai .db (vedi <see cref="CampaignService"/>)
    /// — così chiudere e riaprire il programma non riporta ogni volta a
    /// <c>tablelan.db</c>. Solo se il puntatore manca o non è più valido (primo
    /// avvio, campagna archiviata) si ricade sul default.
    /// </summary>
    public static string ResolveDbPath()
    {
        var custom = Environment.GetEnvironmentVariable("TABLELAN_DB");
        if (!string.IsNullOrWhiteSpace(custom))
            return custom;

        var folder = AppDirectory();
        return CampaignService.RememberedCampaign(folder) ?? Path.Combine(folder, "tablelan.db");
    }

    public static WebApplication Build(string[] args, int? port = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        var listenPort = port ?? ResolvePort();

        // Il server ascolta su tutte le interfacce: i giocatori arrivano
        // dalla LAN. Le rotte admin restano comunque solo-loopback grazie
        // al middleware dedicato.
        builder.WebHost.UseUrls($"http://0.0.0.0:{listenPort}");

        // Era in appsettings.json, ora sta qui: un file di configurazione
        // accanto all'exe pubblicato contraddirebbe "tutto dentro un file",
        // e queste due righe non sono mai state qualcosa da editare a mano.
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

        // La campagna attiva è un file .db, cambiabile a caldo: il percorso non
        // è più inchiodato nella factory all'avvio, ma vive nel CampaignService
        // che la factory rilegge a ogni contesto (vedi CampaignDbContextFactory).
        builder.Services.AddSingleton(new CampaignService(ResolveDbPath()));
        builder.Services.AddSingleton<IDbContextFactory<AppDb>, CampaignDbContextFactory>();
        builder.Services.AddSingleton<GameRepository>();
        builder.Services.AddSingleton<RollLogService>();
        builder.Services.AddSingleton<GameStateService>();
        builder.Services.AddSingleton<LanDiscoveryService>();
        builder.Services.AddSingleton<QrCodeService>();
        // Confronta la versione in esecuzione con l'ultima release: singleton
        // perché tiene in cache l'esito e non deve richiederlo a ogni pagina.
        builder.Services.AddSingleton(new UpdateService(BuildInfo.Version));
        builder.Services.AddSignalR();

        var app = builder.Build();

        var files = ResolveWebFiles(app.Environment);

        app.UseMiddleware<LoopbackOnlyMiddleware>();
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = SetCachePolicy,
        });

        app.MapHub<TableHub>("/hub");
        MapApi(app, listenPort);
        MapSpaFallback(app, files);

        return app;
    }

    /// <summary>
    /// Fallback per il routing lato client con URL veri (History API): una
    /// richiesta a <c>/master</c> o <c>/pg-&lt;guid&gt;</c> non è un file, e i
    /// file statici la lasciano passare. Qui riceve l'<c>index.html</c>
    /// dell'app, che poi il router Angular interpreta. Le API e l'hub sono
    /// esclusi: lì un 404 resta un 404, non diventa la pagina dell'app.
    /// </summary>
    private static void MapSpaFallback(WebApplication app, IFileProvider files)
    {
        app.MapFallback(async ctx =>
        {
            var path = ctx.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/hub", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var index = files.GetFileInfo("index.html");
            if (!index.Exists)
            {
                // Client non ancora compilato: coerente col resto, pagina vuota
                // invece di un errore.
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            ctx.Response.ContentType = "text/html";
            ctx.Response.Headers.CacheControl = "no-cache";
            await using var stream = index.CreateReadStream();
            await stream.CopyToAsync(ctx.Response.Body);
        });
    }

    /// <summary>
    /// I file statici stanno incorporati nell'assembly, così l'exe pubblicato
    /// è autosufficiente. In sviluppo, se accanto c'è una wwwroot vera, quella
    /// vince: si modifica la console del Master e basta ricaricare la finestra,
    /// senza ricompilare.
    /// </summary>
    private sealed record FeatureToggleDraft(bool Active);
    private sealed record InitiativeEntryDraft(string RefId, string? Kind, string? Name, int Initiative);
    private sealed record InitiativeDraft(List<InitiativeEntryDraft>? Order, string? ActiveId);
    private sealed record CampaignRequest(string? Name);

    /// <summary>
    /// I bundle di Angular portano l'hash nel nome (main-46OHT5RP.js): ogni
    /// build è un file nuovo, quindi si possono tenere in cache per sempre. I
    /// file a nome fisso (index.html su tutti) invece cambiano contenuto sotto
    /// lo stesso nome: senza <c>Cache-Control</c> il browser applica la cache
    /// euristica e può servire una pagina vecchia senza nemmeno chiedere. Qui
    /// si obbliga la rivalidazione: <c>no-cache</c> non vuol dire "non
    /// conservare", vuol dire "chiedi sempre se è ancora buono". Con l'ETag la
    /// risposta è un 304 vuoto, e siamo su loopback: costa niente.
    /// </summary>
    private static void SetCachePolicy(StaticFileResponseContext ctx)
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;

        // I bundle di Angular portano l'hash nel nome: sono immutabili per
        // definizione, e vale la pena lasciarli in cache davvero.
        bool fingerprinted = path.StartsWith("/chunk-", StringComparison.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(path, @"-[A-Z0-9]{8}\.(js|css)$");

        ctx.Context.Response.Headers.CacheControl = fingerprinted
            ? "public,max-age=31536000,immutable"
            : "no-cache";
    }

    private static IFileProvider ResolveWebFiles(IWebHostEnvironment env)
    {
        // I file statici sono normalmente incorporati nell'assembly. Ma la
        // wwwroot è interamente generata dal build del client Angular (master e
        // giocatori, una sola app): se quel build non è ancora girato — npm
        // assente, oppure `dotnet build -p:SkipClientBuild=true` su un clone
        // pulito — l'assembly non ha alcun manifest, e costruire il provider
        // incorporato lancerebbe. In quel caso il server deve partire lo stesso:
        // il LAN gira, le pagine restano vuote finché non si compila il client.
        IFileProvider embedded;
        try
        {
            embedded = new ManifestEmbeddedFileProvider(typeof(ServerBootstrap).Assembly, "wwwroot");
        }
        catch (InvalidOperationException)
        {
            embedded = new NullFileProvider();
        }

        var onDisk = Path.Combine(env.ContentRootPath, "wwwroot");
        return Directory.Exists(onDisk)
            ? new CompositeFileProvider(new PhysicalFileProvider(onDisk), embedded)
            : embedded;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static void MapApi(WebApplication app, int port)
    {
        // Ridiffonde lo stato aggiornato a tutti i client dopo una mutazione.
        static async Task Broadcast(GameStateService state, IHubContext<TableHub> hub) =>
            await hub.Clients.All.SendAsync(TableHub.StateChanged, state.Snapshot());

        // Rende attiva una campagna (file .db): sposta il percorso corrente,
        // porta il file a schema+pragma (creandolo se nuovo, con i due
        // personaggi di prova), ricarica tutto lo stato in memoria e ridiffonde.
        // I giocatori che avevano aperto una scheda ora inesistente ricadono da
        // soli sulla scelta del personaggio: lo snapshot nuovo non la contiene.
        static async Task ActivateCampaign(string path, CampaignService camp,
            IDbContextFactory<AppDb> dbFactory, GameStateService state, IHubContext<TableHub> hub)
        {
            camp.SetCurrent(path);
            await using (var db = await dbFactory.CreateDbContextAsync())
                await SeedData.EnsureSeededAsync(db);
            await state.InitializeAsync();
            await Broadcast(state, hub);
        }

        // ---- Lettura (LAN) ----

        app.MapGet("/api/state", (GameStateService state) => Results.Json(state.Snapshot()));

        // Testo esplicativo lazy-loaded: viaggia solo quando l'utente
        // espande l'elemento, mai nel payload iniziale (Capitolo 7).
        app.MapGet("/api/descriptions/{id}", async (string id, GameRepository repo) =>
            await repo.LoadDescriptionAsync(id) is string text
                ? Results.Json(new { id, text })
                : Results.NotFound());

        // ---- Azioni del giocatore sulla propria scheda (LAN) ----
        // Il Master, dalla dashboard su loopback, usa le stesse rotte per
        // qualunque personaggio: in un tavolo di tre persone che si fidano
        // (Capitolo 4) la separazione è di rete, non di permessi applicativi.

        app.MapPost("/api/characters/{id}/intent",
            async (string id, IntentRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            var intent = new Intent(id, req.FeatureId) { SlotLevelOverride = req.SlotLevelOverride };
            var result = await state.SubmitIntentAsync(intent);
            if (!result.IsValid)
                // L'intento respinto non muta nulla: l'esito torna solo a chi
                // ha chiamato, il tavolo non lo vede (Capitolo 8).
                return Results.Json(new { ok = false, reason = result.Reason });
            await Broadcast(state, hub);
            // `notice` non è un errore: l'intento è riuscito, ma qualcosa è
            // caduto per strada — la Benedizione su cui stavi concentrando.
            return Results.Json(new { ok = true, notice = result.Notice });
        });

        // Smettere di concentrarsi: in 5e si può in qualunque momento, non
        // costa niente e non richiede il turno. Per questo è una rotta sua e
        // non un intento — non c'è nulla da validare.
        app.MapDelete("/api/characters/{id}/occupied/{slotId}",
            async (string id, string slotId, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id)) return Results.NotFound();
            await state.ReleaseExclusiveAsync(id, slotId);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        // Tiro di dado. Il canale realtime resta di sola ricezione: come ogni
        // altra mutazione, si entra via REST e si esce via broadcast — l'hub non
        // guadagna nessun metodo invocabile dal client.
        app.MapPost("/api/characters/{id}/roll",
            async (string id, RollRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            var outcome = await state.RollAsync(id, req);
            if (!outcome.Verdict.IsValid)
                return Results.Json(new { ok = false, reason = outcome.Verdict.Reason });

            // Lo stato PRIMA dell'evento: così il tiro atterra su numeri già
            // aggiornati e non si vede il costo scalare dopo il risultato.
            await Broadcast(state, hub);
            await hub.Clients.All.SendAsync(TableHub.RollMade, outcome.Entry);
            return Results.Json(new { ok = true, roll = outcome.Entry, notice = outcome.Verdict.Notice });
        });

        // Tiro libero del giocatore: una formula qualsiasi, per i tiri
        // contestuali che nessuna feature copre. Sotto il suo nome, con le sue
        // statistiche (@Forza funziona anche qui). Non spende, non tocca lo
        // stato — solo il log del tavolo — quindi niente Broadcast.
        app.MapPost("/api/characters/{id}/roll-free",
            async (string id, FreeRollRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            var outcome = state.RollFreeForCharacter(id, req);
            if (!outcome.Verdict.IsValid)
                return Results.Json(new { ok = false, reason = outcome.Verdict.Reason });

            await hub.Clients.All.SendAsync(TableHub.RollMade, outcome.Entry);
            return Results.Json(new { ok = true, roll = outcome.Entry });
        });

        // Modifica di una feature: una rotta sola per tutto ciò che è suo.
        // Prima erano tre parziali (costo, tiro, e basta) e il resto — nome,
        // usi, ricarica, testo — non si poteva cambiare affatto: si cancellava
        // e si rifaceva. "Cambiare l'Attacco Furtivo" non è tre operazioni.
        //
        // Ricarica TUTTI i personaggi: la riga della feature è catalogo
        // condiviso, e una modifica può toccare più schede.
        app.MapPut("/api/characters/{id}/features/{featureId}",
            async (string id, string featureId, FeatureDraft draft, GameStateService state,
                   GameRepository repo, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id))
                return Results.NotFound();
            if (string.IsNullOrWhiteSpace(draft.ShortName))
                return Results.BadRequest(new { error = "Il nome è obbligatorio." });

            // La formula si valida qui: una rotta salvata è una feature che non
            // tirerà mai, e lo scopriresti al tavolo.
            if (!string.IsNullOrWhiteSpace(draft.Roll)
                && !DiceFormula.TryParse(draft.Roll, out _, out var error))
                return Results.BadRequest(new { error });

            await repo.UpdateFeatureAsync(featureId, draft);
            await state.ReloadAllCharactersAsync();
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        app.MapPut("/api/characters/{id}/name",
            async (string id, NewCharacterRequest req, GameStateService state, GameRepository repo, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id))
                return Results.NotFound();
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Il nome è obbligatorio." });

            await repo.RenameCharacterAsync(id, req.Name.Trim());
            await state.ReloadCharacterAsync(id);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        app.MapDelete("/api/characters/{id}/stats/{name}",
            async (string id, string name, GameStateService state, GameRepository repo, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id))
                return Results.NotFound();
            await repo.RemoveStatAsync(id, name);
            await state.ReloadCharacterAsync(id);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        app.MapPost("/api/characters/{id}/reset-turn",
            async (string id, GameStateService state, IHubContext<TableHub> hub) =>
        {
            await state.ResetTurnAsync(id);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        app.MapPost("/api/characters/{id}/rest",
            async (string id, RestRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            await state.RestAsync(id, req.Cycle);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        // I PF di una scheda: authoring, non gioco. La POST qui sotto è il
        // danno e la cura al tavolo; questa è "salgo di livello" o "avevo
        // digitato male" — e serviva, perché i PF massimi si fissavano alla
        // creazione e non si potevano più cambiare.
        app.MapPut("/api/characters/{id}/hp",
            async (string id, SetHpRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id)) return Results.NotFound();
            await state.SetHpAsync(id, req.MaxHp, req.CurrentHp);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        app.MapPost("/api/characters/{id}/hp",
            async (string id, HpDeltaRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            await state.AdjustHpAsync(id, req.Delta);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        // PF temporanei: il cuscinetto che il danno consuma per primo. Lo imposta
        // il giocatore sulla propria scheda (un chierico glieli ha dati) — rotta
        // di gioco come il danno/cura, non authoring.
        app.MapPost("/api/characters/{id}/temp-hp",
            async (string id, TempHpRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            await state.SetTempHpAsync(id, req.Value);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        // Equipaggia/rimuovi: accende o spegne gli effetti di una feature indossabile.
        app.MapPost("/api/characters/{id}/features/{featureId}/toggle",
            async (string id, string featureId, ToggleRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id)) return Results.NotFound();
            await state.ToggleEffectAsync(id, featureId, req.Active);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        // Inventario ordinabile (Capitolo 9): il client invia l'intera lista
        // ordinata; le voci senza id sono nuove e ne ricevono uno qui.
        app.MapPut("/api/characters/{id}/inventory",
            async (string id, InventoryPayload payload, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id)) return Results.NotFound();
            var items = payload.Items.Select(dto => new InventoryItem
            {
                Id = string.IsNullOrWhiteSpace(dto.Id) ? "inv-" + Guid.NewGuid().ToString("N") : dto.Id!,
                Name = dto.Name,
                Quantity = dto.Quantity,
                Notes = dto.Notes,
            });
            await state.SetInventoryAsync(id, items);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        // ---- Authoring: il Master (o il giocatore) aggiunge alla scheda
        // mutazioni, oggetti, magie, statistiche (Capitolo 10). ----

        app.MapPost("/api/characters/{id}/features",
            async (string id, FeatureDraft draft, GameRepository repo, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id)) return Results.NotFound();
            var featureId = await repo.AddFeatureAsync(id, draft);
            await state.ReloadCharacterAsync(id);
            await Broadcast(state, hub);
            return Results.Json(new { id = featureId });
        });

        app.MapDelete("/api/characters/{id}/features/{featureId}",
            async (string id, string featureId, GameRepository repo, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id)) return Results.NotFound();
            await repo.RemoveFeatureAsync(id, featureId);
            await state.ReloadCharacterAsync(id);
            await Broadcast(state, hub);
            return Results.Ok();
        });


        app.MapPost("/api/characters/{id}/sources",
            async (string id, AddSourceRequest req, GameRepository repo, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id)) return Results.NotFound();
            var sourceId = await repo.AddSourceAsync(id, req.Name, req.Type, req.ParentSourceId);
            await state.ReloadCharacterAsync(id);
            await Broadcast(state, hub);
            return Results.Json(new { id = sourceId });
        });

        app.MapPut("/api/characters/{id}/stats",
            async (string id, SetStatRequest req, GameRepository repo, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id)) return Results.NotFound();
            await repo.SetCustomStatAsync(id, req.Name, req.Value);
            await state.ReloadCharacterAsync(id);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        app.MapPut("/api/characters/{id}/pools",
            async (string id, SetPoolRequest req, GameRepository repo, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id)) return Results.NotFound();

            var pool = state.Profile.Pools.FirstOrDefault(p => p.Id == req.PoolId);
            if (pool is null)
                return Results.BadRequest(new { error = $"Il profilo non ha una riserva '{req.PoolId}'." });

            // Un pool a punti ha un solo livello (0): accettare un "Ki di
            // livello 3" scriverebbe una riga che nessuno legge più.
            var level = pool.Kind == PoolKind.Points ? 0 : req.Level;
            if (pool.Kind == PoolKind.Leveled && level < 1)
                return Results.BadRequest(new { error = "Gli slot partono dal livello 1." });

            await repo.SetPoolTierAsync(id, req.PoolId, level, req.Max, req.Remaining);
            await state.ReloadCharacterAsync(id);
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        // ---- Rotte del Master (solo loopback, vedi LoopbackOnlyMiddleware) ----

        app.MapGet("/api/admin/join-info", (LanDiscoveryService lan) =>
            Results.Json(new { url = lan.GetPlayerUrl(port) }));

        app.MapGet("/api/admin/version", () => Results.Json(new { version = BuildInfo.Version }));

        // C'è una versione più nuova? Best-effort e in cache: se offline torna
        // "nessun aggiornamento", non un errore. Solo loopback come il resto
        // dell'admin — è il Master a doverlo sapere, non i giocatori.
        app.MapGet("/api/admin/update", async (UpdateService updates) =>
            Results.Json(await updates.CheckAsync()));

        app.MapGet("/api/admin/qr.png", (LanDiscoveryService lan, QrCodeService qr) =>
            Results.File(qr.GeneratePng(lan.GetPlayerUrl(port)), "image/png"));

        // ---- Giocatori: creare e rimuovere schede (solo Master) ----
        // Sta su /api/admin e non su /api/characters perché non è un'azione
        // della propria scheda: è decidere chi siede al tavolo.

        app.MapPost("/api/admin/characters",
            async (NewCharacterRequest req, GameStateService state, GameRepository repo, IHubContext<TableHub> hub) =>
        {
            var name = req.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "Il nome è obbligatorio." });

            var id = await repo.AddCharacterAsync(name, Math.Max(1, req.MaxHp), state.ProfileStats());
            await state.ReloadAllCharactersAsync();
            await Broadcast(state, hub);
            return Results.Json(new { id });
        });

        // Importa una scheda esportata in PDF da D&D Beyond.
        //
        // È un upload, e sarebbe una parola grossa: il file non esce di qui —
        // il PDF sta sul disco del Master, il server gira sul suo PC, e la
        // rotta è dietro loopback come tutto ciò che è suo. "Upload" qui vuol
        // dire solo passare dei byte a un programma, senza toccare la rete.
        //
        // Il PDF non viene salvato da nessuna parte: si legge in memoria, se ne
        // ricava una scheda, e finisce lì. Tenerlo sarebbe conservare il
        // documento di qualcun altro senza motivo.
        app.MapPost("/api/admin/import/ddb",
            async (HttpRequest request, GameRepository repo, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Serve un file PDF (multipart/form-data)." });

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Nessun file ricevuto." });

            try
            {
                await using var stream = file.OpenReadStream();
                using var memoria = new MemoryStream();
                await stream.CopyToAsync(memoria);
                memoria.Position = 0;

                var campi = DdbSheet.Leggi(memoria);
                var esito = await new DdbImporter(repo).ImportaAsync(campi, state.Profile);

                await state.ReloadAllCharactersAsync();
                await Broadcast(state, hub);
                return Results.Json(esito);
            }
            catch (InvalidDataException e)
            {
                // Il file non è quello che il Master crede: glielo si dice, non
                // si logga e basta.
                return Results.BadRequest(new { error = e.Message });
            }
            catch (Exception e)
            {
                return Results.BadRequest(new { error = $"PDF non leggibile: {e.Message}" });
            }
        });

        app.MapDelete("/api/admin/characters/{id}",
            async (string id, GameStateService state, GameRepository repo, IHubContext<TableHub> hub) =>
        {
            if (!state.Exists(id))
                return Results.NotFound();

            await repo.RemoveCharacterAsync(id);
            await state.ReloadAllCharactersAsync();
            await Broadcast(state, hub);
            return Results.Json(state.Snapshot());
        });

        // ---- Log dei tiri per la console ----
        // La console non ha un client SignalR (è statica, senza build step) e
        // fa polling ogni 3s: per un evento come un tiro sarebbe già vecchio.
        // SSE è nativo del browser, monodirezionale, zero dipendenze — esatta-
        // mente la forma che serve.

        app.MapGet("/api/admin/rolls", (RollLogService rolls) => Results.Json(rolls.Recent()));

        app.MapGet("/api/admin/rolls/stream", async (HttpContext ctx, RollLogService rolls) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";

            // Chi apre la console a metà serata vede gli ultimi tiri, non il vuoto.
            var lastId = 0L;
            while (!ctx.RequestAborted.IsCancellationRequested)
            {
                foreach (var entry in rolls.Recent().Where(e => e.Id > lastId))
                {
                    lastId = entry.Id;
                    await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(entry, JsonOpts)}\n\n", ctx.RequestAborted);
                }
                await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                try
                {
                    await Task.Delay(400, ctx.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    break; // il Master ha chiuso la pagina
                }
            }
        });

        // Bestiario del Master (Capitolo 12), solo loopback: i giocatori non lo
        // vedono. Le schede sono quelle vere — stesso DTO dei PG, con capacità,
        // costi, effetti e tiri: un orso gufo è un personaggio, e il motore non
        // ha ragione di trattarlo diversamente. Prima qui uscivano le righe
        // grezze di SQLite, ed è il motivo per cui la scheda di un mostro non si
        // è mai potuta aprire.
        app.MapGet("/api/admin/monsters", (GameStateService state) =>
            Results.Json(state.MonsterDtos()));

        app.MapGet("/api/admin/monsters/{id}", (string id, GameStateService state) =>
            state.MonsterDto(id) is { } dto ? Results.Json(dto) : Results.NotFound());

        app.MapPost("/api/admin/monsters", async (MonsterDraft draft, GameRepository repo, GameStateService state) =>
        {
            var id = await repo.AddMonsterAsync(draft.Name, draft.MaxHp, draft.ArmorClass, draft.Notes);
            await state.ReloadMonstersAsync();
            return Results.Json(new { id });
        });

        app.MapPost("/api/admin/monsters/{id}/hp", async (string id, MonsterHpDelta req, GameRepository repo, GameStateService state) =>
        {
            await repo.AdjustMonsterHpAsync(id, req.Delta);
            await state.ReloadMonstersAsync();
            return Results.Json(state.MonsterDtos());
        });

        app.MapPost("/api/admin/monsters/{id}/duplicate", async (string id, GameRepository repo, GameStateService state) =>
        {
            var newId = await repo.DuplicateMonsterAsync(id);
            if (newId is null) return Results.NotFound();
            await state.ReloadMonstersAsync();
            return Results.Json(state.MonsterDtos());
        });

        app.MapDelete("/api/admin/monsters/{id}", async (string id, GameRepository repo, GameStateService state) =>
        {
            await repo.DeleteMonsterAsync(id);
            await state.ReloadMonstersAsync();
            return Results.Json(state.MonsterDtos());
        });

        // Authoring di un mostro.
        //
        // Le rotte dei PG non lo raggiungono di proposito — Require() guarda
        // solo fra i giocatori, ed è ciò che impedisce a un telefono di curare
        // l'orso. Il bestiario ha quindi le sue, dietro loopback, che si
        // appoggiano agli stessi metodi del repository: il lavoro è identico,
        // cambia solo chi ha il diritto di chiederlo.
        app.MapPost("/api/admin/monsters/{id}/features",
            async (string id, FeatureDraft draft, GameRepository repo, GameStateService state) =>
        {
            if (!state.MonsterExists(id)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(draft.ShortName))
                return Results.BadRequest(new { error = "Il nome è obbligatorio." });
            if (!string.IsNullOrWhiteSpace(draft.Roll)
                && !DiceFormula.TryParse(draft.Roll, out _, out var err))
                return Results.BadRequest(new { error = err });

            await repo.AddFeatureAsync(id, draft);
            await state.ReloadMonstersAsync();
            return Results.Json(state.MonsterDto(id));
        });

        app.MapPut("/api/admin/monsters/{id}/features/{featureId}",
            async (string id, string featureId, FeatureDraft draft, GameRepository repo, GameStateService state) =>
        {
            if (!state.MonsterExists(id)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(draft.ShortName))
                return Results.BadRequest(new { error = "Il nome è obbligatorio." });
            if (!string.IsNullOrWhiteSpace(draft.Roll)
                && !DiceFormula.TryParse(draft.Roll, out _, out var err))
                return Results.BadRequest(new { error = err });

            await repo.UpdateFeatureAsync(featureId, draft);
            await state.ReloadMonstersAsync();
            await state.ReloadAllCharactersAsync();   // la riga è catalogo condiviso
            return Results.Json(state.MonsterDto(id));
        });

        app.MapDelete("/api/admin/monsters/{id}/features/{featureId}",
            async (string id, string featureId, GameRepository repo, GameStateService state) =>
        {
            if (!state.MonsterExists(id)) return Results.NotFound();
            await repo.RemoveFeatureAsync(id, featureId);
            await state.ReloadMonstersAsync();
            return Results.Json(state.MonsterDto(id));
        });

        app.MapPut("/api/admin/monsters/{id}/hp",
            async (string id, SetHpRequest req, GameStateService state) =>
        {
            if (!state.MonsterExists(id)) return Results.NotFound();
            await state.SetMonsterHpAsync(id, req.MaxHp, req.CurrentHp);
            return Results.Json(state.MonsterDto(id));
        });

        app.MapPut("/api/admin/monsters/{id}/stats",
            async (string id, SetStatRequest req, GameRepository repo, GameStateService state) =>
        {
            if (!state.MonsterExists(id)) return Results.NotFound();
            await repo.SetCustomStatAsync(id, req.Name, req.Value);
            await state.ReloadMonstersAsync();
            return Results.Json(state.MonsterDto(id));
        });

        // Il tiro di un mostro: è il Master che lo fa, ed è come quello di un
        // PG — stessa formula, stesse statistiche, stesso log del tavolo.
        app.MapPost("/api/admin/monsters/{id}/roll",
            async (string id, RollRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            var outcome = await state.RollMonsterAsync(id, req);
            if (!outcome.Verdict.IsValid)
                return Results.Json(new { ok = false, reason = outcome.Verdict.Reason });

            await hub.Clients.All.SendAsync(TableHub.RollMade, outcome.Entry);
            return Results.Json(new { ok = true, roll = outcome.Entry });
        });

        // Tiro libero del Master: la stessa cosa, ma sotto il nome "Master" e
        // senza scheda. Sta fra le rotte /api/admin/ perché è del Master e basta
        // — il boundary loopback lo tiene sulla sua macchina — ma il tiro poi lo
        // vedono tutti al tavolo, come ogni altro.
        app.MapPost("/api/admin/roll",
            async (FreeRollRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            var outcome = state.RollFreeAsMaster(req);
            if (!outcome.Verdict.IsValid)
                return Results.Json(new { ok = false, reason = outcome.Verdict.Reason });

            await hub.Clients.All.SendAsync(TableHub.RollMade, outcome.Entry);
            return Results.Json(new { ok = true, roll = outcome.Entry });
        });

        // La lista arriva intera e il server la riordina: il client non decide
        // chi va prima. Il nome viaggia con la voce perché una bestia cancellata
        // dal bestiario a metà scontro deve restare leggibile nel giro — il
        // Master la sta ancora giocando, e vedere un id nudo sarebbe peggio.
        app.MapPost("/api/admin/initiative", async (InitiativeDraft draft, GameStateService state, IHubContext<TableHub> hub) =>
        {
            var entries = (draft.Order ?? [])
                .Where(e => !string.IsNullOrWhiteSpace(e.RefId))
                .Select(e => new GameStateService.InitiativeEntry
                {
                    RefId = e.RefId,
                    Kind = e.Kind == "monster" ? "monster" : "pc",
                    Name = string.IsNullOrWhiteSpace(e.Name) ? e.RefId : e.Name!.Trim(),
                    Initiative = e.Initiative,
                })
                .ToList();

            state.UpdateInitiative(entries, draft.ActiveId);
            await Broadcast(state, hub);
            return Results.Ok();
        });

        app.MapPost("/api/admin/initiative/next", async (GameStateService state, IHubContext<TableHub> hub) =>
        {
            state.NextTurn();
            await Broadcast(state, hub);
            return Results.Ok();
        });

        // Stati (avvelenato, prono, concentrazione…) su una creatura qualunque,
        // PG o mostro. È tracciamento del combattimento: sta col Master, dietro
        // loopback come l'iniziativa e il bestiario.
        app.MapPost("/api/admin/conditions/{id}",
            async (string id, ConditionRequest req, GameStateService state, IHubContext<TableHub> hub) =>
        {
            state.AddCondition(id, req.Label, req.Rounds);
            await Broadcast(state, hub);
            return Results.Ok();
        });

        app.MapDelete("/api/admin/conditions/{id}/{conditionId}",
            async (string id, string conditionId, GameStateService state, IHubContext<TableHub> hub) =>
        {
            state.RemoveCondition(id, conditionId);
            await Broadcast(state, hub);
            return Results.Ok();
        });

        // Profilo di sistema: il Master ne edita il JSON per adattare le
        // meccaniche (economia del turno, riserve, cicli) ad altri giochi.
        app.MapGet("/api/admin/profile", (GameStateService state) =>
            Results.Json(state.Profile));

        // Preset pronti per il menù "Parti da…" della console.
        app.MapGet("/api/admin/profile/presets", () =>
            Results.Json(GameProfiles.Presets()));

        app.MapPut("/api/admin/profile",
            async (GameProfile profile, GameStateService state, IHubContext<TableHub> hub) =>
        {
            if (profile.TurnResources.Count == 0 && profile.Pools.Count == 0)
                return Results.BadRequest(new { error = "Il profilo deve avere almeno una risorsa di turno o un pool." });
            await state.SetProfileAsync(profile);
            await Broadcast(state, hub);
            return Results.Json(state.Profile);
        });

        // ---- Campagne (Capitolo: "La campagna è un file"), solo loopback ----
        // Una campagna è un file .db accanto all'eseguibile. Averne più d'una e
        // cambiarle in corsa è il modo comodo di gestirle, senza rinominare file
        // e riavviare — il Master lo fa dalla console.

        app.MapGet("/api/admin/campaigns", (CampaignService camp) =>
            Results.Json(new { current = camp.CurrentName, campaigns = camp.List() }));

        app.MapPost("/api/admin/campaigns/switch",
            async (CampaignRequest req, CampaignService camp, IDbContextFactory<AppDb> dbFactory,
                   GameStateService state, IHubContext<TableHub> hub) =>
        {
            var path = camp.PathFor(req.Name);
            if (path is null)
                return Results.BadRequest(new { error = "Nome campagna non valido." });
            if (!File.Exists(path))
                return Results.NotFound(new { error = "Questa campagna non esiste." });
            if (string.Equals(path, camp.CurrentPath, StringComparison.OrdinalIgnoreCase))
                return Results.Json(new { current = camp.CurrentName });

            await ActivateCampaign(path, camp, dbFactory, state, hub);
            return Results.Json(new { current = camp.CurrentName });
        });

        app.MapPost("/api/admin/campaigns/new",
            async (CampaignRequest req, CampaignService camp, IDbContextFactory<AppDb> dbFactory,
                   GameStateService state, IHubContext<TableHub> hub) =>
        {
            var path = camp.PathFor(req.Name);
            if (path is null)
                return Results.BadRequest(new { error = "Serve un nome per la campagna." });
            if (File.Exists(path))
                return Results.BadRequest(new { error = "C'è già una campagna con questo nome." });

            // Il file nasce qui: EnsureSeededAsync crea schema e pragma e mette i
            // due personaggi di prova, come al primo avvio.
            await ActivateCampaign(path, camp, dbFactory, state, hub);
            return Results.Json(new { current = camp.CurrentName });
        });
    }

    public static async Task InitializeAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDb>>();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            await SeedData.EnsureSeededAsync(db);
        }
        await app.Services.GetRequiredService<GameStateService>().InitializeAsync();
    }
}
