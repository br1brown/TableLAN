namespace TableLAN.Server.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableLAN.Server.Data;
using TableLAN.Server.Services;

/// <summary>
/// Il tiro libero è nato da un buco trovato al primo tavolo vero: il Master non
/// aveva modo di tirare, e non ogni tiro contestuale del manuale sta su una
/// scheda. Questi test coprono le due porte — Master e giocatore — e stanno qui,
/// coi test di sessione, perché il tiro libero è stato del server (chi tira,
/// sotto che nome, con quali statistiche), non dominio del motore dei dadi.
/// </summary>
public sealed class FreeRollTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tablelan-free-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;

    public FreeRollTests()
    {
        _services = new ServiceCollection()
            .AddDbContextFactory<AppDb>(o => o.UseSqlite($"Data Source={_dbPath}"))
            .BuildServiceProvider();
    }

    private IDbContextFactory<AppDb> Factory => _services.GetRequiredService<IDbContextFactory<AppDb>>();

    private GameStateService NewState() => new(new GameRepository(Factory), new RollLogService());

    [Fact]
    public void Il_tiro_del_Master_appare_sotto_il_nome_Master()
    {
        var s = NewState();

        var esito = s.RollFreeAsMaster(new FreeRollRequest("2d6+3", "Attacco del goblin"));

        Assert.True(esito.Verdict.IsValid, esito.Verdict.Reason);
        Assert.Equal("Master", esito.Entry!.CharacterName);
        Assert.Equal(GameStateService.MasterId, esito.Entry.CharacterId);
        Assert.Equal("Attacco del goblin", esito.Entry.Label);
        Assert.InRange(esito.Entry.Total, 5, 15);   // 2d6+3
    }

    [Fact]
    public void Il_Master_senza_scheda_non_ha_statistiche_e_un_riferimento_fallisce_con_un_messaggio()
    {
        var s = NewState();

        // @Forza non esiste per il Master: dev'essere un rifiuto spiegato, non un
        // dado muto a zero.
        var esito = s.RollFreeAsMaster(new FreeRollRequest("1d20+@Forza"));

        Assert.False(esito.Verdict.IsValid);
        Assert.Null(esito.Entry);
    }

    [Fact]
    public async Task Il_tiro_libero_del_giocatore_va_sotto_il_suo_nome_e_con_le_sue_statistiche()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var repo = new GameRepository(Factory);
        var s = NewState();
        await s.InitializeAsync();

        // Forza 18 → modificatore +4 (la traduzione la fa il servizio, non il
        // motore): la stessa regola che vale per i tiri delle feature vale qui.
        await repo.SetCustomStatAsync("pg-kael", "Forza", 18);
        await s.ReloadAllCharactersAsync();

        var esito = s.RollFreeForCharacter("pg-kael", new FreeRollRequest("1d20+@Forza"));

        Assert.True(esito.Verdict.IsValid, esito.Verdict.Reason);
        Assert.Equal("pg-kael", esito.Entry!.CharacterId);
        var tentativo = esito.Entry.Attempts[esito.Entry.KeptIndex];
        Assert.Equal(4, tentativo.Modifier);            // +4, non +18
        Assert.InRange(esito.Entry.Total, 5, 24);       // 1d20+4
    }

    [Fact]
    public void Una_formula_rotta_e_un_rifiuto_non_un_tiro()
    {
        var s = NewState();

        var esito = s.RollFreeAsMaster(new FreeRollRequest("pippo"));

        Assert.False(esito.Verdict.IsValid);
        Assert.Null(esito.Entry);
    }

    [Fact]
    public void Senza_formula_non_si_tira()
    {
        var s = NewState();

        Assert.False(s.RollFreeAsMaster(new FreeRollRequest("")).Verdict.IsValid);
        Assert.False(s.RollFreeAsMaster(new FreeRollRequest("   ")).Verdict.IsValid);
        Assert.False(s.RollFreeAsMaster(new FreeRollRequest(null)).Verdict.IsValid);
    }

    [Fact]
    public void Il_tiro_libero_di_un_personaggio_sconosciuto_e_un_rifiuto()
    {
        var s = NewState();

        var esito = s.RollFreeForCharacter("nessuno", new FreeRollRequest("1d20"));

        Assert.False(esito.Verdict.IsValid);
        Assert.Null(esito.Entry);
    }

    [Fact]
    public void Con_vantaggio_tiene_il_migliore_dei_due_tentativi()
    {
        var s = NewState();

        // Times 2 + Highest = vantaggio: due tentativi, entrambi conservati, e
        // il tenuto è il più alto. Non è un caso fortunato — è la definizione.
        var esito = s.RollFreeAsMaster(new FreeRollRequest("1d20", Times: 2, Keep: "Highest"));

        Assert.True(esito.Verdict.IsValid, esito.Verdict.Reason);
        Assert.Equal(2, esito.Entry!.Attempts.Count);
        var tenuto = esito.Entry.Attempts[esito.Entry.KeptIndex].Total;
        Assert.True(esito.Entry.Attempts.All(a => a.Total <= tenuto));
    }

    /// <summary>
    /// Il vassoio del profilo deve viaggiare nello snapshot, non solo dalla
    /// rotta admin.
    ///
    /// Questo test nasce da un bug preso solo col browser: la proiezione del
    /// profilo dentro <c>Snapshot()</c> ometteva i dadi, e il client — che il
    /// vassoio lo legge da lì, non da /api/admin/profile — ricadeva sempre sul
    /// set standard, ignorando ciò che il Master aveva configurato. Uno smoke
    /// test sull'endpoint admin non lo vedeva; questo sì.
    /// </summary>
    [Fact]
    public async Task Lo_snapshot_porta_il_vassoio_dei_dadi_del_profilo()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var s = NewState();
        await s.InitializeAsync();

        var json = System.Text.Json.JsonSerializer.Serialize(s.Snapshot());
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        var dice = doc.RootElement.GetProperty("profile").GetProperty("dice");
        Assert.Equal(System.Text.Json.JsonValueKind.Array, dice.ValueKind);
        Assert.NotEqual(0, dice.GetArrayLength());

        // Ogni voce ha etichetta e formula: è la forma che il client si aspetta.
        var first = dice[0];
        Assert.True(first.TryGetProperty("label", out _));
        Assert.True(first.TryGetProperty("formula", out _));
    }

    public void Dispose()
    {
        _services.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
