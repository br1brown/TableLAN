namespace TableLAN.Server.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableLAN.Core.Engine;
using TableLAN.Server.Data;
using TableLAN.Server.Services;

/// <summary>
/// L'iniziativa era una lista di id da ordinare a mano: niente numero, niente
/// distinzione fra chi ha una scheda nel motore e chi il Master gioca per conto
/// suo. Questi test coprono le regole che la rendono un tracker invece di un
/// elenco — e stanno qui, non fra i test del Core, perché l'iniziativa è stato
/// di sessione del server, non dominio.
/// </summary>
public sealed class InitiativeTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tablelan-init-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;

    public InitiativeTests()
    {
        _services = new ServiceCollection()
            .AddDbContextFactory<AppDb>(o => o.UseSqlite($"Data Source={_dbPath}"))
            .BuildServiceProvider();
    }

    private IDbContextFactory<AppDb> Factory => _services.GetRequiredService<IDbContextFactory<AppDb>>();

    private GameStateService NewState() =>
        new(new GameRepository(Factory), new RollLogService());

    private static GameStateService.InitiativeEntry Voce(string refId, int iniziativa, string kind = "pc") =>
        new() { RefId = refId, Kind = kind, Name = refId, Initiative = iniziativa };

    private static string[] OrdineDi(GameStateService s) =>
        [.. s.InitiativeSnapshot().Order.Select(e => e.RefId)];

    private static string? AttivoDi(GameStateService s) => s.InitiativeSnapshot().ActiveId;

    [Fact]
    public void Chi_ha_tirato_piu_alto_va_prima_anche_se_inserito_per_ultimo()
    {
        var s = NewState();
        s.UpdateInitiative([Voce("kael", 8), Voce("maga", 21), Voce("goblin", 14, "monster")], null);

        // È la regola che mancava del tutto: prima l'ordine era quello di
        // inserimento, cioè nessun ordine.
        Assert.Equal(["maga", "goblin", "kael"], OrdineDi(s));
    }

    [Fact]
    public void A_parita_di_numero_vince_chi_e_stato_aggiunto_prima()
    {
        var s = NewState();
        s.UpdateInitiative([Voce("primo", 12), Voce("secondo", 12)], null);

        // Il pareggio lo scioglie il Master con l'ordine in cui li inserisce:
        // l'ordinamento è stabile e non se lo rimescola da solo a ogni poll.
        Assert.Equal(["primo", "secondo"], OrdineDi(s));
    }

    [Fact]
    public void Riaggiungere_qualcuno_ne_aggiorna_il_numero_invece_di_sdoppiarlo()
    {
        var s = NewState();
        s.UpdateInitiative([Voce("kael", 8), Voce("maga", 21), Voce("kael", 25)], null);

        Assert.Equal(["kael", "maga"], OrdineDi(s));
    }

    [Fact]
    public void Il_giro_procede_dal_piu_alto_al_piu_basso_e_poi_ricomincia()
    {
        var s = NewState();
        s.UpdateInitiative([Voce("kael", 8), Voce("maga", 21), Voce("goblin", 14, "monster")], null);

        s.NextTurn();
        Assert.Equal("maga", AttivoDi(s));
        s.NextTurn();
        Assert.Equal("goblin", AttivoDi(s));
        s.NextTurn();
        Assert.Equal("kael", AttivoDi(s));
        s.NextTurn();
        Assert.Equal("maga", AttivoDi(s));   // giro chiuso
    }

    [Fact]
    public void Togliere_chi_era_di_turno_non_lascia_un_puntatore_a_un_morto()
    {
        var s = NewState();
        s.UpdateInitiative([Voce("maga", 21), Voce("goblin", 14, "monster")], null);
        s.NextTurn();
        Assert.Equal("maga", AttivoDi(s));

        s.UpdateInitiative([Voce("goblin", 14, "monster")], "maga");

        Assert.Null(AttivoDi(s));
        Assert.Equal(["goblin"], OrdineDi(s));
    }

    [Fact]
    public void Un_attivo_ancora_in_lista_sopravvive_al_riordino()
    {
        var s = NewState();
        s.UpdateInitiative([Voce("maga", 21), Voce("goblin", 14, "monster")], null);
        s.NextTurn();

        // Arriva un ritardatario col numero più alto: l'ordine cambia, ma chi
        // stava giocando il suo turno lo sta ancora giocando.
        s.UpdateInitiative([Voce("maga", 21), Voce("goblin", 14, "monster"), Voce("drago", 30, "monster")], "maga");

        Assert.Equal("maga", AttivoDi(s));
        Assert.Equal(["drago", "maga", "goblin"], OrdineDi(s));
    }

    /// <summary>
    /// L'iniziativa dice di chi è il turno, non lo impone.
    ///
    /// Il Master che sposta il segnalino non deve poter azzerare l'Azione di
    /// chi non ha ancora agito: le due cose sono indipendenti, e il turno lo
    /// chiude il giocatore dalla sua scheda. Questo test esiste perché la
    /// tentazione di rilegarle "per comodità" è forte — ci sono già cascato.
    /// </summary>
    [Fact]
    public async Task Il_giro_d_iniziativa_non_tocca_le_risorse_di_nessuno()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var s = NewState();
        await s.InitializeAsync();

        // Kael spende l'Azione col Dardo di Fuoco.
        await s.RollAsync("pg-kael", new RollRequest("ft-dardo-fuoco"));
        Assert.Equal(0, s.TurnRemaining("pg-kael", "Action"));

        // Il Master gira l'iniziativa fino a lui: il segnalino si sposta…
        s.UpdateInitiative([Voce("pg-kael", 8), Voce("goblin", 14, "monster")], null);
        s.NextTurn();
        s.NextTurn();
        Assert.Equal("pg-kael", AttivoDi(s));

        // …ma l'Azione resta spesa. La ricarica è un gesto del giocatore, non
        // un effetto collaterale del segnalino del Master.
        Assert.Equal(0, s.TurnRemaining("pg-kael", "Action"));
    }

    /// <summary>
    /// Il bug trovato giocando: <c>1d20+@Forza</c> con Forza 18 tirava
    /// <c>1d20+18</c> invece di <c>1d20+4</c>. Questo test è il guardiano —
    /// vive qui perché la traduzione punteggio→modificatore la fa il servizio,
    /// non il motore dei dadi, che del profilo non sa niente.
    /// </summary>
    [Fact]
    public async Task Una_formula_somma_il_modificatore_non_il_punteggio()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var repo = new GameRepository(Factory);
        var s = NewState();
        await s.InitializeAsync();

        // Il test si dà i propri dati: il seed non promette una Forza, e un
        // test che si appoggia a un valore che non controlla misura il caso.
        await repo.SetCustomStatAsync("pg-kael", "Forza", 18);
        var fid = await repo.AddFeatureAsync("pg-kael", new FeatureDraft("Spada", Roll: "1d20+@Forza"));
        await s.ReloadAllCharactersAsync();

        var esito = await s.RollAsync("pg-kael", new RollRequest(fid));

        Assert.True(esito.Verdict.IsValid, esito.Verdict.Reason);
        var tentativo = esito.Entry!.Attempts[esito.Entry.KeptIndex];
        Assert.Equal(4, tentativo.Modifier);          // +4, non +18
        Assert.InRange(esito.Entry.Total, 5, 24);     // 1d20+4
    }

    /// <summary>
    /// "Una volta per turno" vuol dire per ogni turno, non per round.
    ///
    /// È la regola che questo progetto rimprovera a Roll20 e D&amp;D Beyond di
    /// sbagliare — e la sbagliava anche lui: l'Attacco Furtivo si ricaricava
    /// solo col pulsante "Termina Turno" del giocatore, cioè una volta per
    /// round. Crawford: «Sneak Attack can occur once per turn, so it can
    /// potentially occur more than once in a round» — il caso classico è
    /// l'attacco di opportunità nel turno di un altro.
    /// </summary>
    [Fact]
    public async Task Una_feature_una_volta_per_turno_si_ricarica_a_ogni_turno_di_chiunque()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var repo = new GameRepository(Factory);
        var s = NewState();
        await s.InitializeAsync();

        // L'Attacco Furtivo del seed è già "una volta per turno".
        var kael = (await repo.LoadCharacterAsync("pg-kael"))!;
        var sneak = kael.Features.First(f => f.Id == "ft-attacco-furtivo");
        Assert.Equal("PerTurn", sneak.Usage!.Recharge);

        s.UpdateInitiative([Voce("pg-kael", 18), Voce("goblin", 12, "monster")], null);
        s.NextTurn();   // tocca a Kael

        // Nel suo turno lo usa.
        var suo = await s.SubmitIntentAsync(new Intent("pg-kael", "ft-attacco-furtivo"));
        Assert.True(suo.IsValid, suo.Reason);

        // Comincia il turno del Goblin: è un turno NUOVO. Kael fa un attacco di
        // opportunità con la reazione — e l'Attacco Furtivo si ripete.
        s.NextTurn();
        var nelTurnoAltrui = await s.SubmitIntentAsync(new Intent("pg-kael", "ft-attacco-furtivo"));

        Assert.True(nelTurnoAltrui.IsValid,
            "l'Attacco Furtivo deve tornare a ogni turno, non una volta per round: " + nelTurnoAltrui.Reason);
    }

    /// <summary>
    /// Il segnalino ricarica il "per turno", ma <strong>non</strong> restituisce
    /// l'Azione: sono due cose diverse. Il turno del giocatore resta suo — è il
    /// disaccoppiamento chiesto esplicitamente, e non deve rompersi mentre si
    /// aggiusta la regola qui sopra.
    /// </summary>
    [Fact]
    public async Task Il_segnalino_ricarica_il_per_turno_ma_non_restituisce_l_Azione()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var s = NewState();
        await s.InitializeAsync();

        // Kael spende l'Azione col Dardo di Fuoco.
        await s.RollAsync("pg-kael", new RollRequest("ft-dardo-fuoco"));
        Assert.Equal(0, s.TurnRemaining("pg-kael", "Action"));

        s.UpdateInitiative([Voce("pg-kael", 18), Voce("goblin", 12, "monster")], null);
        s.NextTurn();
        s.NextTurn();

        // L'Azione resta spesa: la chiude lui, non il Master.
        Assert.Equal(0, s.TurnRemaining("pg-kael", "Action"));
    }

    [Fact]
    public void Il_prossimo_turno_su_una_lista_vuota_non_esplode()
    {
        var s = NewState();
        s.NextTurn();
        Assert.Null(AttivoDi(s));
    }

    [Fact]
    public void Una_bestia_di_turno_non_cerca_una_scheda_che_non_esiste()
    {
        var s = NewState();
        s.UpdateInitiative([Voce("goblin", 14, "monster")], null);

        // I mostri non hanno una scheda nel motore: se NextTurn provasse a
        // ripristinarne il turno lancerebbe, e il giro si fermerebbe lì.
        s.NextTurn();

        Assert.Equal("goblin", AttivoDi(s));
    }

    public void Dispose()
    {
        _services.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
