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
public readonly record struct DiceTerm(int Sign, int Count, int Sides, string? StatName, int Constant)
{
    public static DiceTerm Dice(int sign, int count, int sides) => new(sign, count, sides, null, 0);
    public static DiceTerm Stat(int sign, string name) => new(sign, 0, 0, name, 0);
    public static DiceTerm Number(int sign, int value) => new(sign, 0, 0, null, value);

    /// <summary>Dadi la cui quantità è un punteggio: <c>@Destrezzad10</c>.</summary>
    public static DiceTerm StatDice(int sign, string name, int sides) => new(sign, 0, sides, name, 0);

    public bool IsDice => Sides > 0 && StatName is null;

    /// <summary>Vero per <c>@Nome</c> nudo, non per <c>@Nomed10</c>.</summary>
    public bool IsStat => StatName is not null && Sides == 0;

    /// <summary>Vero per <c>@Nomed10</c>: quanti dadi lo dice la statistica.</summary>
    public bool IsStatDice => StatName is not null && Sides > 0;
}
