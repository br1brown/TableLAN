namespace TableLAN.Core.Dice;

/// <summary>
/// Un dado appena caduto, col segno del suo termine: l'ingresso della
/// risoluzione. È distinto da <see cref="DieRoll"/> — quello è per mostrare, e
/// non porta il segno («il segno vive nel totale, non nella faccia») — perché
/// qui il segno serve: la somma ne ha bisogno, il conteggio dei successi conta
/// solo i dadi a segno positivo.
/// </summary>
public readonly record struct RolledDie(int Sides, int Value, int Sign);

/// <summary>
/// Ciò che la risoluzione produce: i dadi da mostrare (con l'eventuale ruolo
/// assegnato), il modificatore da mostrare, il totale che conta e un esito
/// categorico facoltativo.
/// </summary>
public readonly record struct ResolvedRoll(
    IReadOnlyList<DieRoll> Dice,
    int Modifier,
    int Total,
    string? Outcome);

/// <summary>
/// La regola con cui i dadi caduti diventano un risultato.
///
/// È il solo punto di TableLAN che cambia da sistema a sistema, ed è
/// deliberatamente un <em>insieme chiuso</em> di implementazioni — somma,
/// conteggio di successi, Duality — non un registro di plugin né un motore di
/// scripting. Un JSON è dati; una regola di risoluzione è comportamento: nessun
/// profilo la definisce, sceglie solo quale. Il sistema nuovo è una classe nuova
/// qui dentro; gli altri non si toccano. Questo è il confine che il commento
/// «se un giorno servirà sarà un altro progetto» in <see cref="DiceFormula"/>
/// difende — vale per il DSL, non per lo smettere di inchiodare un tiro a uno
/// scalare.
/// </summary>
public interface IRollResolver
{
    /// <summary>Dai dadi caduti e dal modificatore piatto, il risultato.</summary>
    ResolvedRoll Resolve(IReadOnlyList<RolledDie> dice, int modifier);

    /// <summary>
    /// Coda da appendere alla formula leggibile (es. <c>≥6</c> per i successi),
    /// oppure null. La descrizione pre-tiro non conosce l'esito, che nasce solo
    /// quando i dadi cadono.
    /// </summary>
    string? FormulaSuffix { get; }
}
