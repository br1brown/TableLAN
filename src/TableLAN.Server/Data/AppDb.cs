namespace TableLAN.Server.Data;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Persistenza locale embedded su SQLite (Capitolo 11). Pattern ibrido del
/// Capitolo 7/10: colonne fisse per i parametri obbligatori, una singola
/// colonna JSON per gli attributi dinamici homebrew — niente EAV puro.
/// </summary>
public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<SourceRow> Sources => Set<SourceRow>();
    public DbSet<SharedTextRow> SharedTexts => Set<SharedTextRow>();
    public DbSet<FeatureRow> Features => Set<FeatureRow>();
    public DbSet<CharacterRow> Characters => Set<CharacterRow>();
    public DbSet<IntentLogRow> IntentLog => Set<IntentLogRow>();
    public DbSet<MonsterRow> Monsters => Set<MonsterRow>();
    public DbSet<ProfileRow> Profiles => Set<ProfileRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SourceRow>().HasKey(s => s.Id);
        modelBuilder.Entity<SharedTextRow>().HasKey(t => t.Id);
        modelBuilder.Entity<FeatureRow>().HasKey(f => f.Id);
        modelBuilder.Entity<CharacterRow>()
            .HasDiscriminator<string>("Type")
            .HasValue<CharacterRow>("Character")
            .HasValue<MonsterRow>("Monster");
            
        modelBuilder.Entity<IntentLogRow>().HasKey(i => i.Id);
        modelBuilder.Entity<IntentLogRow>().Property(i => i.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<ProfileRow>().HasKey(p => p.Id);
    }
}

/// <summary>
/// Profilo di sistema attivo, serializzato in JSON (Capitolo 7/10). Una sola
/// riga con Id "active": definisce economia del turno, riserve e cicli di
/// riposo del gioco corrente, editabile dal Master.
/// </summary>
public sealed class ProfileRow
{
    public required string Id { get; set; }
    public required string Json { get; set; }
}

/// <summary>Riga dell'entità Fonte.</summary>
public sealed class SourceRow
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? ParentSourceId { get; set; }
}

/// <summary>
/// Testo esplicativo condiviso, bersaglio del Puntatore di Deduplicazione.
/// Viene servito solo on-demand (lazy) all'espansione dell'elemento.
/// </summary>
public sealed class SharedTextRow
{
    public required string Id { get; set; }
    public required string Text { get; set; }
}

public sealed class FeatureRow
{
    public required string Id { get; set; }
    public required string ShortName { get; set; }

    /// <summary>
    /// Costi di attivazione: array JSON di {kind, amount}. Sostituisce la
    /// coppia CostKind/SlotLevel, che poteva esprimere un costo solo e quindi
    /// non sapeva dire "Azione E slot di 1° livello".
    ///
    /// Nullable di proposito: null vuol dire "riga mai migrata", e va letta dal
    /// vecchio CostKind. Un "[]" invece è una passiva migrata davvero — con un
    /// default non-null i due casi sarebbero indistinguibili.
    /// </summary>
    public string? CostsJson { get; set; }

    /// <summary>
    /// Risorse di turno accreditate dall'uso (Action Surge, Attacco Extra).
    /// Default "[]": una feature che non concede niente è la norma, e qui non
    /// c'è nessun vecchio campo da cui migrare.
    /// </summary>
    public string GrantsJson { get; set; } = "[]";

    /// <summary>Slot esclusivo occupato dall'uso (in 5e: "Concentration"). Null quasi sempre.</summary>
    public string? Occupies { get; set; }

    /// <summary>Vecchio costo singolo: resta solo come sorgente del backfill.</summary>
    public string CostKind { get; set; } = "None";

    /// <summary>Vecchio livello/quantità: come sopra.</summary>
    public int SlotLevel { get; set; }
    public int? MaxUses { get; set; }
    public int? RemainingUses { get; set; }
    public string? Recharge { get; set; }
    public required string DescriptionId { get; set; }

    /// <summary>Array JSON degli id delle Fonti che concedono la feature.</summary>
    public required string SourceIdsJson { get; set; }

    /// <summary>Attributi dinamici homebrew (JSON1 di SQLite, Capitolo 10).</summary>
    public string CustomJson { get; set; } = "{}";

    /// <summary>Effetti (modificatori) applicati quando la feature è attiva. Array JSON.</summary>
    public string EffectsJson { get; set; } = "[]";

    /// <summary>Vero per gli oggetti indossabili: i loro effetti si accendono/spengono.</summary>
    public bool Toggleable { get; set; }

    /// <summary>
    /// Formula del tiro di dado (es. "1d6", "1d20+@CA"), null se la feature non
    /// tira. Colonna dedicata e non <see cref="CustomJson"/> perché il motore la
    /// interpreta: ciò che è interpretato non è un attributo di trasporto.
    /// </summary>
    public string? Roll { get; set; }
}

public class CharacterRow
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    /// <summary>PF temporanei: cuscinetto che il danno consuma per primo. 0 = nessuno.</summary>
    public int TempHp { get; set; }

    /// <summary>Array JSON degli id delle Fonti attive del personaggio.</summary>
    public required string SourceIdsJson { get; set; }

    /// <summary>Array JSON degli id delle feature possedute.</summary>
    public required string FeatureIdsJson { get; set; }

    /// <summary>Snapshot JSON degli slot: livello → (max, residui).</summary>
    public string ResourcesJson { get; set; } = "{}";

    /// <summary>Statistiche dinamiche homebrew (es. "Sanità Mentale").</summary>
    public string CustomStatsJson { get; set; } = "{}";

    /// <summary>Inventario ordinabile: array JSON di {id, name, quantity, notes}.</summary>
    public string InventoryJson { get; set; } = "[]";

    /// <summary>Stato on/off delle feature con effetti indossabili: mappa JSON featureId→bool.</summary>
    public string EffectStateJson { get; set; } = "{}";

    /// <summary>
    /// Slot esclusivi occupati: id slot → id feature. In 5e, su cosa stai
    /// concentrando. Si persiste perché dura fra i turni — e fra le sessioni:
    /// se il Master chiude l'app a metà combattimento, la Benedizione regge.
    /// </summary>
    public string OccupiedJson { get; set; } = "{}";

    /// <summary>
    /// Formule di tiro delle statistiche: mappa JSON nome→formula (es.
    /// "Furtività"→"1d20+7"). Mappa parallela e non un valore annidato dentro
    /// <see cref="CustomStatsJson"/>: lì i valori devono restare scalari, o
    /// EffectiveStats() non riesce a coercerli e azzera la base degli effetti.
    /// </summary>
    public string StatRollsJson { get; set; } = "{}";
}

/// <summary>
/// Mostro del bestiario del Master (Capitolo 12): riferimento e traccia PF
/// facoltativa. Vive solo lato Master (loopback), i giocatori non lo vedono —
/// il Master traccia il combattimento come preferisce, questo gli tiene i
/// blocchi statistici che sta preparando per la campagna.
/// </summary>
public class MonsterRow : CharacterRow
{
    /// <summary>Blocco statistico o note libere (copiato dal manuale posseduto).</summary>
    public string Notes { get; set; } = string.Empty;

    // Qui c'era una proprietà ArmorClass che ripescava la CA dal JSON cercando
    // la prima chiave che iniziasse per "CA" — e trovava "Carisma". È stata
    // tolta, non riparata: la CA è una statistica come le altre, sta in
    // CustomStats e si legge da lì come Forza o Destrezza. Un mostro è un
    // personaggio: non gli serve una scorciatoia sua.
}

/// <summary>
/// Registro degli intenti validati: ogni intento è scritto su disco prima
/// del broadcast realtime (Capitolo 11) — atomicità prima della rete.
/// </summary>
public sealed class IntentLogRow
{
    public long Id { get; set; }
    public required string CharacterId { get; set; }
    public required string FeatureId { get; set; }
    public int? SlotLevelUsed { get; set; }
    public DateTime TimestampUtc { get; set; }
}
