namespace TableLAN.Core.Dice;

/// <summary>
/// Cosa fare di più tentativi dello stesso tiro.
/// </summary>
public enum Keep
{
    /// <summary>Un tentativo solo (o, se più d'uno, li si guarda tutti).</summary>
    Sum,

    /// <summary>Vince il totale più alto — il "vantaggio" di D&D 5e.</summary>
    Highest,

    /// <summary>Vince il totale più basso — lo "svantaggio".</summary>
    Lowest,
}

/// <summary>
/// Modificatori di un singolo tiro.
///
/// Sta sulla richiesta e NON nel profilo di sistema, di proposito: il profilo è
/// un vocabolario di <em>risorse</em> (turno, riserve, cicli), non di semantica
/// dei dadi. Tenerlo qui lascia il profilo agnostico e rende additiva ogni
/// estensione futura.
///
/// Onestà sulla copertura: <see cref="Keep"/> descrive vantaggio/svantaggio di
/// D&amp;D 5e e fortune/misfortune di Pathfinder 2e. NON descrive il Wild Die di
/// Savage Worlds (due formule diverse), né i pool a successi di World of
/// Darkness e Call of Cthulhu, né i dadi che esplodono. Quando serviranno,
/// saranno nuovi valori qui — non una riscrittura.
/// </summary>
/// <param name="Times">Quante volte tirare la formula. Minimo 1.</param>
/// <param name="Keep">Quale tentativo vale.</param>
public readonly record struct RollOptions(int Times = 1, Keep Keep = Keep.Sum)
{
    public static readonly RollOptions Single = new();

    /// <summary>
    /// Tentativi effettivi: almeno uno, e non più di dieci — ma almeno due con
    /// vantaggio o svantaggio, perché "tieni il più alto di un tentativo solo"
    /// non vuol dire niente.
    ///
    /// Prima la regola stava nel chiamante: chi chiedeva <see cref="Keep.Highest"/>
    /// senza passare anche <c>Times = 2</c> otteneva un tiro normale — un
    /// vantaggio silenziosamente inerte, che è peggio di un errore. I test lo
    /// scrivevano a mano e restavano verdi; la scheda no.
    /// </summary>
    public int EffectiveTimes =>
        Math.Clamp(Math.Max(Times, Keep == TableLAN.Core.Dice.Keep.Sum ? 1 : 2), 1, 10);
}
