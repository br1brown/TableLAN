namespace TableLAN.Server.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableLAN.Core.Profile;
using TableLAN.Server.Data;

/// <summary>
/// Il progetto non ha migration EF: <c>EnsureCreated</c> costruisce lo schema
/// solo su un database nuovo, e su uno già esistente non fa nulla. Una colonna
/// aggiunta dopo il primo rilascio non comparirebbe mai sul database del Master,
/// che morirebbe con "no such column" al primo caricamento.
///
/// Questi test coprono l'unico punto del ridisegno che può perdere dati suoi:
/// il bestiario, la config e le feature che ha creato lui non li rigenera nessun
/// seed. Sono anche i primi test che esistano sul progetto server.
/// </summary>
public sealed class SchemaUpgradeTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tablelan-test-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;

    public SchemaUpgradeTests()
    {
        _services = new ServiceCollection()
            .AddDbContextFactory<AppDb>(o => o.UseSqlite($"Data Source={_dbPath}"))
            .BuildServiceProvider();
    }

    private IDbContextFactory<AppDb> Factory => _services.GetRequiredService<IDbContextFactory<AppDb>>();

    [Fact]
    public async Task Un_database_nuovo_nasce_con_le_colonne_dei_tiri()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        Assert.True(await HasColumnAsync("Features", "Roll"));
        Assert.True(await HasColumnAsync("Characters", "StatRollsJson"));

        var characters = await new GameRepository(Factory).LoadCharactersAsync();
        Assert.Equal(2, characters.Count);
    }



    /// <summary>
    /// Il caso che conta davvero, e che mancava: il database del Master esiste
    /// già da prima del bestiario, quando Characters non era ancora una tabella
    /// TPH. Si simula togliendo le due colonne del discriminatore da uno schema
    /// nuovo — è esattamente ciò che il suo file ha in meno.
    ///
    /// Gli altri test partono da un database vergine, dove EnsureCreated crea
    /// tutto dal modello: passano sempre, per costruzione, e non avrebbero mai
    /// visto questo crash.
    /// </summary>
    [Fact]
    public async Task Un_database_anteriore_al_bestiario_si_aggiorna_invece_di_morire()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        await ExecuteAsync("ALTER TABLE Characters DROP COLUMN Notes");
        await ExecuteAsync("ALTER TABLE Characters DROP COLUMN Type");
        Assert.False(await HasColumnAsync("Characters", "Type"));

        // Qui EnsureCreated è un no-op (le tabelle ci sono già): se l'upgrade
        // non riporta le colonne, il caricamento muore con "no such column".
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        Assert.True(await HasColumnAsync("Characters", "Type"));
        Assert.True(await HasColumnAsync("Characters", "Notes"));

        // Le schede preesistenti devono tornare a materializzarsi: senza il
        // default 'Character' il discriminatore sarebbe vuoto e EF le salterebbe.
        var characters = await new GameRepository(Factory).LoadCharactersAsync();
        Assert.Equal(2, characters.Count);
    }

    [Fact]
    public async Task Rieseguire_il_seed_non_riaggiunge_le_colonne()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        // Un secondo ALTER TABLE sulla stessa colonna sarebbe un errore SQLite:
        // se questo non lancia, il guard di idempotenza funziona.
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        Assert.True(await HasColumnAsync("Features", "Roll"));
    }

    /// <summary>
    /// Il bestiario del Master non deve uscire da loopback.
    ///
    /// <c>Characters</c> è TPH: i mostri stanno nella stessa tabella dei PG, e
    /// il DbSet base li restituisce tutti. Senza filtro finivano in
    /// <c>/api/state</c>, che è LAN: i giocatori vedevano nome e PF di ogni
    /// mostro, e potevano pure indirizzarlo con <c>/api/characters/{id}/hp</c>.
    /// Il bug è vissuto invisibile finché il bestiario è rimasto vuoto.
    /// </summary>
    [Fact]
    public async Task Il_bestiario_non_finisce_fra_le_schede_dei_giocatori()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var repo = new GameRepository(Factory);
        var monsterId = await repo.AddMonsterAsync("Orso Gufo", 59, 13, null);

        var characters = await repo.LoadCharactersAsync();

        Assert.DoesNotContain(characters, c => c.Id == monsterId);
        Assert.Equal(2, characters.Count);                       // i due PG del seed
        Assert.Null(await repo.LoadCharacterAsync(monsterId));   // né uno per uno

        // Il mostro però deve esistere: il filtro non lo cancella, lo tiene al
        // di là del confine.
        Assert.Contains(await repo.ListMonstersAsync(), m => m.Id == monsterId);
    }

    /// <summary>
    /// La campagna aperta ieri adotta il ki al riavvio: il pool nel profilo, il
    /// costo sulla Raffica, i punti sulla scheda. Senza, la Raffica resterebbe
    /// un contatore da tre usi e il ki una stringa decorativa.
    /// </summary>
    [Fact]
    public async Task Una_campagna_nata_prima_del_ki_lo_adotta_al_riavvio()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var opts = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        // Riporto indietro il database com'era: profilo senza la riserva Ki,
        // Raffica a tre usi per riposo breve, Kael senza punti.
        await using (var db = await Factory.CreateDbContextAsync())
        {
            var row = await db.Profiles.FindAsync("active");
            var p = System.Text.Json.JsonSerializer.Deserialize<GameProfile>(row!.Json, opts)!;
            p.Pools.RemoveAll(pool => pool.Id == "Ki");
            row.Json = System.Text.Json.JsonSerializer.Serialize(p, opts);

            var raffica = await db.Features.FindAsync("ft-raffica-di-colpi");
            raffica!.CostsJson = """[{"kind":"BonusAction","amount":1}]""";
            raffica.MaxUses = 3;
            raffica.RemainingUses = 3;
            raffica.Recharge = "ShortRest";
            raffica.CustomJson = """{"risorsa":"ki"}""";

            var kael = await db.Characters.FindAsync("pg-kael");
            kael!.ResourcesJson = "{}";
            await db.SaveChangesAsync();
        }

        // Riavvio.
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var profile = await new GameRepository(Factory).LoadProfileAsync();
        var ki = Assert.Single(profile!.Pools.Where(p => p.Id == "Ki"));
        Assert.Equal(PoolKind.Points, ki.Kind);
        Assert.Equal("ShortRest", ki.RechargeCycle);

        var kaelDopo = (await new GameRepository(Factory).LoadCharactersAsync())
            .First(c => c.Id == "pg-kael");

        // Il costo vero, e niente più contatore: il limite è la riserva.
        var raffica2 = kaelDopo.Features.First(f => f.Id == "ft-raffica-di-colpi");
        Assert.Contains(raffica2.Costs, c => c.Kind == "Ki" && c.EffectiveAmount == 1);
        Assert.Contains(raffica2.Costs, c => c.Kind == "BonusAction");
        Assert.Null(raffica2.Usage);

        // E i punti per pagarlo: una Raffica migrata e impagabile sarebbe
        // corretta e inutilizzabile.
        Assert.Equal(3, kaelDopo.Resources.Remaining("Ki"));
    }

    /// <summary>
    /// Una Raffica che il Master ha già sistemato di suo non viene toccata: il
    /// backfill riconosce solo la riga ancora identica al seed.
    /// </summary>
    [Fact]
    public async Task Il_backfill_del_ki_non_calpesta_le_modifiche_del_master()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        await using (var db = await Factory.CreateDbContextAsync())
        {
            var raffica = await db.Features.FindAsync("ft-raffica-di-colpi");
            raffica!.CostsJson = """[{"kind":"BonusAction","amount":1}]""";
            raffica.MaxUses = 5;              // cinque usi: scelta sua, non del seed
            raffica.RemainingUses = 5;
            raffica.Recharge = "ShortRest";
            await db.SaveChangesAsync();
        }

        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        await using (var db = await Factory.CreateDbContextAsync())
        {
            var raffica = await db.Features.FindAsync("ft-raffica-di-colpi");
            Assert.Equal(5, raffica!.MaxUses);
            Assert.DoesNotContain("Ki", raffica.CostsJson);
        }
    }

    /// <summary>
    /// Il profilo è JSON su disco: il preset in codice può guadagnare un campo,
    /// ma la campagna del Master gira col JSON che aveva. Senza il backfill,
    /// una campagna iniziata prima di oggi avrebbe <c>1d20+@Forza</c> che somma
    /// 18 per sempre — il bug per cui il modificatore è nato, sopravvissuto
    /// alla propria correzione.
    /// </summary>
    [Fact]
    public async Task Un_profilo_nato_prima_dei_modificatori_li_adotta_al_riavvio()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        // Simulo la campagna di ieri: il profilo salvato senza modificatori, e
        // con un'etichetta rinominata dal Master, che non deve andare persa.
        var opts = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        await using (var db = await Factory.CreateDbContextAsync())
        {
            var row = await db.Profiles.FindAsync("active");
            var p = System.Text.Json.JsonSerializer.Deserialize<GameProfile>(row!.Json, opts)!;
            for (int i = 0; i < p.Stats.Count; i++)
            {
                var s = p.Stats[i];
                p.Stats[i] = new StatDef
                {
                    Id = s.Id,
                    Label = s.Id == "for" ? "Vigore" : s.Label,   // rinominata dal Master
                    Default = s.Default,
                    Roll = s.Roll,
                    Modifier = null,                              // com'era prima
                };
            }
            row.Json = System.Text.Json.JsonSerializer.Serialize(p, opts);
            await db.SaveChangesAsync();
        }

        // Riavvio.
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        await using (var db = await Factory.CreateDbContextAsync())
        {
            var row = await db.Profiles.FindAsync("active");
            var p = System.Text.Json.JsonSerializer.Deserialize<GameProfile>(row!.Json, opts)!;

            var forza = p.Stats.First(s => s.Id == "for");
            Assert.NotNull(forza.Modifier);
            Assert.Equal(4, forza.Effective(18));
            // L'aggancio è per id: il nome che il Master ha scelto resta suo.
            Assert.Equal("Vigore", forza.Label);
            // La CA non ne ha, e non deve inventarselo.
            Assert.Null(p.Stats.First(s => s.Id == "ca").Modifier);
        }
    }

    /// <summary>
    /// La Classe Armatura di un mostro deve finire nella CA, non nel Carisma.
    ///
    /// Sembra assurdo e invece succedeva: le statistiche di D&amp;D in italiano
    /// sono Forza… Saggezza, <em>Carisma</em>, CA, e la CA veniva cercata con
    /// <c>Label.StartsWith("CA")</c> — che su "Carisma" è vero, e Carisma viene
    /// prima. Ogni mostro creato aveva la CA scritta nel Carisma, con lo stesso
    /// errore in lettura a rendere la cosa coerente e quindi invisibile.
    /// </summary>
    [Fact]
    public async Task La_classe_armatura_di_un_mostro_non_finisce_nel_carisma()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var repo = new GameRepository(Factory);
        var id = await repo.AddMonsterAsync("Goblin Sfregiato", 7, 13, null);

        var mostri = await repo.LoadMonstersAsync();
        var goblin = mostri.First(m => m.Id == id);

        // TryReadNumber e non un cast: dopo il giro in SQLite un 13 torna come
        // JsonElement, ed è lo stesso lettore che usa il motore dei dadi.
        Assert.Equal(13, Leggi(goblin, "CA"));
        // Il Carisma resta quello del profilo: nessuno l'ha toccato.
        Assert.Equal(10, Leggi(goblin, "Carisma"));
    }

    private static int Leggi(TableLAN.Core.Characters.Character c, string stat)
    {
        Assert.True(TableLAN.Core.Characters.Character.TryReadNumber(c.CustomStats[stat], out var v),
            $"La statistica '{stat}' non è un numero leggibile.");
        return v;
    }

    /// <summary>
    /// La campagna deve stare in un file solo.
    ///
    /// Il README promette al Master: «è un file normale, quindi si rinomina per
    /// archiviarla, si sostituisce per riprenderne un'altra, si copia per fare
    /// un backup». Col WAL — che il driver Microsoft accende da sé — la
    /// campagna vive in tre file, e copiare il solo <c>.db</c> dà un backup a
    /// cui manca tutto il gioco recente. Silenziosamente.
    ///
    /// Questo test è la promessa scritta come codice: se un giorno qualcuno
    /// riaccende il WAL per la concorrenza, deve prima cambiare il README.
    /// </summary>
    [Fact]
    public async Task La_campagna_sta_in_un_file_solo()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        await using (var db = await Factory.CreateDbContextAsync())
        {
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            var modo = (string)(await command.ExecuteScalarAsync())!;

            Assert.Equal("delete", modo, ignoreCase: true);
        }

        // La prova che conta: niente file sidecar accanto alla campagna.
        Assert.False(File.Exists(_dbPath + "-wal"), "esiste un -wal: la campagna non è più un file solo");
        Assert.False(File.Exists(_dbPath + "-shm"), "esiste un -shm: la campagna non è più un file solo");
    }

    /// <summary>
    /// Una campagna già in WAL — cioè ogni campagna esistente, perché finora
    /// l'app le creava così — torna a essere un file solo al riavvio.
    /// </summary>
    [Fact]
    public async Task Una_campagna_gia_in_wal_torna_un_file_solo()
    {
        await using (var db = await Factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }

        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        await using (var db = await Factory.CreateDbContextAsync())
        {
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            Assert.Equal("delete", (string)(await command.ExecuteScalarAsync())!, ignoreCase: true);
        }

        // I dati sopravvivono alla conversione: è la campagna del Master.
        Assert.Equal(2, (await new GameRepository(Factory).LoadCharactersAsync()).Count);
    }

    /// <summary>
    /// La rotta dei giocatori non cancella un mostro.
    ///
    /// Non è teoria: è successo. Finché il bestiario finiva nello snapshot, un
    /// mostro compariva nella lista "Al tavolo" della console, col suo pulsante
    /// "elimina" accanto — e un Goblin Sfregiato è stato cancellato davvero.
    /// Sulla base TPH <c>FindAsync</c> trova anche i mostri: il filtro deve
    /// stare nel repository, non solo nella guardia della rotta.
    /// </summary>
    [Fact]
    public async Task La_rotta_dei_giocatori_non_cancella_un_mostro()
    {
        await using (var db = await Factory.CreateDbContextAsync())
            await SeedData.EnsureSeededAsync(db);

        var repo = new GameRepository(Factory);
        var id = await repo.AddMonsterAsync("Goblin Sfregiato", 7, 13, null);

        await repo.RemoveCharacterAsync(id);

        Assert.Contains(await repo.ListMonstersAsync(), m => m.Id == id);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var db = await Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<bool> HasColumnAsync(string table, string column)
    {
        await using var db = await Factory.CreateDbContextAsync();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

    public void Dispose()
    {
        _services.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
