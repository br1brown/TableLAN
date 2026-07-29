namespace TableLAN.Core.Dice;

/// <summary>
/// Un termine con segno di una formula. Quattro forme, mutuamente esclusive:
/// <list type="bullet">
///   <item>dadi fissi — <c>2d6</c>: <see cref="Sides"/> e <see cref="Count"/> &gt; 0</item>
///   <item>dadi a quantità variabile — <c>@Destrezzad10</c>: <see cref="Sides"/> &gt; 0
///         e <see cref="StatName"/> valorizzato; quanti dadi lo dice il punteggio</item>
///   <item>riferimento a statistica — <c>@Forza</c>: solo <see cref="StatName"/></item>
///   <item>costante — <c>3</c></item>
/// </list>
///
/// La seconda forma esiste per i sistemi a pool: nel Mondo di Tenebra si tira
/// un numero di d10 pari al punteggio, e <c>7d10</c> darebbe una formula che
/// resta a sette dadi anche quando il punteggio cambia.
/// </summary>
/// <param name="Sign">+1 o −1.</param>
/// <param name="Explode">
/// Dadi esplosivi (<c>3d6!</c>): un dado che cade sul massimo si ri-tira e si
/// somma, e ancora finché non smette — è l'«Ace» di Savage Worlds. Solo per i
/// dadi veri, non per le costanti.
/// </param>
/// <param name="Keep">
/// «Tieni i migliori/peggiori N» (<c>4d6kh3</c>, <c>2d20kl1</c>): 0 = tutti,
/// &gt;0 = tieni gli N più alti, &lt;0 = tieni gli |N| più bassi. È la
/// generazione delle caratteristiche di D&amp;D (tira 4d6, scarta il più basso)
/// e lo svantaggio/vantaggio scritto in un termine solo. I dadi scartati si
/// vedono ancora, sbarrati, ma non contano.
/// </param>
/// <param name="Fudge">
/// Dadi Fudge/Fate (<c>4dF</c>): ogni dado vale −1, 0 o +1 con uguale
/// probabilità, e si sommano. È il tiro di Fate — non hanno «facce» nel senso
/// comune (il valore <em>è</em> il segno), quindi <see cref="Sides"/> resta a 3
/// solo per contarli come dadi, ma non si mostra.
/// </param>
public readonly record struct DiceTerm(int Sign, int Count, int Sides, string? StatName, int Constant, bool Explode = false, int Keep = 0, bool Fudge = false)
{
    public static DiceTerm Dice(int sign, int count, int sides, bool explode = false, int keep = 0) => new(sign, count, sides, null, 0, explode, keep);
    public static DiceTerm Stat(int sign, string name) => new(sign, 0, 0, name, 0);
    public static DiceTerm Number(int sign, int value) => new(sign, 0, 0, null, value);

    /// <summary>Dadi la cui quantità è un punteggio: <c>@Destrezzad10</c>.</summary>
    public static DiceTerm StatDice(int sign, string name, int sides, bool explode = false, int keep = 0) => new(sign, 0, sides, name, 0, explode, keep);

    /// <summary>Dadi Fudge/Fate: <c>4dF</c>, ognuno −1/0/+1.</summary>
    public static DiceTerm FudgeDice(int sign, int count) => new(sign, count, 3, null, 0, Fudge: true);

    public bool IsDice => Sides > 0 && StatName is null;

    /// <summary>Vero per <c>@Nome</c> nudo, non per <c>@Nomed10</c>.</summary>
    public bool IsStat => StatName is not null && Sides == 0;

    /// <summary>Vero per <c>@Nomed10</c>: quanti dadi lo dice la statistica.</summary>
    public bool IsStatDice => StatName is not null && Sides > 0;
}
