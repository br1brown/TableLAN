namespace TableLAN.Core.Engine;

using TableLAN.Core.Profile;

/// <summary>
/// Stato dell'economia del turno di un personaggio. Non più quattro booleani
/// fissi (Azione/Bonus/Reazione/Interazione) ma un dizionario risorsa → residui
/// costruito dal profilo di sistema: un gioco senza azioni bonus, o con un pool
/// di punti azione, si esprime cambiando il profilo, non il codice.
/// </summary>
public sealed class TurnState
{
    private readonly Dictionary<string, int> _remaining = new();

    public TurnState(IReadOnlyDictionary<string, int> limits) => ResetForNewTurn(limits);

    public void ResetForNewTurn(IReadOnlyDictionary<string, int> limits)
    {
        _remaining.Clear();
        foreach (var (resource, limit) in limits)
            _remaining[resource] = limit;
    }

    public int Remaining(string resourceId) => _remaining.GetValueOrDefault(resourceId, 0);

    public bool CanPay(string resourceId, int amount) => Remaining(resourceId) >= amount;

    public void Pay(string resourceId, int amount)
    {
        if (!CanPay(resourceId, amount))
            throw new InvalidOperationException($"Risorsa di turno '{resourceId}' insufficiente.");
        _remaining[resourceId] -= amount;
    }

    /// <summary>
    /// Accredita una risorsa nel turno in corso.
    ///
    /// Può superare il limite per turno del profilo, ed è il punto: Action
    /// Surge dà una seconda Azione <em>adesso</em>, non alza il tetto da domani.
    /// Il tetto vale al reset; questo è un credito una tantum.
    /// </summary>
    public void Grant(string resourceId, int amount) =>
        _remaining[resourceId] = Remaining(resourceId) + amount;

    public IReadOnlyDictionary<string, int> Snapshot() => _remaining;
}
