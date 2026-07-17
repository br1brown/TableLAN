namespace TableLAN.Core.Profile;

using System.Text.Json.Serialization;

/// <summary>
/// Profilo di sistema: il "vocabolario delle meccaniche" che rende il motore
/// adattabile ad altri giochi senza toccare codice. Definisce quali risorse
/// di turno esistono, quali riserve (pool) di risorse, e quali cicli di
/// riposo. Il motore non ha più enum cablati su D&D 5e: legge questo profilo.
///
/// Le feature continuano a salvare id-stringa per costo e ricarica; è il
/// profilo a dire cosa significa ciascun id — così cambiare sistema significa
/// cambiare il profilo, non il codice.
/// </summary>
public sealed class GameProfile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Risorse dell'economia del turno (es. Azione, Azione Bonus, Reazione).</summary>
    public List<TurnResource> TurnResources { get; init; } = [];

    /// <summary>Riserve di risorse (es. slot incantesimo a livelli, punti ki/mana).</summary>
    public List<ResourcePoolDef> Pools { get; init; } = [];

    /// <summary>Cicli di riposo ordinati per rango: un ciclo maggiore include i minori.</summary>
    public List<RestCycle> Cycles { get; init; } = [];

    /// <summary>
    /// Slot esclusivi: posti in cui può stare <em>una cosa sola</em>, e che
    /// durano fra i turni. In D&amp;D è la Concentrazione.
    ///
    /// È il terzo tipo di meccanica, dopo le risorse di turno e le riserve, e
    /// non si riduce a nessuna delle due: non si spende (non "consumi" una
    /// concentrazione) e non si ricarica a un riposo — semplicemente, quando ci
    /// metti dentro qualcosa, ciò che c'era prima cade. Sta nel profilo e non
    /// nel motore perché "concentrazione" è una parola di D&amp;D: un altro
    /// sistema ci mette la sua posa, o non ne ha nessuna.
    /// </summary>
    public List<ExclusiveSlot> ExclusiveSlots { get; init; } = [];

    /// <summary>Lo slot esclusivo con questo id, se il profilo lo dichiara.</summary>
    public ExclusiveSlot? Exclusive(string? id) =>
        id is null ? null : ExclusiveSlots.FirstOrDefault(s => s.Id == id);

    /// <summary>
    /// Le statistiche del sistema (es. Forza…Carisma in D&amp;D, Umanità in
    /// Vampiri). Sono <em>del gioco</em>, non del singolo personaggio: senza
    /// questa lista ogni scheda doveva reinventarsi i propri nomi a mano, e
    /// "Contaminazione" era una stringa che il Master ridigitava ogni volta.
    /// </summary>
    public List<StatDef> Stats { get; init; } = [];

    public StatDef? Stat(string id) => Stats.FirstOrDefault(s => s.Id == id);

    public TurnResource? TurnResource(string id) =>
        TurnResources.FirstOrDefault(t => t.Id == id);

    public ResourcePoolDef? Pool(string id) =>
        Pools.FirstOrDefault(p => p.Id == id);

    public RestCycle? Cycle(string id) =>
        Cycles.FirstOrDefault(c => c.Id == id);

    public bool IsTurnResource(string id) => TurnResource(id) is not null;

    public bool IsPool(string id) => Pool(id) is not null;
}

/// <summary>
/// Una statistica del sistema: un valore che una scheda porta e che di solito
/// si tira.
///
/// È volutamente distinta da <see cref="ResourcePoolDef"/>, e la differenza non
/// è accademica — è quella che i sistemi fanno davvero. In Vampiri il Sangue è
/// una riserva (si spende), l'Umanità è una statistica (si ha, e si tira), e la
/// Forza di Volontà è **entrambe**: un punteggio permanente che si tira e una
/// riserva temporanea che si spende. Un profilo può quindi dichiarare lo stesso
/// concetto in tutte e due le liste, ed è corretto così.
///
/// Le abilità restano fuori: sono un altro discorso, con derivazioni e
/// specializzazioni che questo vocabolario non prova a coprire.
/// </summary>
public sealed class StatDef
{
    /// <summary>Codice interno stabile: rinominare l'etichetta non orfana nulla.</summary>
    public required string Id { get; init; }

    /// <summary>Nome mostrato, ed è anche la chiave con cui le formule la citano (@Nome).</summary>
    public required string Label { get; init; }

    /// <summary>Valore di partenza su una scheda nuova.</summary>
    public int Default { get; init; }

    /// <summary>
    /// Formula con cui si tira, se si tira (es. "1d20+@Forza", "1d10").
    /// Null = statistica che si guarda e basta, come i PF massimi.
    /// Il default del sistema; una scheda può sovrascriverlo.
    /// </summary>
    public string? Roll { get; init; }

    /// <summary>
    /// Come si ricava il modificatore da questo punteggio, se ne ha uno.
    ///
    /// Null = la statistica non ha modificatore, e nelle formule vale per
    /// intero: la CA si somma tutta, l'Umanità di Vampiri non si somma a
    /// niente. È il default, perché è il caso generale — è D&D a essere strano.
    /// </summary>
    public StatModifier? Modifier { get; init; }

    /// <summary>Il modificatore di un punteggio, o il punteggio stesso se non ne ha.</summary>
    public int Effective(int value) => Modifier?.Of(value) ?? value;
}

/// <summary>
/// La regola con cui un punteggio diventa un modificatore: <c>floor((v - Base) / Div)</c>.
///
/// Esiste perché senza, <c>1d20+@Forza</c> con Forza 18 tirava <c>1d20+18</c>:
/// legale, silenzioso e assurdo. È la sola derivazione che il motore fa, e
/// costa poco perché dipende da <em>una</em> statistica — niente grafo delle
/// dipendenze, niente ordine di valutazione, nessun ciclo possibile.
///
/// D&amp;D 5e e Pathfinder 2e: Base 10, Div 2. Altri sistemi mettono i loro
/// numeri, o lasciano <c>Modifier</c> nullo e non ne parlano più.
/// </summary>
public sealed class StatModifier
{
    public int Base { get; init; } = 10;

    /// <summary>Passo del modificatore. Zero non ha senso: si tratta come 1.</summary>
    public int Div { get; init; } = 2;

    /// <summary>
    /// Arrotonda <strong>verso il basso</strong>, sempre, anche sotto zero: in
    /// D&amp;D un punteggio di 7 dà −2, non −1. La divisione fra interi di C#
    /// tronca verso lo zero e darebbe −1 — cioè un personaggio scarso meno
    /// scarso del dovuto, e nessuno se ne accorgerebbe mai.
    /// </summary>
    public int Of(int value)
    {
        var div = Div == 0 ? 1 : Div;
        return (int)Math.Floor((value - Base) / (double)div);
    }
}

/// <summary>
/// Un posto in cui può stare una cosa sola per volta, che dura fra i turni.
/// In D&amp;D 5e: la Concentrazione.
/// </summary>
public sealed class ExclusiveSlot
{
    public required string Id { get; init; }      // es. "Concentration"
    public required string Label { get; init; }   // es. "Concentrazione"
}

/// <summary>Una risorsa spendibile nel turno, ripristinata a ogni nuovo turno.</summary>
public sealed class TurnResource
{
    public required string Id { get; init; }      // es. "Action"
    public required string Label { get; init; }   // es. "Azione"

    /// <summary>Quante se ne hanno per turno (di norma 1; es. 3 per un pool di punti azione).</summary>
    public int PerTurn { get; init; } = 1;
}

public enum PoolKind
{
    /// <summary>A livelli con upcasting, come gli slot incantesimo 5e.</summary>
    Leveled,

    /// <summary>Un totale unico da cui si spende una quantità, come mana o ki.</summary>
    Points,
}

/// <summary>Definizione di una riserva di risorse (la struttura, non i valori del personaggio).</summary>
public sealed class ResourcePoolDef
{
    public required string Id { get; init; }      // es. "SpellSlot"
    public required string Label { get; init; }   // es. "Slot incantesimo"

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required PoolKind Kind { get; init; }

    /// <summary>Id del ciclo di riposo che la ricarica (es. "LongRest").</summary>
    public string RechargeCycle { get; init; } = string.Empty;
}

/// <summary>Un ciclo di riposo/ripristino, ordinato per rango.</summary>
public sealed class RestCycle
{
    public required string Id { get; init; }      // es. "LongRest"
    public required string Label { get; init; }   // es. "Riposo lungo"

    /// <summary>Ordine di "ampiezza": un riposo di rango N ricarica tutto ciò di rango ≤ N.</summary>
    public int Rank { get; init; }

    /// <summary>Vero per il ciclo "a ogni turno" (ripristino all'inizio del turno, non col riposo).</summary>
    public bool PerTurn { get; init; }
}
