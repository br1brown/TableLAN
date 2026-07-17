namespace TableLAN.Core.Dice;

/// <summary>
/// Sorgente di casualità del motore dei dadi. Esiste per un motivo solo:
/// senza un seam iniettabile un tiro non è verificabile, e un motore di dadi
/// non testabile è un motore di cui nessuno può fidarsi.
/// </summary>
public interface IRandomSource
{
    /// <summary>Intero in [minInclusive, maxExclusive).</summary>
    int Next(int minInclusive, int maxExclusive);
}

/// <summary>Implementazione di esercizio: la casualità della piattaforma.</summary>
public sealed class SystemRandom : IRandomSource
{
    public static readonly SystemRandom Instance = new();

    public int Next(int minInclusive, int maxExclusive) =>
        Random.Shared.Next(minInclusive, maxExclusive);
}
