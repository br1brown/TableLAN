namespace TableLAN.Core.Dice;

/// <summary>
/// Il caso generale: si somma. <c>Σ(segno·valore)</c> dei dadi più il
/// modificatore. Nessun esito: un tiro di D&amp;D, Pathfinder o Fabula Ultima è
/// solo un numero.
/// </summary>
public sealed class SumResolver : IRollResolver
{
    public static readonly SumResolver Instance = new();

    public string? FormulaSuffix => null;

    public ResolvedRoll Resolve(IReadOnlyList<RolledDie> dice, int modifier)
    {
        var diceTotal = 0;
        var shown = new List<DieRoll>(dice.Count);
        foreach (var d in dice)
        {
            diceTotal += d.Sign * d.Value;
            shown.Add(new DieRoll(d.Sides, d.Value));
        }
        return new ResolvedRoll(shown, modifier, diceTotal + modifier, Outcome: null);
    }
}

/// <summary>
/// I pool a successi: non si somma, si <em>contano</em> le facce che arrivano a
/// una soglia. <c>@Destrezzad10 + @{Furtività}d10 ≥ 6</c> del Mondo di Tenebra —
/// tanti d10 quanti Destrezza+Furtività, ogni 6 o più è un successo.
///
/// Il modificatore non conta: in un pool non esiste «+2 al totale». E un dado a
/// segno negativo non può togliere un successo — i pool non sommano.
/// </summary>
public sealed class SuccessResolver(int threshold) : IRollResolver
{
    public int Threshold { get; } = threshold;

    public string? FormulaSuffix => $"≥{Threshold}";

    public ResolvedRoll Resolve(IReadOnlyList<RolledDie> dice, int modifier)
    {
        var successi = 0;
        var shown = new List<DieRoll>(dice.Count);
        foreach (var d in dice)
        {
            if (d.Sign > 0 && d.Value >= Threshold)
                successi++;
            shown.Add(new DieRoll(d.Sides, d.Value));
        }
        // Modificatore mostrato 0: in un pool non c'è nulla da sommare.
        return new ResolvedRoll(shown, Modifier: 0, successi, Outcome: null);
    }
}

/// <summary>
/// I Duality Dice di Daggerheart: due dadi (i 2d12 dell'azione), uno di Speranza
/// e uno di Paura. Si sommano col modificatore come sempre, ma conta anche
/// <em>quale dei due è più alto</em>:
/// <list type="bullet">
///   <item>uguali → <c>crit</c> (critico);</item>
///   <item>Speranza &gt; Paura → <c>hope</c>;</item>
///   <item>altrimenti → <c>fear</c>.</item>
/// </list>
/// Il primo dado è la Speranza, il secondo la Paura: l'ordine lo fissa la
/// formula, che il parser valida essere esattamente due dadi uguali a segno
/// positivo. La Difficoltà non entra qui di proposito — TableLAN mostra il
/// totale e l'esito, il successo lo giudica il tavolo, come per ogni altro tiro.
/// </summary>
public sealed class DualityResolver : IRollResolver
{
    public const string Hope = "hope";
    public const string Fear = "fear";
    public const string Crit = "crit";

    public string? FormulaSuffix => null;

    public ResolvedRoll Resolve(IReadOnlyList<RolledDie> dice, int modifier)
    {
        // Il parser garantisce esattamente due dadi: se così non fosse è un bug
        // di costruzione, non un input da gestire con grazia.
        var hope = dice[0];
        var fear = dice[1];

        var outcome = hope.Value == fear.Value ? Crit
                    : hope.Value > fear.Value ? Hope
                    : Fear;

        var shown = new List<DieRoll>(2)
        {
            new(hope.Sides, hope.Value, Hope),
            new(fear.Sides, fear.Value, Fear),
        };
        return new ResolvedRoll(shown, modifier, hope.Value + fear.Value + modifier, outcome);
    }
}
