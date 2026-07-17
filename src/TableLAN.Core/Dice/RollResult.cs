namespace TableLAN.Core.Dice;

/// <summary>Un singolo dado caduto: quante facce aveva, e cosa ha dato.</summary>
public readonly record struct DieRoll(int Sides, int Value);

/// <summary>Una statistica risolta dentro la formula, col valore che aveva.</summary>
public readonly record struct StatRef(string Name, int Value);

/// <summary>
/// Un tentativo completo della formula: i dadi caduti, il modificatore fisso
/// (costanti e statistiche già risolte) e il totale.
/// </summary>
public sealed record RollAttempt(IReadOnlyList<DieRoll> Dice, int Modifier, int Total);

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

    /// <summary>Vero se un tentativo è stato scartato (vantaggio/svantaggio).</summary>
    public bool HasDiscarded => Attempts.Count > 1;
}
