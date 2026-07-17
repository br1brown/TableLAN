namespace TableLAN.Core.Dice;

/// <summary>
/// Un singolo dado caduto: quante facce aveva, cosa ha dato, e — se il sistema
/// lo prevede — che <em>ruolo</em> aveva. Il ruolo è una chiave opaca
/// (<c>"hope"</c>/<c>"fear"</c> per la Duality di Daggerheart), null per i dadi
/// anonimi di quasi tutti i tiri: il tipo resta agnostico, la semantica la
/// conoscono solo il resolver che lo assegna e il render che lo colora.
/// </summary>
public readonly record struct DieRoll(int Sides, int Value, string? Role = null);

/// <summary>Una statistica risolta dentro la formula, col valore che aveva.</summary>
public readonly record struct StatRef(string Name, int Value);

/// <summary>
/// Un tentativo completo della formula: i dadi caduti, il modificatore fisso
/// (costanti e statistiche già risolte), il totale, ed eventualmente un
/// <em>esito</em> categorico — anch'esso una chiave opaca (<c>"hope"</c>,
/// <c>"fear"</c>, <c>"crit"</c>), null quando il tiro è solo un numero.
/// </summary>
public sealed record RollAttempt(IReadOnlyList<DieRoll> Dice, int Modifier, int Total, string? Outcome = null);

/// <summary>
/// Esito di un tiro.
///
/// Conserva <em>tutti</em> i tentativi, non solo quello che vale: con
/// vantaggio il dado scartato è metà dell'informazione, e chi tira vuole
/// vederlo. <see cref="KeptIndex"/> dice quale ha fatto testo.
/// </summary>
public sealed record RollResult(
    string Formula,
    IReadOnlyList<RollAttempt> Attempts,
    int KeptIndex,
    IReadOnlyList<StatRef> Resolved)
{
    /// <summary>Il tentativo che conta.</summary>
    public RollAttempt Kept => Attempts[KeptIndex];

    /// <summary>Il totale che conta. Può essere negativo: non si clampa.</summary>
    public int Total => Kept.Total;

    /// <summary>L'esito categorico del tentativo che conta, se il sistema ne ha uno.</summary>
    public string? Outcome => Kept.Outcome;

    /// <summary>Vero se un tentativo è stato scartato (vantaggio/svantaggio).</summary>
    public bool HasDiscarded => Attempts.Count > 1;
}
