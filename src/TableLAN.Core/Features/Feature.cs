namespace TableLAN.Core.Features;

/// <summary>
/// Elemento di gioco generico — abilità, tratto, incantesimo, capacità di
/// oggetto. Porta con sé i parametri obbligatori del Capitolo 7:
/// nome breve, Fonti di appartenenza, costo di attivazione, contatore di
/// utilizzo, puntatore di deduplicazione del testo esplicativo e una borsa
/// di attributi custom (homebrew) in JSON.
/// </summary>
public sealed class Feature
{
    public required string Id { get; init; }

    /// <summary>Etichetta visibile a colpo d'occhio (es. "Attacco Furtivo").</summary>
    public required string ShortName { get; init; }

    /// <summary>
    /// Fonti che concedono questa feature. Se un elemento è comune a più
    /// Fonti compare una volta sola, collegato a tutte (deduplicazione).
    /// </summary>
    public required IReadOnlyList<string> SourceIds { get; init; }

    /// <summary>
    /// Cosa costa attivarla. Lista, non uno solo: lanciare un incantesimo
    /// consuma l'Azione <em>e</em> lo slot, e con un costo singolo quel "e" non
    /// era esprimibile — il seed dichiarava solo lo slot, e il fatto che una
    /// magia fosse anche un'azione andava perduto. Il motore lasciava lanciare
    /// tre incantesimi nello stesso turno.
    ///
    /// Vuota = passiva: non c'è niente da pagare.
    /// </summary>
    public IReadOnlyList<ActivationCost> Costs { get; init; } = [];

    /// <summary>
    /// Risorse di turno che usarla <em>accredita</em>, subito — il rovescio di
    /// <see cref="Costs"/>.
    ///
    /// Serve a due cose che il costo da solo non sa dire. Action Surge concede
    /// «one additional action <em>on top of</em> your regular action»: è un
    /// credito nel turno in corso, e l'effetto <c>@turn:Action +1</c> non basta
    /// perché i limiti si applicano al reset — cioè al turno dopo, quando non
    /// serve più. L'Attacco Extra, invece, è un'Azione che vale <em>due</em>
    /// attacchi: "Azione di Attacco" costa l'Azione e concede due "Attacco",
    /// così il motore sa fermarti al terzo, invece di lasciare gli attacchi a
    /// costo zero e sperare nella buona fede.
    ///
    /// Solo risorse di turno: accreditare uno slot è un'altra storia
    /// (il Recupero Arcano ha un suo budget), e qui non si finge di saperla.
    /// </summary>
    public IReadOnlyList<ActivationCost> Grants { get; init; } = [];

    /// <summary>I crediti che contano davvero, senza i "None" di comodo.</summary>
    public IEnumerable<ActivationCost> RealGrants => Grants.Where(g => !g.IsNone);

    /// <summary>
    /// Slot esclusivo che questa feature occupa finché è attiva; null per la
    /// gran parte. In D&amp;D è la Concentrazione: lanciarne una seconda fa
    /// cadere la prima, «no saving throw and no negotiation».
    ///
    /// Non è un costo: occupare non fallisce mai, sostituisce. Per questo il
    /// motore non lo rifiuta — lo dice, e la scheda mostra cosa stai buttando.
    /// </summary>
    public string? Occupies { get; init; }

    /// <summary>Vero se attivarla non costa nulla.</summary>
    public bool IsFree => Costs.Count == 0 || Costs.All(c => c.IsNone);

    /// <summary>I costi che contano davvero, senza i "None" di comodo.</summary>
    public IEnumerable<ActivationCost> RealCosts => Costs.Where(c => !c.IsNone);

    /// <summary>Null quando la feature non ha usi limitati.</summary>
    public UsageCounter? Usage { get; init; }

    /// <summary>
    /// Puntatore di Deduplicazione: id del testo esplicativo condiviso.
    /// Il testo NON viaggia nel payload iniziale — viene caricato in modo
    /// lazy solo all'espansione dell'elemento (Capitolo 7).
    /// </summary>
    public required string DescriptionId { get; init; }

    /// <summary>
    /// Effetti che modificano la scheda finché la feature è attiva (es. una
    /// mutazione che dà Forza +2, un oggetto che porta CA a 15). Vuota per le
    /// feature che non modificano numeri.
    /// </summary>
    public IReadOnlyList<Effect> Effects { get; init; } = [];

    /// <summary>
    /// Vero per gli oggetti che si indossano/tolgono: i loro effetti si
    /// possono accendere e spegnere. Le mutazioni permanenti sono false
    /// (sempre attive).
    /// </summary>
    public bool Toggleable { get; init; }

    /// <summary>
    /// Formula del tiro di dado (es. "1d6", "1d20+@CA"), null se la feature
    /// non tira.
    ///
    /// È un campo di prima classe e non un attributo in <see cref="CustomData"/>
    /// perché il motore la <em>interpreta</em>: la analizza, ne risolve i
    /// riferimenti alle statistiche e tira. Ciò che è interpretato non è un
    /// dato di trasporto.
    ///
    /// L'Attacco Furtivo è il motivo per cui questo campo esiste: "un numero di
    /// d6" non era un dato da nessuna parte — nemmeno nella sua descrizione, che
    /// diceva solo "danni extra".
    /// </summary>
    public string? Roll { get; init; }

    /// <summary>
    /// Attributi dinamici homebrew (Capitolo 10): il motore non li
    /// interpreta, li trasporta; il client genera i widget al volo.
    /// </summary>
    public IDictionary<string, object?> CustomData { get; init; } =
        new Dictionary<string, object?>();
}
