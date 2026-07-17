namespace TableLAN.Core.Profile;

/// <summary>
/// Profili di sistema predefiniti. Il profilo D&D 5e riproduce esattamente le
/// meccaniche cablate nelle versioni precedenti del motore, così è il default
/// sicuro; gli altri sono punti di partenza pronti che il Master può caricare
/// e ritoccare dalla console, senza toccare il codice.
/// </summary>
public static class GameProfiles
{
    /// <summary>Tutti i preset, nell'ordine con cui compaiono nel menù "Parti da…".</summary>
    public static IReadOnlyList<GameProfile> Presets() =>
    [
        Dnd5e(),
        Pathfinder2e(),
        CallOfCthulhu(),
        WorldOfDarkness(),
        SavageWorlds(),
        Fate(),
    ];

    public static GameProfile Dnd5e() => new()
    {
        Id = "dnd5e",
        Name = "D&D 5e",
        TurnResources =
        [
            new TurnResource { Id = "Action", Label = "Azione" },
            new TurnResource { Id = "BonusAction", Label = "Azione Bonus" },
            new TurnResource { Id = "Reaction", Label = "Reazione" },
            new TurnResource { Id = "Interaction", Label = "Interazione" },
            // Gli attacchi non piovono dal turno: li concede l'azione di
            // Attacco (uno, due dal 5° con Attacco Extra) o la Raffica del
            // Monaco. Per questo parte da zero — averne uno "per turno"
            // vorrebbe dire poter colpire senza fare nulla.
            new TurnResource { Id = "Attack", Label = "Attacco", PerTurn = 0 },
        ],
        Pools =
        [
            new ResourcePoolDef { Id = "SpellSlot", Label = "Slot incantesimo", Kind = PoolKind.Leveled, RechargeCycle = "LongRest" },
            // Il ki è una riserva, non un contatore per capacità: Raffica,
            // Difesa Paziente e Passo del Vento pescano dagli stessi punti.
            // Modellato con tre contatori da 3 usi ciascuno, un monaco di 3°
            // ne spenderebbe nove — e il motore non avrebbe niente da obiettare.
            new ResourcePoolDef { Id = "Ki", Label = "Punti Ki", Kind = PoolKind.Points, RechargeCycle = "ShortRest" },
        ],
        ExclusiveSlots =
        [
            new ExclusiveSlot { Id = "Concentration", Label = "Concentrazione" },
        ],
        Cycles =
        [
            new RestCycle { Id = "PerTurn", Label = "A ogni turno", Rank = 1, PerTurn = true },
            new RestCycle { Id = "ShortRest", Label = "Riposo breve", Rank = 2 },
            new RestCycle { Id = "LongRest", Label = "Riposo lungo", Rank = 3 },
        ],
        // Le sei caratteristiche più la CA. Il tiro è scritto per esteso e non
        // derivato: calcolare (punteggio − 10) / 2 vorrebbe dire cablare l'aritmetica
        // di D&D nel motore, che è precisamente ciò che il profilo evita. Il Master
        // scrive il modificatore che il suo personaggio ha.
        Stats =
        [
            // Le sei caratteristiche portano il modificatore: 18 vale +4, ed è
            // +4 che si somma al d20 — mai 18.
            new StatDef { Id = "for", Label = "Forza", Default = 10, Roll = "1d20+@Forza", Modifier = new() },
            new StatDef { Id = "des", Label = "Destrezza", Default = 10, Roll = "1d20+@Destrezza", Modifier = new() },
            new StatDef { Id = "cos", Label = "Costituzione", Default = 10, Roll = "1d20+@Costituzione", Modifier = new() },
            new StatDef { Id = "int", Label = "Intelligenza", Default = 10, Roll = "1d20+@Intelligenza", Modifier = new() },
            new StatDef { Id = "sag", Label = "Saggezza", Default = 10, Roll = "1d20+@Saggezza", Modifier = new() },
            new StatDef { Id = "car", Label = "Carisma", Default = 10, Roll = "1d20+@Carisma", Modifier = new() },
            // La CA si guarda, non si tira: Roll null.
            new StatDef { Id = "ca", Label = "CA", Default = 10 },
            // Il bonus di competenza: +2 al 1°-4° livello, poi +1 ogni quattro
            // (2 + floor((liv-1)/4)). È un numero nudo — nessun modificatore,
            // perché è già un bonus — e lo scrive il giocatore quando sale di
            // livello. Il motore non sa cos'è un livello, e non deve saperlo:
            // sapendolo dovrebbe anche sapere le classi, e da lì non si torna.
            new StatDef { Id = "competenza", Label = "Competenza", Default = 2 },
        ],
    };

    /// <summary>Pathfinder 2e: economia a 3 azioni per turno + reazione, slot e punti focus.</summary>
    public static GameProfile Pathfinder2e() => new()
    {
        Id = "pf2e",
        Name = "Pathfinder 2e",
        TurnResources =
        [
            new TurnResource { Id = "Action", Label = "Azione", PerTurn = 3 },
            new TurnResource { Id = "Reaction", Label = "Reazione" },
        ],
        Pools =
        [
            new ResourcePoolDef { Id = "SpellSlot", Label = "Slot incantesimo", Kind = PoolKind.Leveled, RechargeCycle = "LongRest" },
            new ResourcePoolDef { Id = "Focus", Label = "Punti Focus", Kind = PoolKind.Points, RechargeCycle = "ShortRest" },
        ],
        Cycles =
        [
            new RestCycle { Id = "PerTurn", Label = "A ogni turno", Rank = 1, PerTurn = true },
            new RestCycle { Id = "ShortRest", Label = "Rifocalizzazione", Rank = 2 },
            new RestCycle { Id = "LongRest", Label = "Riposo lungo", Rank = 3 },
        ],
        // Stessa formula del modificatore di D&D — floor((v-10)/2) — perché
        // Pathfinder 2e la eredita tale e quale. Cambia tutto il resto: la
        // competenza non è una tabella per livello ma **livello + grado**
        // (Addestrato +2, Esperto +4, Maestro +6, Leggendario +8), quindi qui
        // "Competenza" è una statistica che il giocatore si scrive, non un
        // numero che il motore calcola. La CA in PF2e è 10 + Des + competenza
        // in armatura + oggetto: si guarda, non si tira.
        Stats =
        [
            new StatDef { Id = "for", Label = "Forza", Default = 10, Roll = "1d20+@Forza+@Competenza", Modifier = new() },
            new StatDef { Id = "des", Label = "Destrezza", Default = 10, Roll = "1d20+@Destrezza+@Competenza", Modifier = new() },
            new StatDef { Id = "cos", Label = "Costituzione", Default = 10, Roll = "1d20+@Costituzione+@Competenza", Modifier = new() },
            new StatDef { Id = "int", Label = "Intelligenza", Default = 10, Roll = "1d20+@Intelligenza+@Competenza", Modifier = new() },
            new StatDef { Id = "sag", Label = "Saggezza", Default = 10, Roll = "1d20+@Saggezza+@Competenza", Modifier = new() },
            new StatDef { Id = "car", Label = "Carisma", Default = 10, Roll = "1d20+@Carisma+@Competenza", Modifier = new() },
            new StatDef { Id = "ca", Label = "CA", Default = 10 },
            // Numero nudo, nessun modificatore: è già un bonus, non un punteggio.
            new StatDef { Id = "competenza", Label = "Competenza", Default = 3 },
        ],
    };

    /// <summary>Call of Cthulhu: niente slot, Punti Magia come pool a punti.</summary>
    public static GameProfile CallOfCthulhu() => new()
    {
        Id = "coc",
        Name = "Call of Cthulhu",
        TurnResources =
        [
            new TurnResource { Id = "Action", Label = "Azione" },
            new TurnResource { Id = "Reaction", Label = "Reazione" },
        ],
        Pools =
        [
            new ResourcePoolDef { Id = "MagicPoints", Label = "Punti Magia", Kind = PoolKind.Points, RechargeCycle = "Rest" },
        ],
        Cycles =
        [
            new RestCycle { Id = "PerTurn", Label = "A ogni turno", Rank = 1, PerTurn = true },
            new RestCycle { Id = "Rest", Label = "Riposo (recupero PM)", Rank = 2 },
        ],
    };

    /// <summary>
    /// Mondo di Tenebra / Vampiri. È il preset che mostra perché statistiche e
    /// riserve sono due cose diverse: il Sangue si spende (riserva), l'Umanità
    /// si ha e si tira (statistica), e la Forza di Volontà è entrambe — un
    /// punteggio permanente che si tira e dei punti temporanei che si spendono.
    /// Per questo compare in tutte e due le liste, con lo stesso nome e ruoli
    /// diversi. Non è un errore di modellazione: è il gioco che funziona così.
    /// </summary>
    public static GameProfile WorldOfDarkness() => new()
    {
        Id = "wod",
        Name = "Vampiri / Mondo di Tenebra",
        TurnResources =
        [
            new TurnResource { Id = "Action", Label = "Azione" },
            new TurnResource { Id = "Reaction", Label = "Azione Riflessiva" },
        ],
        Pools =
        [
            new ResourcePoolDef { Id = "Vitae", Label = "Sangue", Kind = PoolKind.Points, RechargeCycle = "Notte" },
            new ResourcePoolDef { Id = "Willpower", Label = "Forza di Volontà", Kind = PoolKind.Points, RechargeCycle = "Notte" },
        ],
        Cycles =
        [
            new RestCycle { Id = "PerTurn", Label = "A ogni turno", Rank = 1, PerTurn = true },
            new RestCycle { Id = "Scena", Label = "Scena", Rank = 2 },
            new RestCycle { Id = "Notte", Label = "Notte", Rank = 3 },
        ],
        // Qui i tiri ci sono, e sono pool: "@Nome d10 >= 6" tira tanti d10
        // quanto vale il punteggio e conta le facce da 6 in su. È il modo in
        // cui il Mondo di Tenebra tira, e finché il motore non ha saputo farlo
        // questo preset aveva l'economia del turno giusta e i dadi no.
        //
        // Nessun modificatore: i punteggi valgono interi. Non è una
        // dimenticanza — è che nel Mondo di Tenebra un 4 vale quattro dadi, non
        // "+4". La regola del modificatore è di D&D, e questo non è D&D.
        //
        // Una prova vera somma due pool: "@Destrezza d10 + @{Furtività} d10
        // >= 6" — Attributo + Abilità, come vuole il sistema. Le abilità le
        // aggiunge il Master sulla scheda: sono sue, non del vocabolario.
        Stats =
        [
            new StatDef { Id = "forza", Label = "Forza", Default = 2, Roll = "@Forza d10 >= 6" },
            new StatDef { Id = "destrezza", Label = "Destrezza", Default = 2, Roll = "@Destrezza d10 >= 6" },
            new StatDef { Id = "prontezza", Label = "Prontezza", Default = 2, Roll = "@Prontezza d10 >= 6" },
            new StatDef { Id = "intelligenza", Label = "Intelligenza", Default = 2, Roll = "@Intelligenza d10 >= 6" },
            new StatDef { Id = "manipolazione", Label = "Manipolazione", Default = 2, Roll = "@Manipolazione d10 >= 6" },
            new StatDef { Id = "umanita", Label = "Umanità", Default = 7, Roll = "@Umanità d10 >= 6" },
            new StatDef { Id = "volonta", Label = "Forza di Volontà", Default = 5, Roll = "@{Forza di Volontà} d10 >= 6" },
            // Si guarda e basta: la Generazione dice quanto sangue puoi tenere,
            // non è un pool da tirare.
            new StatDef { Id = "generazione", Label = "Generazione", Default = 13 },
        ],
    };

    /// <summary>Savage Worlds: Punti Potere e Bennies (che si azzerano a sessione).</summary>
    public static GameProfile SavageWorlds() => new()
    {
        Id = "savage",
        Name = "Savage Worlds",
        TurnResources =
        [
            new TurnResource { Id = "Action", Label = "Azione" },
        ],
        Pools =
        [
            new ResourcePoolDef { Id = "PowerPoints", Label = "Punti Potere", Kind = PoolKind.Points, RechargeCycle = "Rest" },
            new ResourcePoolDef { Id = "Bennies", Label = "Bennies", Kind = PoolKind.Points, RechargeCycle = "Sessione" },
        ],
        Cycles =
        [
            new RestCycle { Id = "PerTurn", Label = "A ogni turno", Rank = 1, PerTurn = true },
            new RestCycle { Id = "Rest", Label = "Riposo", Rank = 2 },
            new RestCycle { Id = "Sessione", Label = "Sessione", Rank = 3 },
        ],
    };

    /// <summary>Fate: Punti Fato come pool a punti, che si ripristina a sessione.</summary>
    public static GameProfile Fate() => new()
    {
        Id = "fate",
        Name = "Fate",
        TurnResources =
        [
            new TurnResource { Id = "Action", Label = "Azione" },
        ],
        Pools =
        [
            new ResourcePoolDef { Id = "FatePoints", Label = "Punti Fato", Kind = PoolKind.Points, RechargeCycle = "Sessione" },
        ],
        Cycles =
        [
            new RestCycle { Id = "PerTurn", Label = "A ogni turno", Rank = 1, PerTurn = true },
            new RestCycle { Id = "Sessione", Label = "Sessione", Rank = 2 },
        ],
    };
}
