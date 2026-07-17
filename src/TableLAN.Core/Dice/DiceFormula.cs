namespace TableLAN.Core.Dice;

using System.Globalization;
using System.Text;
using TableLAN.Core.Engine;
using TableLAN.Core.Features;

/// <summary>
/// Una formula di tiro, analizzata una volta e poi valutabile.
///
/// Grammatica, deliberatamente piatta:
/// <code>
///   Formula := ('duality:')? Termine (('+' | '-') Termine)* ('>=' N)?
///   Termine := NdM | dM | N | @Nome | @{Nome con spazi} | @Nome dM
/// </code>
///
/// Due forme servono ai sistemi a pool, e non erano qui all'inizio:
/// <list type="bullet">
///   <item><c>@Nome dM</c> — quanti dadi lo dice il punteggio. Nel Mondo di
///         Tenebra si tirano tanti d10 quanto vale la caratteristica, e
///         <c>7d10</c> resterebbe a sette dadi anche cambiando il punteggio.</item>
///   <item><c>&gt;= N</c> in coda — non si somma: si <em>contano le facce</em>
///         che arrivano a N. <c>@Destrezzad10 + @{Furtività}d10 &gt;= 6</c> è
///         una prova di Vampiri: tanti d10 quanto Destrezza+Furtività, e ogni
///         dado da 6 in su è un successo.</item>
/// </list>
/// Fuori restano i dadi che esplodono e il Wild Die di Savage Worlds: quelli
/// cambiano <em>quanti</em> dadi cadono mentre cadono, ed è un altro motore.
/// I termini sono una lista con segno, quindi valutare è una somma — niente
/// albero sintattico, niente precedenza, niente parentesi. Se un giorno servirà
/// <c>(1d6)*2</c> sarà un altro progetto: non lo si costruisce adesso "per ogni
/// evenienza".
///
/// I riferimenti a statistica si risolvono sui valori <em>effettivi</em> del
/// personaggio: un anello che dà CA +1 cambia da solo ogni formula che dice
/// <c>@CA</c>, senza che i dadi sappiano nulla di anelli.
/// Le graffe non sono un vezzo: esistono statistiche come "Sanità Mentale", e
/// <c>@Sanità Mentale</c> non avrebbe un terminatore.
/// </summary>
public sealed record DiceFormula(IReadOnlyList<DiceTerm> Terms, string Source, IRollResolver Resolver)
{
    /// <summary>
    /// Nome riservato per i PF massimi. È <see cref="Effect.MaxHpTarget"/> senza
    /// la chiocciola: stesso token degli effetti, così <c>@maxhp</c> vuol dire
    /// la stessa cosa in tutto il dominio.
    /// </summary>
    public static readonly string MaxHpRef = Effect.MaxHpTarget[1..];

    // Un tiro va scritto a mano dal Master: le guardie servono a fermare uno
    // zero di troppo (100000d6), non un attacco.
    private const int MaxDiceCount = 100;
    private const int MaxSides = 1000;

    public override string ToString() => Source;

    /// <summary>
    /// Analizza una formula. <paramref name="error"/> è in italiano e mostrabile.
    /// </summary>
    public static bool TryParse(string? input, out DiceFormula? formula, out string? error)
    {
        formula = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "La formula è vuota.";
            return false;
        }

        // La modalità viaggia nella formula, come la soglia «≥ N» in coda: un
        // prefisso «duality:» segna il tiro Duality di Daggerheart. Source tiene
        // la stringa intera com'è stata scritta; il parsing procede sul resto.
        var source = input.Trim();
        var text = source;
        var duality = false;
        if (text.StartsWith("duality:", StringComparison.OrdinalIgnoreCase))
        {
            duality = true;
            text = text["duality:".Length..].Trim();
        }

        var terms = new List<DiceTerm>();
        var i = 0;
        var sign = 1;
        int? soglia = null;

        while (true)
        {
            SkipSpaces(text, ref i);
            if (i >= text.Length)
            {
                error = terms.Count == 0
                    ? "La formula è vuota."
                    : $"Manca un termine dopo '{(sign > 0 ? '+' : '-')}' in «{text}».";
                return false;
            }

            if (!TryParseTerm(text, ref i, sign, out var term, out error))
                return false;

            terms.Add(term);

            SkipSpaces(text, ref i);
            if (i >= text.Length)
                break;

            // '>= N' in coda chiude la formula: da qui in poi si contano i
            // successi invece di sommare.
            if (text[i] == '>')
            {
                if (!TryParseThreshold(text, ref i, out soglia, out error))
                    return false;
                break;
            }

            var op = text[i];
            if (op != '+' && op != '-')
            {
                error = $"Carattere inatteso '{op}' in «{text}».";
                return false;
            }

            sign = op == '+' ? 1 : -1;
            i++;
        }

        if (soglia is not null && !terms.Any(t => t.IsDice || t.IsStatDice))
        {
            error = $"«{text}» conta i successi ma non tira dadi.";
            return false;
        }

        IRollResolver resolver;
        if (duality)
        {
            if (!TryBuildDuality(terms, soglia, text, out resolver!, out error))
                return false;
        }
        else if (soglia is not null)
        {
            resolver = new SuccessResolver(soglia.Value);
        }
        else
        {
            resolver = SumResolver.Instance;
        }

        formula = new DiceFormula(terms, source, resolver);
        return true;
    }

    /// <summary>
    /// Un tiro Duality vuole esattamente due dadi uguali a segno positivo (i
    /// 2d12 dell'azione), più eventuali modificatori. Niente soglia di successi,
    /// niente dadi-da-statistica: la coppia dev'essere fissa e nota, perché il
    /// resolver ne prende il primo come Speranza e il secondo come Paura.
    /// </summary>
    private static bool TryBuildDuality(
        List<DiceTerm> terms, int? soglia, string text,
        out IRollResolver? resolver, out string? error)
    {
        resolver = null;
        error = null;

        if (soglia is not null)
        {
            error = $"Un tiro Duality non conta successi: togli «≥» da «{text}».";
            return false;
        }
        if (terms.Any(t => t.IsStatDice))
        {
            error = $"Un tiro Duality vuole due dadi fissi, non dadi-da-statistica, in «{text}».";
            return false;
        }

        var diceTerms = terms.Where(t => t.IsDice).ToList();
        var diceCount = diceTerms.Sum(t => t.Count);
        var facceUguali = diceTerms.Select(t => t.Sides).Distinct().Count() <= 1;
        if (diceCount != 2 || !facceUguali || diceTerms.Any(t => t.Sign < 0))
        {
            error = $"Un tiro Duality vuole esattamente due dadi uguali, es. «duality: 2d12». In «{text}».";
            return false;
        }

        resolver = new DualityResolver();
        return true;
    }

    /// <summary>
    /// In un nome letto avido, dove finisce il nome e comincia il <c>dNN</c>.
    /// Zero se non c'è un suffisso di dadi.
    ///
    /// "Destrezzad10" → 9 ("Destrezza"). "Forza" → 0. "d10" → 0: un nome vuoto
    /// non è un nome, e quel caso è il dado semplice, che qui non arriva.
    /// </summary>
    private static int SuffissoDadi(string name)
    {
        var i = name.Length;
        while (i > 0 && char.IsAsciiDigit(name[i - 1]))
            i--;

        if (i == name.Length)      // non finisce con cifre
            return 0;
        if (i - 1 <= 0)            // niente 'd', o niente nome prima della 'd'
            return 0;
        if (name[i - 1] != 'd' && name[i - 1] != 'D')
            return 0;

        return i - 1;
    }

    /// <summary>Analizza il <c>&gt;= N</c> finale.</summary>
    private static bool TryParseThreshold(string text, ref int i, out int? soglia, out string? error)
    {
        soglia = null;
        error = null;

        i++; // consuma '>'
        if (i >= text.Length || text[i] != '=')
        {
            error = $"Per contare i successi si scrive '>=', non '>' da solo, in «{text}».";
            return false;
        }
        i++; // consuma '='

        SkipSpaces(text, ref i);
        var digits = ReadDigits(text, ref i);
        if (digits.Length == 0)
        {
            error = $"Manca il numero dopo '>=' in «{text}». Es. «@Destrezzad10 >= 6».";
            return false;
        }

        SkipSpaces(text, ref i);
        if (i < text.Length)
        {
            error = $"Dopo la soglia '>= {digits}' non può esserci altro, in «{text}».";
            return false;
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
        {
            error = $"La soglia dei successi dev'essere almeno 1: «{digits}» in «{text}».";
            return false;
        }

        soglia = n;
        return true;
    }

    private static void SkipSpaces(string text, ref int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
            i++;
    }

    private static bool TryParseTerm(string text, ref int i, int sign, out DiceTerm term, out string? error)
    {
        term = default;
        error = null;

        if (text[i] == '@')
            return TryParseStat(text, ref i, sign, out term, out error);

        // NdM, dM, oppure una costante.
        var digits = ReadDigits(text, ref i);
        var hasDie = i < text.Length && (text[i] == 'd' || text[i] == 'D');

        if (!hasDie)
        {
            if (digits.Length == 0)
            {
                error = $"Termine non riconosciuto in «{text}»: atteso un numero, NdM o @statistica.";
                return false;
            }
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                error = $"Numero troppo grande in «{text}».";
                return false;
            }
            term = DiceTerm.Number(sign, value);
            return true;
        }

        i++; // consuma la 'd'
        var sidesDigits = ReadDigits(text, ref i);
        if (sidesDigits.Length == 0)
        {
            error = $"Manca il numero di facce dopo 'd' in «{text}».";
            return false;
        }

        // "d6" senza numero davanti vale "1d6".
        var count = 1;
        if (digits.Length > 0 && !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out count))
        {
            error = $"Quantità di dadi troppo grande in «{text}».";
            return false;
        }
        if (!int.TryParse(sidesDigits, NumberStyles.None, CultureInfo.InvariantCulture, out var sides))
        {
            error = $"Numero di facce troppo grande in «{text}».";
            return false;
        }

        if (count is < 1 or > MaxDiceCount)
        {
            error = $"La quantità di dadi dev'essere tra 1 e {MaxDiceCount} (in «{text}»).";
            return false;
        }
        if (sides is < 2 or > MaxSides)
        {
            error = $"Le facce devono essere tra 2 e {MaxSides} (in «{text}»).";
            return false;
        }

        term = DiceTerm.Dice(sign, count, sides);
        return true;
    }

    private static bool TryParseStat(string text, ref int i, int sign, out DiceTerm term, out string? error)
    {
        term = default;
        error = null;
        i++; // consuma la '@'

        string name;
        var conGraffe = i < text.Length && text[i] == '{';
        if (conGraffe)
        {
            var close = text.IndexOf('}', i);
            if (close < 0)
            {
                error = $"Manca la graffa di chiusura dopo '@{{' in «{text}».";
                return false;
            }
            name = text[(i + 1)..close].Trim();
            i = close + 1;
        }
        else
        {
            var start = i;
            while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                i++;
            name = text[start..i];

            // Il nome si legge avido, e 'd' e cifre sono lettere e cifre:
            // "@Destrezzad10" arriva qui tutto attaccato. Il suffisso 'dNN' va
            // staccato a mano — con le graffe il problema non si pone, ed è la
            // via d'uscita per una statistica che finisse davvero per "d10".
            var taglio = SuffissoDadi(name);
            if (taglio > 0)
            {
                i -= name.Length - taglio;
                name = name[..taglio];
            }
        }

        if (name.Length == 0)
        {
            error = $"Riferimento a statistica senza nome in «{text}». Usa @Nome oppure @{{Nome con spazi}}.";
            return false;
        }

        // '@Nome' seguito da 'dM' è un pool: quanti dadi lo dice il punteggio.
        // Senza, è il riferimento nudo di sempre. Lo spazio è ammesso —
        // "@Destrezza d10" si legge meglio di "@Destrezzad10".
        SkipSpaces(text, ref i);
        if (i < text.Length && (text[i] == 'd' || text[i] == 'D'))
        {
            var dopo = i + 1;
            var facce = ReadDigits(text, ref dopo);
            if (facce.Length > 0)
            {
                i = dopo;
                if (!int.TryParse(facce, NumberStyles.None, CultureInfo.InvariantCulture, out var sides)
                    || sides < 2 || sides > MaxSides)
                {
                    error = $"Un dado deve avere fra 2 e {MaxSides} facce: «{facce}» in «{text}».";
                    return false;
                }
                term = DiceTerm.StatDice(sign, name, sides);
                return true;
            }
        }

        term = DiceTerm.Stat(sign, name);
        return true;
    }

    private static string ReadDigits(string text, ref int i)
    {
        var start = i;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
            i++;
        return text[start..i];
    }

    /// <summary>Le statistiche a cui questa formula fa riferimento.</summary>
    public IEnumerable<string> StatNames => Terms.Where(t => t.IsStat).Select(t => t.StatName!);

    /// <summary>
    /// Tira. <paramref name="stats"/> sono i valori effettivi del personaggio
    /// (base + effetti attivi), più la chiave riservata dei PF massimi.
    ///
    /// Fallisce — restituendo <see cref="ValidationResult"/>, che è già il
    /// canale delle spiegazioni in italiano — se una statistica non esiste.
    /// Fallisce PRIMA di tirare: nessun dado si consuma su una formula rotta.
    /// </summary>
    public ValidationResult TryEvaluate(
        IReadOnlyDictionary<string, object?> stats,
        RollOptions options,
        IRandomSource rng,
        string subject,
        out RollResult? result)
    {
        result = null;

        // Prima si risolve tutto, poi si tira: un tiro mostrato e poi annullato
        // è peggio di un tiro mai fatto.
        // Anche i pool: in '@Destrezzad10' la Destrezza dice quanti dadi, e se
        // non esiste il tiro deve fermarsi qui, non esplodere mentre cade.
        var resolved = new List<StatRef>();
        foreach (var term in Terms.Where(t => t.StatName is not null))
        {
            if (resolved.Any(r => r.Name == term.StatName))
                continue;
            if (!TryResolve(stats, term.StatName!, out var value))
                return ValidationResult.Fail(
                    $"La statistica '{term.StatName}' non esiste su '{subject}'.");
            resolved.Add(new StatRef(term.StatName!, value));
        }

        var byName = resolved.ToDictionary(r => r.Name, r => r.Value);
        var attempts = new List<RollAttempt>();
        for (var attempt = 0; attempt < options.EffectiveTimes; attempt++)
            attempts.Add(RollOnce(byName, rng));

        var kept = options.Keep switch
        {
            Keep.Highest => IndexOfBest(attempts, best: true),
            Keep.Lowest => IndexOfBest(attempts, best: false),
            _ => 0,
        };

        result = new RollResult(Source, attempts, kept, resolved);
        return ValidationResult.Ok();
    }

    private RollAttempt RollOnce(IReadOnlyDictionary<string, int> stats, IRandomSource rng)
    {
        var rolled = new List<RolledDie>();
        var modifier = 0;

        foreach (var term in Terms)
        {
            if (term.IsDice || term.IsStatDice)
            {
                // Quanti dadi: un numero scritto, oppure il punteggio. Un pool
                // negativo non vuol dire niente e vale zero dadi.
                var quanti = term.IsStatDice
                    ? Math.Clamp(stats[term.StatName!], 0, MaxDiceCount)
                    : term.Count;

                for (var n = 0; n < quanti; n++)
                    rolled.Add(new RolledDie(term.Sides, rng.Next(1, term.Sides + 1), term.Sign));
            }
            else if (term.IsStat)
            {
                modifier += term.Sign * stats[term.StatName!];
            }
            else
            {
                modifier += term.Sign * term.Constant;
            }
        }

        // Come i dadi diventino un risultato — somma, conteggio di successi, o il
        // confronto Duality — lo decide il resolver della formula. Qui si tira e
        // basta; il segno resta sul dado grezzo perché al resolver serve.
        var resolved = Resolver.Resolve(rolled, modifier);
        return new RollAttempt(resolved.Dice, resolved.Modifier, resolved.Total, resolved.Outcome);
    }

    private static int IndexOfBest(List<RollAttempt> attempts, bool best)
    {
        var index = 0;
        for (var i = 1; i < attempts.Count; i++)
        {
            var better = best ? attempts[i].Total > attempts[index].Total
                              : attempts[i].Total < attempts[index].Total;
            if (better)
                index = i;
        }
        return index;
    }

    /// <summary>
    /// Risoluzione di una statistica. Passa da <see cref="Character.TryReadNumber"/>
    /// perché dopo il giro in SQLite un 15 torna come <c>JsonElement</c>, non come
    /// <c>int</c>: un cast diretto darebbe 0 a ogni @riferimento solo dopo un
    /// riavvio, funzionando benissimo nei test.
    /// </summary>
    /// <summary>
    /// La formula come la leggerebbe un giocatore: <c>1d20+6</c> invece di
    /// <c>1d20+@Destrezza+@Competenza</c>. I riferimenti si risolvono e le
    /// costanti si sommano in un modificatore solo, che è il numero che si
    /// somma al dado.
    ///
    /// Null se una statistica non si risolve: chi chiama mostra la formula
    /// grezza: meglio un <c>@Nome</c> in faccia che un numero inventato.
    /// </summary>
    public string? Describe(IReadOnlyDictionary<string, object?> stats)
    {
        var pezzi = new List<string>();
        var modificatore = 0;

        foreach (var term in Terms)
        {
            if (term.StatName is not null && !TryResolve(stats, term.StatName, out _))
                return null;

            if (term.IsDice)
            {
                pezzi.Add($"{Segno(term.Sign, pezzi.Count == 0)}{term.Count}d{term.Sides}");
            }
            else if (term.IsStatDice)
            {
                TryResolve(stats, term.StatName!, out var quanti);
                pezzi.Add($"{Segno(term.Sign, pezzi.Count == 0)}{quanti}d{term.Sides}");
            }
            else if (term.IsStat)
            {
                TryResolve(stats, term.StatName!, out var valore);
                modificatore += term.Sign * valore;
            }
            else
            {
                modificatore += term.Sign * term.Constant;
            }
        }

        if (modificatore != 0 || pezzi.Count == 0)
            pezzi.Add($"{Segno(Math.Sign(modificatore), pezzi.Count == 0)}{Math.Abs(modificatore)}");

        var testo = string.Concat(pezzi);
        return Resolver.FormulaSuffix is { } suffix ? $"{testo} {suffix}" : testo;

        static string Segno(int sign, bool primo) =>
            sign < 0 ? "−" : primo ? "" : "+";
    }

    private static bool TryResolve(IReadOnlyDictionary<string, object?> stats, string name, out int value)
    {
        if (stats.TryGetValue(name, out var raw))
            return Characters.Character.TryReadNumber(raw, out value);

        foreach (var (key, candidate) in stats)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                return Characters.Character.TryReadNumber(candidate, out value);
        }

        value = 0;
        return false;
    }
}
